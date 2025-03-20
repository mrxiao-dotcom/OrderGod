using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Io.Gate.GateApi.Api;
using Io.Gate.GateApi.Client;
using Io.Gate.GateApi.Model;

public class MarketDataService
{
    private readonly string _baseUrl;
    private readonly Microsoft.Extensions.Logging.ILogger _logger;
    private readonly FuturesApi _futuresApi;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly HashSet<string> _subscribedSymbols;

    public event EventHandler<Dictionary<string, FuturesTicker>>? TickerUpdated;

    public MarketDataService(string baseUrl, Microsoft.Extensions.Logging.ILogger logger)
    {
        _baseUrl = baseUrl;
        _logger = logger;
        
        // 初始化 API 配置
        var config = new Configuration
        {
            BasePath = _baseUrl,
            Timeout = 10000 // 10秒
        };
        
        _futuresApi = new FuturesApi(config);
        
        _subscribedSymbols = new HashSet<string>
        {
            "BTC_USDT",
            "ETH_USDT",
            "SOL_USDT",
            "XRP_USDT"
        };
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
                        _logger.LogInformation("正在获取 {Symbol} 的行情数据...", symbol);
                        
                        var settle = "usdt";
                        var contract = symbol;
                        
                        var response = await _futuresApi.ListFuturesTickersAsync(settle, contract);
                        if (response != null && response.Count > 0)
                        {
                            var ticker = response[0];
                            var displaySymbol = symbol.Replace("_", "/");
                            tickers[displaySymbol] = ticker;
                            _logger.LogInformation("成功获取 {Symbol} 的行情数据", symbol);
                        }
                        
                        await Task.Delay(200);
                    }
                    catch (ApiException ex)
                    {
                        _logger.LogError(ex, "获取 {Symbol} 行情数据失败", symbol);
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
} 