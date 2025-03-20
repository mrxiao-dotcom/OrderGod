using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Io.Gate.GateApi.Api;
using Io.Gate.GateApi.Client;
using Io.Gate.GateApi.Model;
using Newtonsoft.Json;

public class MarketDataService
{
    private readonly string _baseUrl;
    private readonly ILogger _logger;
    private readonly FuturesApi _futuresApi;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly HashSet<string> _subscribedSymbols;

    public event EventHandler<Dictionary<string, FuturesTicker>>? TickerUpdated;

    public MarketDataService(string baseUrl, ILogger logger)
    {
        _baseUrl = baseUrl;
        _logger = logger;
        
        var config = new Configuration
        {
            BasePath = _baseUrl,
            Timeout = 10000
        };
        
        _futuresApi = new FuturesApi(config);
        _subscribedSymbols = new HashSet<string>();
    }

    public void SubscribeSymbols(IEnumerable<string> symbols)
    {
        foreach (var symbol in symbols)
        {
            var formattedSymbol = symbol.Replace("/", "_");
            _subscribedSymbols.Add(formattedSymbol);
        }
        _logger.LogInformation($"已订阅合约: {string.Join(", ", _subscribedSymbols)}");
    }

    public void Start()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        Task.Run(UpdateTickersLoop, _cancellationTokenSource.Token);
    }

    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
    }

    private async Task UpdateTickersLoop()
    {
        while (_cancellationTokenSource?.Token.IsCancellationRequested == false)
        {
            try
            {
                var tickers = new Dictionary<string, FuturesTicker>();
                
                foreach (var symbol in _subscribedSymbols)
                {
                    try
                    {
                        _logger.LogInformation($"正在获取 {symbol} 的行情数据...");
                        
                        var settle = "usdt";
                        var response = await _futuresApi.ListFuturesTickersAsync(settle, symbol);
                        
                        if (response != null && response.Count > 0)
                        {
                            var ticker = response[0];
                            var displaySymbol = symbol.Replace("_", "/");
                            tickers[displaySymbol] = ticker;
                            _logger.LogInformation($"成功获取 {symbol} 的行情数据");
                        }
                        
                        await Task.Delay(200);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"获取 {symbol} 行情数据失败");
                        continue;
                    }
                }

                if (tickers.Count > 0)
                {
                    TickerUpdated?.Invoke(this, tickers);
                }

                await Task.Delay(5000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新行情数据时发生错误");
                await Task.Delay(10000);
            }
        }
    }

    public async Task<decimal> GetContractSizeAsync(string symbol)
    {
        try
        {
            // 从 symbol (如 "BTC_USDT" 或 "BTC/USDT") 中提取合约名称
            string contract = symbol;  // 保持原始格式
            string settle = "usdt";  // 结算货币

            // 获取所有合约信息
            var contracts = await _futuresApi.ListFuturesContractsAsync(settle);
            
            // 打印调试信息
            _logger.LogInformation($"查找合约：{contract}");
            _logger.LogInformation($"可用合约：{string.Join(", ", contracts.Select(c => c.Name))}");
            
            // 查找匹配的合约，使用原始格式进行匹配
            var targetContract = contracts.FirstOrDefault(c => 
                c.Name.Equals(contract, StringComparison.OrdinalIgnoreCase) || 
                c.Name.Equals(contract.Replace("/", "_"), StringComparison.OrdinalIgnoreCase));
            
            if (targetContract != null)
            {
                _logger.LogInformation($"找到合约：{targetContract.Name}");
                _logger.LogInformation($"合约信息：{JsonConvert.SerializeObject(targetContract)}");
                
                if (decimal.TryParse(targetContract.QuantoMultiplier, out decimal multiplier))
                {
                    _logger.LogInformation($"获取合约 {symbol} 面值成功：{multiplier}");
                    return multiplier;
                }
                else
                {
                    throw new Exception($"无法解析合约 {symbol} 的面值");
                }
            }
            
            // 如果没有找到合约，打印更多调试信息
            _logger.LogError($"未找到合约 {symbol}，当前所有可用合约：");
            foreach (var c in contracts)
            {
                _logger.LogError($"合约名称：{c.Name}，类型：{c.Type}");
            }
            
            throw new Exception($"未找到合约 {symbol} 的信息");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"获取合约 {symbol} 面值失败");
            throw new Exception($"获取合约 {symbol} 面值失败: {ex.Message}", ex);
        }
    }
} 