using System;
using System.IO;
using System.Text.Json;

namespace DatabaseConfigDemo
{
    public class DbConfig
    {
        public string Server { get; set; } = "";
        public string Database { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";

        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dbconfig.json");

        public static DbConfig Load()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string jsonString = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<DbConfig>(jsonString) ?? new DbConfig();
                }
                catch
                {
                    return new DbConfig();
                }
            }
            return new DbConfig();
        }

        public void Save()
        {
            string jsonString = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, jsonString);
        }

        public string GetConnectionString()
        {
            return $"Server={Server};Database={Database};Uid={Username};Pwd={Password}";
        }
    }
} 