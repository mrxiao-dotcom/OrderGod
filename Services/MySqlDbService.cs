using DatabaseConfigDemo.Models;
using MySql.Data.MySqlClient;
using System.Data;
using Dapper;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;

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
        try
        {
            var sql = @"
                SELECT 
                    id,
                    order_id as OrderId,
                    account_id as AccountId,
                    contract as Contract,
                    direction as Direction,
                    quantity as Quantity,
                    entry_price as EntryPrice,
                    initial_stop_loss as InitialStopLoss,
                    current_stop_loss as CurrentStopLoss,
                    highest_price as HighestPrice,
                    max_floating_profit as MaxFloatingProfit,
                    leverage as Leverage,
                    margin as Margin,
                    total_value as TotalValue,
                    realized_profit as RealizedProfit,
                    real_profit as RealProfit,
                    status as Status,
                    open_time as OpenTime,
                    close_time as CloseTime,
                    close_price as ClosePrice,
                    close_type as CloseType
                FROM simulation_orders 
                WHERE account_id = @accountId 
                AND status = 'open'
                ORDER BY open_time DESC";

            using var connection = await CreateConnectionAsync();
            var orders = await connection.QueryAsync<OrderModel>(sql, new { accountId });
            return orders.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取账户 {accountId} 的活跃订单失败", ex);
            throw;
        }
    }

    public async Task<List<OrderModel>> GetCompletedOrdersAsync(long accountId)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT 
                    id,
                    order_id as OrderId,
                    account_id as AccountId,
                    contract as Contract,
                    direction as Direction,
                    quantity as Quantity,
                    entry_price as EntryPrice,
                    initial_stop_loss as InitialStopLoss,
                    current_stop_loss as CurrentStopLoss,
                    highest_price as HighestPrice,
                    max_floating_profit as MaxFloatingProfit,
                    leverage as Leverage,
                    margin as Margin,
                    total_value as TotalValue,
                    realized_profit as RealizedProfit,
                    status as Status,
                    open_time as OpenTime,
                    close_time as CloseTime,
                    close_price as ClosePrice,
                    close_type as CloseType
                FROM simulation_orders 
                WHERE account_id = @accountId 
                AND status = 'closed'
                ORDER BY close_time DESC";

            var orders = await connection.QueryAsync<OrderModel>(sql, new { accountId });
            return orders.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取账户 {accountId} 的已完成订单失败", ex);
            throw;
        }
    }

    public async Task<string> CreateOrderAsync(OrderModel order, TakeProfitStrategy? takeProfitStrategy = null)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"INSERT INTO simulation_orders 
                    (account_id, contract, direction, quantity, entry_price, 
                        initial_stop_loss, current_stop_loss, leverage, margin, 
                        total_value, status, open_time)
                    VALUES 
                    (@AccountId, @Contract, @Direction, @Quantity, @EntryPrice,
                        @InitialStopLoss, @CurrentStopLoss, @Leverage, @Margin,
                        @TotalValue, 'active', @OpenTime);
                    SELECT CAST(LAST_INSERT_ID() AS CHAR);";  // 修改这里，确保返回字符串

            var orderId = await connection.ExecuteScalarAsync<string>(sql, order);  // 修改这里，使用 string 类型
            return orderId ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError($"创建订单失败: {ex.Message}", ex);
            throw;
        }
    }

    public async Task UpdateOrderAsync(OrderModel order)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            
            string query = @"UPDATE simulation_orders 
                           SET current_stop_loss = @currentStopLoss,
                               highest_price = @highestPrice,
                               max_floating_profit = @maxFloatingProfit,
                               realized_profit = @realizedProfit,
                               status = @status,
                               close_time = @closeTime,
                               close_price = @closePrice,
                               close_type = @closeType
                           WHERE id = @id";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@id", order.Id);
            command.Parameters.AddWithValue("@currentStopLoss", order.CurrentStopLoss);
            command.Parameters.AddWithValue("@highestPrice", order.HighestPrice);
            command.Parameters.AddWithValue("@maxFloatingProfit", order.MaxFloatingProfit);
            command.Parameters.AddWithValue("@realizedProfit", order.RealizedProfit);
            command.Parameters.AddWithValue("@status", order.Status);
            command.Parameters.AddWithValue("@closeTime", order.CloseTime == DateTime.MinValue ? DBNull.Value : (object)order.CloseTime);
            command.Parameters.AddWithValue("@closePrice", order.ClosePrice ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@closeType", string.IsNullOrEmpty(order.CloseType) ? DBNull.Value : (object)order.CloseType);

            await command.ExecuteNonQueryAsync();
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

    public async Task<AccountData> GetAccountDataAsync(long accountId)
    {
        try
        {
            var sql = @"
                SELECT 
                    a.id,
                    a.name as AccountName,
                    COALESCE(arm.total_equity, 0) as TotalEquity,
                    COALESCE(a.init_value, 0) as InitialValue,
                    COALESCE((
                        SELECT SUM(total_value)
                        FROM simulation_orders
                        WHERE account_id = a.id AND status = 'open'
                    ), 0) as TotalValue,
                    COALESCE(arm.used_margin, 0) as UsedMargin,
                    COALESCE(arm.available_margin, 0) as AvailableMargin,
                    COALESCE(a.max_total_risk, 0) as TotalRisk,
                    COALESCE(arm.used_risk, 0) as UsedRisk,
                    COALESCE(arm.available_risk, 0) as AvailableRisk,
                    COALESCE(a.single_order_risk, 0) as MaxSingleRisk
                FROM accounts a
                LEFT JOIN account_risk_monitor arm ON arm.account_id = a.id
                WHERE a.id = @accountId";

            using var connection = await CreateConnectionAsync();
            var result = await connection.QueryFirstOrDefaultAsync<AccountData>(sql, new { accountId })
                ?? new AccountData();

            if (result == null)
            {
                _logger.Log($"未找到账户数据，账户ID：{accountId}");
                return new AccountData();
            }

            // 计算杠杆率 = 总市值/总权益
            result.LeverageRatio = result.TotalEquity > 0 ? 
                Math.Round(result.TotalValue / result.TotalEquity, 4) : 0;

            // 建议风险金默认等于单笔最大风险
            result.SuggestedRisk = result.MaxSingleRisk;

            _logger.Log($"获取账户数据成功：\n" +
                       $"账户={result.AccountName}\n" +
                       $"总权益={result.TotalEquity:F2}\n" +
                       $"初始值={result.InitialValue:F2}\n" +
                       $"总市值={result.TotalValue:F2}\n" +
                       $"杠杆率={result.LeverageRatio:F4}\n" +
                       $"已用保证金={result.UsedMargin:F2}\n" +
                       $"可用保证金={result.AvailableMargin:F2}\n" +
                       $"总风险金={result.TotalRisk:F2}\n" +
                       $"已用风险={result.UsedRisk:F2}\n" +
                       $"可用风险={result.AvailableRisk:F2}\n" +
                       $"单笔最大风险={result.MaxSingleRisk:F2}\n" +
                       $"建议风险金={result.SuggestedRisk:F2}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError("获取账户数据失败", ex);
            throw;
        }
    }

    public async Task<AccountRiskData> GetAccountRiskDataAsync(long accountId)
    {
        try
        {
            var sql = @"
                SELECT 
                    COALESCE(arm.used_risk, 0) as used_risk,
                    COALESCE(arm.available_risk, 0) as available_risk
                FROM accounts a
                LEFT JOIN account_risk_monitor arm ON arm.account_id = a.id
                WHERE a.id = @accountId";

            using var connection = await CreateConnectionAsync();
            var result = await connection.QueryFirstOrDefaultAsync<AccountRiskData>(sql, new { accountId });

            if (result == null)
            {
                _logger.Log($"未找到账户风险数据，账户ID：{accountId}");
                return new AccountRiskData();
            }

            _logger.Log($"获取账户风险数据成功：\n" +
                       $"已用风险={result.used_risk:F2}\n" +
                       $"可用风险={result.available_risk:F2}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError("获取账户风险数据失败", ex);
            throw;
        }
    }

    private OrderModel MapOrderFromReader(MySqlDataReader reader)
    {
        return new OrderModel
        {
            Id = reader.GetInt64("id"),
            OrderId = reader.IsDBNull("order_id") ? "" : reader.GetString("order_id"),
            AccountId = reader.GetInt64("account_id"),
            Contract = reader.IsDBNull("contract") ? "" : reader.GetString("contract"),
            Direction = reader.IsDBNull("direction") ? "" : reader.GetString("direction"),
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

    public async Task<IDbConnection> CreateConnectionAsync()
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task<int> ExecuteAsync(string sql, object param)
    {
        using var connection = await CreateConnectionAsync();
        return await connection.ExecuteAsync(sql, param);
    }
} 