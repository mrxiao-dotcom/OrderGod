namespace DatabaseConfigDemo.Models;

public class OrderModel
{
    public long Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public long AccountId { get; set; }
    public string Contract { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal? InitialStopLoss { get; set; }
    public decimal? CurrentStopLoss { get; set; }
    public decimal? LastPrice { get; set; }
    public decimal? ClosePrice { get; set; }
    public decimal? FloatingPnL { get; set; }
    public decimal? RealizedProfit { get; set; }
    public int Leverage { get; set; }
    public decimal Margin { get; set; }
    public decimal TotalValue { get; set; }
    public decimal RiskAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public string? CloseType { get; set; }
    public decimal? HighestPrice { get; set; }
    public decimal? StopLossAmount { get; set; }
    public decimal? MaxFloatingProfit { get; set; }
    public decimal? RealProfit { get; set; }
}

public class TakeProfitStrategy
{
    public long Id { get; set; }
    public string OrderId { get; set; } = "";
    public string StrategyType { get; set; } = "";
    public decimal? TriggerPrice { get; set; }
    public decimal? DrawdownPercentage { get; set; }
    public decimal? ProfitTriggerAmount { get; set; }
    public decimal? ProfitFallbackAmount { get; set; }
    public decimal? BreakevenProfitAmount { get; set; }
    public string Status { get; set; } = "active";
} 