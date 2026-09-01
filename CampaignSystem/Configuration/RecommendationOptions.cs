namespace CampaignSystem.Configuration;

/// <summary>
/// Settings for the campaign recommendation engine, bound from the "Recommendation" section.
///
/// These are the only knobs the heuristic exposes, and tuning them is what "training" means
/// for it right now. A later model can read the same transaction history and replace the
/// scoring outright without any of these names leaking into its callers.
///
/// AĞIRLIK GEREKÇESİ (2026-09-01): Aşağıdaki üç skor ağırlığı (Spend / Trend / Season) üç
/// sentetik veri setinde yapılan taramayla belirlendi — biri elle kurgulanmış, ikisi tamamen
/// rastgele. Her veri setine, motorun karşısında ölçüldüğü BİLİNEN yapısal trendler enjekte
/// edildi. Bkz. docs/analysis/weight_sweep.py ve docs/kampanya-oneri-motoru-rapor.md §B.6.
/// Yön (trend hacimden ağır basar; statik sezon önceli kısılır) üç veri setinde de sağlam
/// çıktı; kesin sayılar, yeterli gerçek işlem geçmişi birikince tekrar gözden geçirilmeli.
/// Ölçüt: motor skoru ile enjekte edilen gerçek trend arasındaki Spearman ρ, kırılımlı
/// kategorilerde 3 veri seti ortalamasında 0,29 → 0,49'a çıktı (elle kurgulanmış sette
/// -0,20 → +0,40 — yani motor artık gerçekten büyüyeni gerçekten düşenin üstüne koyuyor).
/// </summary>
public class RecommendationOptions
{
    public const string SectionName = "Recommendation";

    /// <summary>
    /// How far back the spend and trend figures look, in days. The window is split in half to
    /// read a trend: the more recent half against the one before it.
    /// </summary>
    public int LookbackDays { get; set; } = 90;

    /// <summary>
    /// How many days ahead a suggested campaign is assumed to run. Decides which months'
    /// seasonal weights are averaged into a suggestion's score, and the dates that prefill
    /// the campaign form.
    /// </summary>
    public int HorizonDays { get; set; } = 45;

    /// <summary>
    /// Lookback penceresinde bu net harcamanın altında kalan kategori baştan elenir.
    /// 1000 → 7500: skorun parçası değil, gürültü filtresi. Çok küçük kategoriler tek bir
    /// büyük işlemle "artışta" görünüp öneri listesine sızabiliyordu; eşiği yükseltmek bunu
    /// engelliyor (bkz. rapor §B.6).
    /// </summary>
    public decimal MinimumSpend { get; set; } = 7500m;

    /// <summary>How many suggestions the endpoint returns at most, best first.</summary>
    public int MaxSuggestions { get; set; } = 10;

    /// <summary>
    /// Normalize edilmiş harcama hacminin skordaki ağırlığı.
    /// 1.0 → 0.85: Tek veri setinde daha büyük bir kesinti (0,65) iyi görünmüştü ama üç veri
    /// seti birlikte bunu desteklemedi — yüksek TrendWeight'te SpendWeight'i 0,85–1,0'da
    /// tutmak en iyi sonucu verdi. Küçük bir düşüş nötr-olumlu: cirosu büyük ama trendi düz/
    /// düşen kategorilerin (ör. Elektronik, Turizm) sırf hacim yüzünden "önerilir" bölgesinde
    /// kalmasını bir miktar azaltır.
    /// </summary>
    public double SpendWeight { get; set; } = 0.85;

    /// <summary>
    /// Harcama trendinin (pencerenin son yarısı / önceki yarısı) skordaki ağırlığı.
    /// 1.5 → 2.0: Taramanın gösterdiği ASIL kaldıraç bu. 1,5 → ~1,9–2,2 aralığına çıkarmak
    /// üç veri setinde de hem bağımsız istatistiksel sıraya hem enjekte edilen gerçek trende
    /// uyumu iyileştiriyor. 2,2'nin üstünde kazanç düzleşiyor, o yüzden 2,0 seçildi.
    /// </summary>
    public double TrendWeight { get; set; } = 2.0;

    /// <summary>
    /// Ufuk boyunca beklenen sezonsal artışın (SEASONAL_PATTERN öncül tablosu) skordaki ağırlığı.
    /// 1.25 → 1.0: Statik öncül tablo CARİ YIL kırılımını göremiyor — ör. bir yıl okula dönüş
    /// dönemi zayıf geçerse öncül yine "Eylül yüksek" der ve skoru yanlış yöne iter. Taramada
    /// bu terimin ağırlığını düşürmek fayda-zarar dengesinde tutarlı biçimde daha iyi çıktı.
    /// Sezonsallığı tamamen kaldırmaz — gerçek sezon etkisi zaten trend/momentum sinyaline
    /// yansıyor; Faz 2'de öncül, veriden öğrenilen ay endeksiyle değiştirilecek.
    /// </summary>
    public double SeasonWeight { get; set; } = 1.0;

    /// <summary>
    /// Extra multiplier for a category that no open or upcoming campaign already covers. The
    /// engine exists to surface these, so an uncovered category outranks a covered one at the
    /// same spend.
    /// </summary>
    public double CoverageGapBoost { get; set; } = 1.75;

    /// <summary>
    /// Fraction of a category's average qualifying transaction that becomes the suggested
    /// RewardPoint on the prefilled form. A starting point for the operator, nothing binding.
    /// </summary>
    public double SuggestedRewardRate { get; set; } = 0.02;
}
