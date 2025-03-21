public class AccountData
{
    public long Id { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public decimal TotalEquity { get; set; }
    public decimal InitialValue { get; set; }
    public decimal TotalValue { get; set; }
    public decimal LeverageRatio { get; set; }
    public decimal UsedMargin { get; set; }
    public decimal AvailableMargin { get; set; }
    public decimal TotalRisk { get; set; }
    public decimal UsedRisk { get; set; }
    public decimal AvailableRisk { get; set; }
    public decimal MaxSingleRisk { get; set; }
    public decimal SuggestedRisk { get; set; }
} 