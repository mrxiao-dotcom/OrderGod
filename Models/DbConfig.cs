namespace DatabaseConfigDemo.Models
{
    public class DbConfig
    {
        public string Server { get; set; } = "";
        public string Database { get; set; } = "";
        public string UserId { get; set; } = "";
        public string Password { get; set; } = "";
        public string ApiBaseUrl { get; set; } = "https://api.gateio.ws/api/v4";  // 添加 API 基础 URL

        public string GetConnectionString()
        {
            return $"Server={Server};Database={Database};Uid={UserId};Pwd={Password};";
        }

        public static DbConfig Load()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dbconfig.json");
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    var config = System.Text.Json.JsonSerializer.Deserialize<DbConfig>(json);
                    return config ?? new DbConfig();
                }
            }
            catch
            {
                // 如果加载失败，返回默认配置
            }
            return new DbConfig();
        }
    }
}