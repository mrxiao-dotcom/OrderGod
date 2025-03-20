using System.Text.Json;

namespace DatabaseConfigDemo.Models;

public class ApiConfig
{
    // Gate.io API 配置
    public string GateApiKey { get; set; } = string.Empty;
    public string GateApiSecret { get; set; } = string.Empty;

    // Zhima API 配置
    public ZhimaConfig Zhima { get; set; } = new ZhimaConfig();

    private static string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "apiconfig.json");

    public static ApiConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<ApiConfig>(json);
                return config ?? new ApiConfig();
            }
        }
        catch
        {
            // 如果读取失败，返回空配置
        }
        return new ApiConfig();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}

public class ZhimaConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
} 