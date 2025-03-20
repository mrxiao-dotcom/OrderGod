namespace DatabaseConfigDemo.Services;

public static class TradingCalculator
{
    // 计算下单市值
    public static decimal CalculateTotalValue(int quantity, decimal faceValue, int leverage)
    {
        return quantity * faceValue * leverage;
    }

    // 计算保证金
    public static decimal CalculateMargin(decimal totalValue, int leverage)
    {
        return totalValue / leverage;
    }

    // 计算手数
    public static int CalculateQuantity(decimal totalValue, decimal faceValue, int leverage)
    {
        return (int)(totalValue / (faceValue * leverage));
    }

    // 计算止损金额
    public static decimal CalculateStopLossAmount(int quantity, decimal entryPrice, decimal stopLossPrice, decimal faceValue, string direction)
    {
        var diff = direction.ToLower() == "buy" ? 
            stopLossPrice - entryPrice : 
            entryPrice - stopLossPrice;
        return Math.Abs(diff * quantity * faceValue);
    }

    // 计算止损比例
    public static decimal CalculateStopLossPercentage(decimal stopLossAmount, decimal margin)
    {
        return (stopLossAmount / margin) * 100;
    }

    // 计算止损价格
    public static decimal CalculateStopLossPrice(decimal entryPrice, decimal stopLossPercentage, decimal margin, int quantity, decimal faceValue, string direction)
    {
        var stopLossAmount = margin * stopLossPercentage / 100;
        var priceDiff = stopLossAmount / (quantity * faceValue);
        return direction.ToLower() == "buy" ? 
            entryPrice - priceDiff : 
            entryPrice + priceDiff;
    }
} 