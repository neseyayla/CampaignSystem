using System.Text;
using System.Text.Json.Serialization;
using CampaignSystem.Configuration;
using CampaignSystem.Data;
using CampaignSystem.Middleware;
using CampaignSystem.Repositories;
using CampaignSystem.Services;
using CampaignSystem.Services.Caching;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Elastic.Serilog.Sinks;

var builder = WebApplication.CreateBuilder(args);

// Logging goes through Serilog. The console and the rolling daily file are always present;
// only the levels are read from the "Serilog" section of appsettings, so they can be tuned
// without a rebuild. Writing through the standard ILogger<T> everywhere means a central sink
// is added only here, with no change in the services.
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            "logs/campaignsystem-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14);

    // The central sink is opt-in through configuration: set Elasticsearch:Uri (the compose file
    // does, pointing at the elasticsearch service) and logs also stream there, to be searched in
    // Kibana. Left empty — as when running locally without Elasticsearch — the sink is simply not
    // added, so nothing depends on a server that is not there.
    var elasticsearchUri = context.Configuration["Elasticsearch:Uri"];

    if (!string.IsNullOrWhiteSpace(elasticsearchUri))
    {
        configuration.WriteTo.Elasticsearch([new Uri(elasticsearchUri)]);
    }
});

// Add services to the container.

// Enums travel as their names ("Mass", "CardBased") rather than as the integers C#
// assigns them. Readable in Swagger, and adding an enum member cannot silently change
// the meaning of a value an existing client sends.
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
// OpenAPI 3.0 zorlanıyor çünkü Swagger UI, .NET 10'un varsayılanı olan 3.1'deki
// dizi tipli (union) parametre şemalarını (ör. int route parametreleri için
// "type": ["integer","string"]) düzgün işleyemiyor ve "Required field is not
// provided" hatası veriyor.
builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;

    // Adds a JWT "Bearer" security scheme to the document so Swagger UI shows an Authorize
    // button: paste a token once and it travels on every request as "Authorization: Bearer …".
    // The token comes from POST /api/auth/login or /api/auth/admin/login.
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new Microsoft.OpenApi.OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, Microsoft.OpenApi.IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes["Bearer"] = new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.ParameterLocation.Header,
            Description = "Sadece token'ı yapıştır — 'Bearer ' önekine gerek yok."
        };

        document.Security ??= new List<Microsoft.OpenApi.OpenApiSecurityRequirement>();
        document.Security.Add(new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] =
                new List<string>()
        });

        return Task.CompletedTask;
    });
});

// The connection string is supplied by User Secrets in development and by an
// environment variable in every other environment; appsettings.json only holds an
// empty placeholder so the required key is discoverable.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured. In development, set it with: " +
        "dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"<value>\" --project CampaignSystem");
}

builder.Services.AddDbContext<CampaignDbContext>(options =>
    options.UseSqlServer(connectionString));

// Registered as an open generic: one registration covers IRepository<Segment>,
// IRepository<Campaign> and every other entity. Scoped, so a request shares one
// repository — and therefore one DbContext — across all the services it touches.
builder.Services.Configure<RewardCalculationOptions>(
    builder.Configuration.GetSection(RewardCalculationOptions.SectionName));

builder.Services.Configure<DailyBatchOptions>(
    builder.Configuration.GetSection(DailyBatchOptions.SectionName));

builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));

// The signing key follows the same rule as the connection string: User Secrets in
// development, an environment variable elsewhere, an empty placeholder in appsettings.json.
// Anyone holding it can mint a token for any customer, so the application refuses to start
// without one rather than falling back to something predictable.
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwt.SigningKey))
{
    throw new InvalidOperationException(
        "Jwt:SigningKey is not configured. In development, set it with: " +
        "dotnet user-secrets set \"Jwt:SigningKey\" \"<at least 32 characters>\" --project CampaignSystem");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),

            // No grace period on expiry. The default five minutes would keep a token working
            // after the screen has already signed the customer out.
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Any exception a controller or service does not handle ends up here: logged once and
// returned as a ProblemDetails 500 instead of leaking a stack trace to the caller.
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHostedService<DailyBatchHostedService>();

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// Backs the lookup caches (products, segments, merchants, transaction codes). Reference data
// is read on nearly every campaign screen and changes only through the admin write endpoints,
// which evict their own key. LookupCache centralises the caching policy over IMemoryCache.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<LookupCache>();
builder.Services.AddSingleton<CampaignCatalogCache>();

builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<ICampaignConditionService, CampaignConditionService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ICardService, CardService>();
builder.Services.AddScoped<IParticipationService, ParticipationService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IRewardService, RewardService>();
builder.Services.AddScoped<IDailyBatchService, DailyBatchService>();
builder.Services.AddScoped<ISegmentService, SegmentService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IMerchantService, MerchantService>();
builder.Services.AddScoped<ITransactionCodeService, TransactionCodeService>();
builder.Services.AddScoped<ICustomerCampaignService, CustomerCampaignService>();
builder.Services.AddScoped<IAuthService, AuthService>();



builder.Services.AddCors(options =>

{

    options.AddPolicy("AllowSwagger", policy =>

    {

        policy.AllowAnyOrigin()

              .AllowAnyMethod()

              .AllowAnyHeader();

    });

});



var app = builder.Build();

// Applies any pending migrations at startup, but only when explicitly asked to. The container
// sets RunMigrationsAtStartup=true so its empty database gets the schema on first boot; running
// locally leaves it unset, so nothing here touches the LocalDB you use by hand.
if (app.Configuration.GetValue<bool>("RunMigrationsAtStartup"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<CampaignDbContext>().Database.Migrate();
}



// One tidy line per HTTP request — method, path, status code and how long it took — instead
// of the framework's several. Placed first so it wraps everything below and times the whole
// request.
app.UseSerilogRequestLogging();

// Below the request logging so a handled exception is one Error log plus a single request
// summary line, not the same failure recorded twice.
app.UseExceptionHandler();

app.UseRouting();



app.UseCors("AllowSwagger"); // <-- Buraya ekleyin



app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "CampaignSystem API v1");
    });
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
