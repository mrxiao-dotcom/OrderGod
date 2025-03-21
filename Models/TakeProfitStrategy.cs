public class TakeProfitStrategy
{
    public long Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string StrategyType { get; set; } = string.Empty;
    public decimal? TriggerPrice { get; set; }
    public decimal? DrawdownPercentage { get; set; }
    public decimal? ProfitTriggerAmount { get; set; }
    public decimal? ProfitFallbackAmount { get; set; }
    public decimal? BreakevenProfitAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
} 