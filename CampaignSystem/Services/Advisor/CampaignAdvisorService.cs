using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using CampaignSystem.Configuration;
using CampaignSystem.Data;
using CampaignSystem.DTOs;
using CampaignSystem.DTOs.Advisor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CampaignSystem.Services.Advisor;

/// <summary>
/// Tool-calling implementation of <see cref="ICampaignAdvisorService"/>.
///
/// The loop is written out rather than delegated to the SDK's tool runner for one reason:
/// every call the model makes has to end up in the answer's trace, and owning the loop is the
/// simplest way to guarantee that. It is the shape the SDK documents — send, look for
/// tool_use blocks, execute, send the results back — with a hard iteration cap.
///
/// Adding a tool is the only way to widen what the advisor can say. That is the point: the
/// boundary of its knowledge is a list in source control, not a prompt.
/// </summary>
public class CampaignAdvisorService(
    AnthropicClient client,
    CampaignDbContext context,
    ICampaignRecommendationService recommendations,
    IOptions<AdvisorOptions> options,
    ILogger<CampaignAdvisorService> logger) : ICampaignAdvisorService
{
    private readonly AdvisorOptions _options = options.Value;

    private static readonly JsonSerializerOptions JsonFormat = new()
    {
        // Turkish category and segment names would otherwise be escaped to \uXXXX, which
        // wastes tokens and makes the trace unreadable on the screen.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// The rules that keep the model inside what the data can actually support. Points 2 and 4
    /// are the important ones: they name the two things this system cannot yet measure, so the
    /// model says "not available" instead of producing a plausible number for them.
    /// </summary>
    private const string SystemPrompt = """
        Sen bir bankanın kampanya yönetim sisteminde çalışan, iş birimine kampanya fikri sunan
        bir danışmansın. Kullanıcı bir kampanya yöneticisidir.

        KURALLAR:

        1. Cevabındaki HER SAYI bir araç çağrısından gelmek zorundadır. Kendin toplama,
           oranlama veya tahmin yapma. Aracın döndürmediği bir sayıyı yazma.

        2. Katılım oranı, artımsal harcama (incremental spend) ve ROI şu anda ÖLÇÜLEMİYOR:
           sistemde kontrol grubu (holdout) yok, dolayısıyla "kampanya olmasaydı ne olurdu"
           sorusunun cevabı veride mevcut değil. Bu üç sayıyı asla üretme. Sorulursa neden
           ölçülemediğini kısaca açıkla.

        3. Bir kırılım araçlarda yoksa "bu veri sistemde yok" de ve orada dur. Tahminle
           doldurma.

        4. ÖNEMLİ KISIT: öneri motoru şu an banka geneli çalışır. Segment bazlı kategori
           kırılımı HENÜZ YOK. Bir segment sorulduğunda segmenti çözebilir, büyüklüğünü
           söyleyebilirsin; ama o segmente özel harcama dağılımı veremezsin. Bunu açıkça
           belirt ve genel motorun ne söylediğini ayrı ayrı aktar.

        5. Türkçe, sade ve iş birimine hitap eden bir dille yaz. Madde madde, kısa.

        6. Bir kampanya fikri sunarken hem lehte gerekçeyi hem varsa aleyhte gerekçeyi ver.
           Kapsama boşluğu (o kategoride aktif kampanya olmaması) lehte güçlü bir sinyaldir;
           harcamanın düşüş trendinde olması aleyhte bir sinyaldir.
        """;

    private static readonly Tool[] AdvisorTools =
    [
        new Tool
        {
            Name = "resolve_segment",
            Description =
                "Bir müşteri segmentini adından veya kodundan bulur ve aktif müşteri sayısını " +
                "döndürür. Eşleşme bulunamazsa sistemdeki tüm segmentleri listeler. " +
                "Kullanıcı bir segment adı geçirdiğinde (ör. 'Çiftçi') önce bunu çağır.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["name"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "string",
                        description = "Segment adı veya kodu, örn. 'Çiftçi' ya da 'CFT'."
                    })
                },
                Required = ["name"]
            }
        },
        new Tool
        {
            Name = "get_campaign_suggestions",
            Description =
                "Kampanya öneri motorunu çalıştırır: son dönem kart harcamalarına bakarak hangi " +
                "merchant kategorisinde kampanya açmaya değdiğini sıralar. Her satır net harcama, " +
                "trend oranı, sezonsal ağırlık ve o kategoriyi zaten hedefleyen aktif kampanya " +
                "olup olmadığını içerir. BANKA GENELİ çalışır — segment kırılımı vermez. " +
                "Tüm parametreler isteğe bağlıdır.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["lookbackDays"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "integer",
                        description = "Geçmişe bakış penceresi, gün. 14-365 arası, varsayılan 90."
                    }),
                    ["horizonDays"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "integer",
                        description = "Önerilen kampanyanın süreceği varsayılan gün sayısı. 7-180, varsayılan 45."
                    }),
                    ["minimumSpend"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "number",
                        description = "Bu net harcamanın altındaki kategoriler elenir. Varsayılan 7500."
                    }),
                    ["maxSuggestions"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "integer",
                        description = "En fazla kaç öneri dönsün. 1-50, varsayılan 10."
                    }),
                    ["includeCovered"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "boolean",
                        description = "true ise aktif kampanyanın zaten kapsadığı kategoriler de döner. Varsayılan false."
                    })
                },
                Required = []
            }
        }
    ];

    public async Task<ServiceResult<AdvisorAnswerDto>> AskAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey)
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
        {
            return ServiceResult<AdvisorAnswerDto>.Invalid(
                "Advisor:ApiKey ayarlanmamış. Geliştirmede: " +
                "dotnet user-secrets set \"Advisor:ApiKey\" \"<anahtar>\" --project CampaignSystem");
        }

        List<MessageParam> messages = [new() { Role = Role.User, Content = question }];
        List<AdvisorToolCallDto> trace = [];

        for (var iteration = 0; iteration < _options.MaxToolIterations; iteration++)
        {
            var response = await client.Messages.Create(
                new MessageCreateParams
                {
                    Model = _options.Model,
                    MaxTokens = _options.MaxTokens,
                    System = SystemPrompt,
                    Tools = [.. AdvisorTools],
                    Messages = messages
                },
                cancellationToken: cancellationToken);

            // The assistant turn has to be echoed back verbatim alongside the tool results, so
            // every block is rebuilt as its *Param counterpart. Thinking blocks carry a
            // signature the API validates — copying it is not optional.
            List<ContentBlockParam> assistantContent = [];
            List<ContentBlockParam> toolResults = [];

            foreach (var block in response.Content)
            {
                if (block.TryPickText(out TextBlock? text))
                {
                    assistantContent.Add(new TextBlockParam { Text = text.Text });
                }
                else if (block.TryPickThinking(out ThinkingBlock? thinking))
                {
                    assistantContent.Add(new ThinkingBlockParam
                    {
                        Thinking = thinking.Thinking,
                        Signature = thinking.Signature
                    });
                }
                else if (block.TryPickRedactedThinking(out RedactedThinkingBlock? redacted))
                {
                    assistantContent.Add(new RedactedThinkingBlockParam { Data = redacted.Data });
                }
                else if (block.TryPickToolUse(out ToolUseBlock? toolUse))
                {
                    assistantContent.Add(new ToolUseBlockParam
                    {
                        ID = toolUse.ID,
                        Name = toolUse.Name,
                        Input = toolUse.Input
                    });

                    var arguments = JsonSerializer.Serialize(toolUse.Input, JsonFormat);
                    var output = await ExecuteToolAsync(toolUse.Name, toolUse.Input, cancellationToken);

                    logger.LogInformation(
                        "Advisor tool {Tool} called with {Arguments}", toolUse.Name, arguments);

                    trace.Add(new AdvisorToolCallDto(toolUse.Name, arguments, output));
                    toolResults.Add(new ToolResultBlockParam
                    {
                        ToolUseID = toolUse.ID,
                        Content = output
                    });
                }
            }

            // No tool was asked for, so this turn is the answer.
            if (toolResults.Count == 0)
            {
                var answer = string.Join(
                    "\n\n",
                    response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));

                return ServiceResult<AdvisorAnswerDto>.Success(new AdvisorAnswerDto(answer, trace));
            }

            messages.Add(new MessageParam { Role = Role.Assistant, Content = assistantContent });
            messages.Add(new MessageParam { Role = Role.User, Content = toolResults });
        }

        logger.LogWarning(
            "Advisor gave up after {Iterations} tool iterations", _options.MaxToolIterations);

        return ServiceResult<AdvisorAnswerDto>.Invalid(
            $"Danışman {_options.MaxToolIterations} tur sonunda sonuca varamadı. Soruyu daraltmayı deneyin.");
    }

    private async Task<string> ExecuteToolAsync(
        string name,
        IReadOnlyDictionary<string, JsonElement> input,
        CancellationToken cancellationToken)
    {
        try
        {
            return name switch
            {
                "resolve_segment" => await ResolveSegmentAsync(ReadString(input, "name"), cancellationToken),
                "get_campaign_suggestions" => await SuggestCampaignsAsync(input, cancellationToken),
                _ => Serialise(new { error = $"Bilinmeyen araç: {name}" })
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Handed back to the model as a tool result rather than thrown: the API rejects a
            // follow-up where a tool_use block has no matching tool_result, and the model can
            // often recover by calling something else.
            logger.LogError(exception, "Advisor tool {Tool} failed", name);
            return Serialise(new { error = "Araç çalıştırılamadı." });
        }
    }

    /// <summary>
    /// Matches on folded text so "ciftci", "Çiftçi" and "CFT" all find the same row. Returning
    /// the whole list when nothing matches is deliberate — it lets the model recover by picking
    /// the right segment itself instead of guessing an id.
    /// </summary>
    private async Task<string> ResolveSegmentAsync(string? name, CancellationToken cancellationToken)
    {
        var segments = await context.Segments
            .AsNoTracking()
            .Select(s => new
            {
                s.Id,
                s.SegmentCode,
                s.SegmentName,
                ActiveCustomers = s.Customers.Count(c => c.IsActive)
            })
            .ToListAsync(cancellationToken);

        var needle = Fold(name ?? "");

        var matches = needle.Length == 0
            ? []
            : segments
                .Where(s => Fold(s.SegmentName).Contains(needle) || Fold(s.SegmentCode).Contains(needle))
                .ToList();

        return matches.Count > 0
            ? Serialise(new { matches })
            : Serialise(new
            {
                matches = Array.Empty<object>(),
                note = "Eşleşme bulunamadı. Sistemdeki segmentlerin tamamı aşağıdadır.",
                available = segments
            });
    }

    /// <summary>
    /// Runs the existing recommendation engine and hands back a leaner shape than the endpoint
    /// returns — the draft's merchant id list can run to hundreds of entries and buys the model
    /// nothing.
    /// </summary>
    private async Task<string> SuggestCampaignsAsync(
        IReadOnlyDictionary<string, JsonElement> input,
        CancellationToken cancellationToken)
    {
        var suggestions = await recommendations.GetSuggestionsAsync(
            new RecommendationQueryDto
            {
                LookbackDays = ReadInt(input, "lookbackDays"),
                HorizonDays = ReadInt(input, "horizonDays"),
                MinimumSpend = ReadDecimal(input, "minimumSpend"),
                MaxSuggestions = ReadInt(input, "maxSuggestions"),
                IncludeCovered = ReadBool(input, "includeCovered") ?? false
            },
            cancellationToken);

        return Serialise(new
        {
            scope = "Banka geneli. Segment kırılımı içermez.",
            suggestions = suggestions.Select(s => new
            {
                s.Rank,
                s.Score,
                category = s.MerchantCategoryName,
                s.Headline,
                netSpend = s.Reason.TotalSpend,
                s.Reason.TransactionCount,
                s.Reason.TrendRatio,
                s.Reason.SeasonalWeight,
                s.Reason.IsCoverageGap,
                coveringCampaigns = s.Reason.CoveringCampaignIds.Count,
                suggestedRewardPoint = s.Draft.SuggestedRewardPoint
            })
        });
    }

    private static string Serialise(object value) => JsonSerializer.Serialize(value, JsonFormat);

    /// <summary>Lower-cases and strips Turkish diacritics so segment lookup is forgiving.</summary>
    private static string Fold(string value) => value
        .Replace('İ', 'i').Replace('I', 'i').Replace('Ç', 'c').Replace('Ğ', 'g')
        .Replace('Ö', 'o').Replace('Ş', 's').Replace('Ü', 'u')
        .ToLowerInvariant()
        .Replace('ç', 'c').Replace('ğ', 'g').Replace('ı', 'i')
        .Replace('ö', 'o').Replace('ş', 's').Replace('ü', 'u');

    private static string? ReadString(IReadOnlyDictionary<string, JsonElement> input, string key) =>
        input.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt(IReadOnlyDictionary<string, JsonElement> input, string key) =>
        input.TryGetValue(key, out var value) && value.TryGetInt32(out var parsed) ? parsed : null;

    private static decimal? ReadDecimal(IReadOnlyDictionary<string, JsonElement> input, string key) =>
        input.TryGetValue(key, out var value) && value.TryGetDecimal(out var parsed) ? parsed : null;

    private static bool? ReadBool(IReadOnlyDictionary<string, JsonElement> input, string key) =>
        input.TryGetValue(key, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
}
