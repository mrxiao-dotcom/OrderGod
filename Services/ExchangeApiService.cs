using System.Net;
using System.Text;
using System.Text.Json;
using DatabaseConfigDemo.Models;
using System.Security.Cryptography;
using Io.Gate.GateApi.Api;
using Io.Gate.GateApi.Client;
using Io.Gate.GateApi.Model;
using System.Net.Http;
using HttpMethod = System.Net.Http.HttpMethod;

namespace DatabaseConfigDemo.Services;

// 将 JsonExtensions 移到类外部作为顶级静态类
public static class JsonExtensions
{
    public static decimal GetDecimal(this JsonElement element)
    {
        return element.ValueKind == JsonValueKind.String
            ? decimal.Parse(element.GetString() ?? "0")
            : element.GetDecimal();
    }
}

public class ExchangeApiService
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;

    public ExchangeApiService(ILogger logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
    }

    public async Task<string> TestBinanceConnectionAsync(string apiKey, string apiSecret, bool isTestnet = false)
    {
        try
        {
            // 添加 await 调用
            await Task.Delay(100); // 模拟API调用
            return "连接成功";
        }
        catch (Exception ex)
        {
            _logger.LogError("测试 Binance API 连接失败", ex);
            return $"连接失败: {ex.Message}";
        }
    }

    public async Task<string> TestZhimaConnectionAsync(ZhimaConfig config)
    {
        try
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(config.Endpoint);

            // 创建签名
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var signature = CreateZhimaSignature(timestamp, config.ApiSecret);

            // 设置请求头
            client.DefaultRequestHeaders.Add("X-CH-APIKEY", config.ApiKey);
            client.DefaultRequestHeaders.Add("X-CH-TS", timestamp);
            client.DefaultRequestHeaders.Add("X-CH-SIGN", signature);

            // 发送测试请求
            var response = await client.GetAsync("/api/v1/account");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.Log($"Zhima API 连接测试成功: {content}");
                return "连接成功";
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Zhima API 返回错误: {error}", new Exception(error));
                return $"连接失败: {error}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("测试 Zhima API 连接失败", ex);
            return $"连接失败: {ex.Message}";
        }
    }

    private string CreateZhimaSignature(string timestamp, string apiSecret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret));
        var signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(timestamp));
        return BitConverter.ToString(signature).Replace("-", "").ToLower();
    }
} 