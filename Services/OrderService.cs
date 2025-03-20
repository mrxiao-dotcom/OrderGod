using DatabaseConfigDemo.Models;

namespace DatabaseConfigDemo.Services;

public class OrderService
{
    private readonly IDbService _dbService;
    private readonly ILogger _logger;

    public OrderService(IDbService dbService, ILogger logger)
    {
        _dbService = dbService;
        _logger = logger;
    }

    public async Task<OrderModel> CreateOrderAsync(
        long accountId,
        int quantity,
        decimal entryPrice,
        string direction,
        decimal stopLossPrice,
        TakeProfitStrategy? takeProfitStrategy = null)
    {
        try
        {
            var order = new OrderModel
            {
                AccountId = accountId,
                OrderId = Guid.NewGuid().ToString(),
                Quantity = quantity,
                EntryPrice = entryPrice,
                Direction = direction,
                InitialStopLoss = stopLossPrice,
                CurrentStopLoss = stopLossPrice,
                Status = "pending",
                OpenTime = DateTime.Now
                // ... 设置其他属性
            };

            await _dbService.CreateOrderAsync(order, takeProfitStrategy);
            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError("创建订单失败", ex);
            throw;
        }
    }

    // ... 其他订单管理方法 ...
} 