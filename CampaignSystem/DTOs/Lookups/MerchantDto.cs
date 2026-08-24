namespace CampaignSystem.DTOs;

public class MerchantDto
{
    public int Id { get; set; }

    public string MerchantNumber { get; set; } = null!;

    public string MerchantName { get; set; } = null!;

    public bool IsActive { get; set; }
}
