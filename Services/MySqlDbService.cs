using DatabaseConfigDemo.Models;
using MySql.Data.MySqlClient;
using System.Data;

namespace DatabaseConfigDemo.Services;

public class MySqlDbService : IDbService
{
    private readonly string _connectionString;
    private readonly ILogger _logger;

    public MySqlDbService(string connectionString, ILogger logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<List<AccountItem>> GetActiveAccountsAsync()
    {
        var accounts = new List<AccountItem>();
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            
            string query = "SELECT id, name FROM accounts WHERE status = 'active'";
            using var command = new MySqlCommand(query, connection);
            using MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                accounts.Add(new AccountItem
                {
                    AccountId = reader.GetInt64("id").ToString(),
                    AccountName = reader.GetString("name")
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("获取活跃账户失败", ex);
            throw;
        }
        return accounts;
    }

    public async Task<List<OrderModel>> GetActiveOrdersAsync(long accountId)
    {
        var orders = new List<OrderModel>();
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            
            string query = @"SELECT * FROM simulation_orders 
                           WHERE account_id = @accountId 
                           AND status = 'open'
                           ORDER BY open_time DESC";
            
            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@accountId", accountId);
            using MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                orders.Add(MapOrderFromReader(reader));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取账户 {accountId} 的活跃订单失败", ex);
            throw;
        }
        return orders;
    }

    public async Task<List<OrderModel>> GetCompletedOrdersAsync(long accountId)
    {
        var orders = new List<OrderModel>();
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            
            string query = @"SELECT * FROM simulation_orders 
                           WHERE account_id = @accountId 
                           AND status = 'closed'
                           ORDER BY close_time DESC";
            
            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@accountId", accountId);
            using MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                orders.Add(MapOrderFromReader(reader));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取账户 {accountId} 的已平仓订单失败", ex);
            throw;
        }
        return orders;
    }

    public async Task<string> CreateOrderAsync(OrderModel order, TakeProfitStrategy? takeProfitStrategy = null)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            
            // 实现创建订单的逻辑
            string orderId = Guid.NewGuid().ToString();
            // TODO: 添加实际的数据库操作
            
            return orderId;
        }
        catch (Exception ex)
        {
            _logger.LogError("创建订单失败", ex);
            throw;
        }
    }

    public async Task UpdateOrderAsync(OrderModel order)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            
            // TODO: 添加实际的数据库操作
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError("更新订单失败", ex);
            throw;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("测试数据库连接失败", ex);
            return false;
        }
    }

    private OrderModel MapOrderFromReader(MySqlDataReader reader)
    {
        return new OrderModel
        {
            Id = reader.GetInt64("id"),
            OrderId = reader.GetString("order_id"),
            AccountId = reader.GetInt64("account_id"),
            Contract = reader.GetString("contract"),
            Direction = reader.GetString("direction"),
            Quantity = reader.GetInt32("quantity"),
            EntryPrice = reader.GetDecimal("entry_price"),
            InitialStopLoss = reader.GetDecimal("initial_stop_loss"),
            CurrentStopLoss = reader.GetDecimal("current_stop_loss"),
            HighestPrice = reader.IsDBNull("highest_price") ? 0m : reader.GetDecimal("highest_price"),
            MaxFloatingProfit = reader.IsDBNull("max_floating_profit") ? 0m : reader.GetDecimal("max_floating_profit"),
            Leverage = reader.GetInt32("leverage"),
            Margin = reader.GetDecimal("margin"),
            TotalValue = reader.GetDecimal("total_value"),
            Status = reader.GetString("status"),
            OpenTime = reader.GetDateTime("open_time"),
            CloseTime = reader.IsDBNull("close_time") ? DateTime.MinValue : reader.GetDateTime("close_time"),
            ClosePrice = reader.IsDBNull("close_price") ? 0m : reader.GetDecimal("close_price"),
            RealizedProfit = reader.IsDBNull("realized_profit") ? 0m : reader.GetDecimal("realized_profit"),
            CloseType = reader.IsDBNull("close_type") ? "" : reader.GetString("close_type")
        };
    }
} 