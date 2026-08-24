namespace CampaignSystem.Entities;

/// <summary>
/// The wording behind one auto-generated line of <see cref="CampaignCondition"/>. Maps to
/// CAMPAIGN_CONDITION_TEMPLATE.
///
/// <see cref="Key"/> is what the code refers to when it decides a line applies (e.g. the
/// campaign has a minimum amount) — <see cref="TemplateText"/> is the only part meant to be
/// edited directly in the table, using <c>{TokenName}</c> placeholders that
/// <see cref="Services.CampaignService"/> fills in with values already formatted for display
/// (dates, "N0" amounts, Turkish enum words). A row can be turned off with
/// <see cref="IsActive"/> instead of being deleted, which simply drops that line from every
/// campaign's generated terms.
/// </summary>
public class CampaignConditionTemplate
{
    public int Id { get; set; }

    public string Key { get; set; } = null!;

    public string TemplateText { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}
