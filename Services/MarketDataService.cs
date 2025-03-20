using System.Collections.Concurrent;
using Io.Gate.GateApi.Api;
using Io.Gate.GateApi.Client;
using Io.Gate.GateApi.Model;
using DatabaseConfigDemo.Models;  // 添加命名空间引用

namespace DatabaseConfigDemo.Services;

public class MarketDataService
{
    private readonly ILogger _logger;
    private readonly Configuration _gateConfig;
    private readonly ConcurrentDictionary<string, FuturesTicker> _tickerCache;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly FuturesApi _futuresApi;
    private readonly HashSet<string> _subscribedSymbols;
    
    public event EventHandler<Dictionary<string, FuturesTicker>>? TickerUpdated;

    public MarketDataService(string apiEndpoint, ILogger logger)
    {
        _logger = logger;
        
        // 从配置文件加载 API 配置
        var apiConfig = ApiConfig.Load();
        _logger.Log($"API配置加载完成，Endpoint: {apiEndpoint}");

        _gateConfig = new Configuration
        {
            BasePath = apiEndpoint,
            ApiV4Key = apiConfig.GateApiKey,
            ApiV4Secret = apiConfig.GateApiSecret
        };

        _futuresApi = new FuturesApi(_gateConfig);
        _tickerCache = new ConcurrentDictionary<string, FuturesTicker>();
        _cancellationTokenSource = new CancellationTokenSource();
        
        // 修改为正确的永续合约格式
        _subscribedSymbols = new HashSet<string>
        {
            "BTC_USDT",
            "ETH_USDT",
            "SOL_USDT", 
            "XRP_USDT"
        };

        _logger.Log($"已订阅的合约: {string.Join(", ", _subscribedSymbols)}");
    }

    private async Task UpdateTickersLoop()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                _logger.Log("开始批量获取永续合约行情数据...");

                // 使用 List<FuturesTicker> 存储所有获取到的行情数据
                var allTickers = new List<FuturesTicker>();
                
                // 逐个获取每个合约的行情数据
                foreach (var symbol in _subscribedSymbols)
                {
                    try
                    {
                        _logger.Log($"正在获取 {symbol} 的行情数据...");
                        var tickers = await Task.Run(() => _futuresApi.ListFuturesTickers("usdt", symbol));
                        if (tickers != null && tickers.Any())
                        {
                            allTickers.AddRange(tickers);
                            _logger.Log($"成功获取 {symbol} 的行情数据");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"获取 {symbol} 行情数据失败", ex);
                        continue;  // 继续获取下一个合约的数据
                    }
                }

                // 处理获取到的行情数据
                var updatedTickers = new Dictionary<string, FuturesTicker>();
                foreach (var ticker in allTickers)
                {
                    if (ticker != null && !string.IsNullOrEmpty(ticker.Contract))
                    {
                        // 转换为显示格式
                        var displaySymbol = ticker.Contract.Replace("_", "/");
                        _tickerCache[displaySymbol] = ticker;
                        updatedTickers[displaySymbol] = ticker;
                        
                        _logger.Log($"更新行情: {displaySymbol} - 最新价: {ticker.Last}, 涨跌幅: {ticker.ChangePercentage}%");
                    }
                }

                // 触发更新事件
                if (updatedTickers.Count > 0)
                {
                    _logger.Log($"成功更新 {updatedTickers.Count} 个合约的行情数据");
                    TickerUpdated?.Invoke(this, updatedTickers);
                }
                else
                {
                    _logger.Log("没有获取到任何行情数据");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("批量获取永续合约行情数据失败", ex);
            }

            try
            {
                await Task.Delay(5000, _cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                _logger.Log("行情更新服务已停止");
                break;
            }
        }
    }

    public void Start()
    {
        _logger.Log("启动行情更新服务...");
        Task.Run(UpdateTickersLoop, _cancellationTokenSource.Token);
    }

    public void Stop()
    {
        _logger.Log("正在停止行情更新服务...");
        _cancellationTokenSource.Cancel();
    }

    public FuturesTicker? GetTicker(string symbol)
    {
        _tickerCache.TryGetValue(symbol, out var ticker);
        return ticker;
    }
} 