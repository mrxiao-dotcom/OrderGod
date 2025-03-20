using System.Threading.Tasks;
using System.Collections.Generic;
using DatabaseConfigDemo.Models;

namespace DatabaseConfigDemo.Services;

public interface IDbService
{
    Task<List<AccountItem>> GetActiveAccountsAsync();
    Task<List<OrderModel>> GetActiveOrdersAsync(long accountId);
    Task<List<OrderModel>> GetCompletedOrdersAsync(long accountId);
    Task<string> CreateOrderAsync(OrderModel order, TakeProfitStrategy? takeProfitStrategy = null);
    Task UpdateOrderAsync(OrderModel order);
    Task<bool> TestConnectionAsync();
    Task<AccountData> GetAccountDataAsync(long accountId);
    Task<AccountRiskData> GetAccountRiskDataAsync(long accountId);
} 