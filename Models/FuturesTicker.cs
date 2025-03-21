using System;

namespace DatabaseConfigDemo.Models
{
    public class FuturesTicker
    {
        public string Symbol { get; set; } = string.Empty;
        public string Contract { get; set; } = string.Empty;
        public decimal LastPrice { get; set; }
        public decimal ChangePercentage { get; set; }
        public decimal Volume24H { get; set; }
        // ... 其他属性 ...
    }
} 