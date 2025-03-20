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

    public async Task<List<OrderModel>> GetActiveOrdersAsync(string accountId)
    {
        try
        {
            // 修复这里：将 string 转换为 long
            if (long.TryParse(accountId, out long accId))
            {
                return await _dbService.GetActiveOrdersAsync(accId);
            }
            _logger.LogError($"无效的账户ID格式: {accountId}", new ArgumentException("Invalid account ID format"));
            return new List<OrderModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError("获取活跃订单失败", ex);
            throw;
        }
    }

    public async Task<List<OrderModel>> GetCompletedOrdersAsync(string accountId)
    {
        try
        {
            if (long.TryParse(accountId, out long accId))
            {
                return await _dbService.GetCompletedOrdersAsync(accId);
            }
            _logger.LogError($"无效的账户ID格式: {accountId}", new ArgumentException("Invalid account ID format"));
            return new List<OrderModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError("获取已完成订单失败", ex);
            throw;
        }
    }
}