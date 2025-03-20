namespace DatabaseConfigDemo.Models
{
    public class AccountData
    {
        public decimal TotalEquity { get; set; }
        public decimal InitialValue { get; set; }
        public decimal TotalValue { get; set; }
        public decimal LeverageRatio { get; set; }
        public decimal UsedMargin { get; set; }
        public decimal AvailableMargin { get; set; }
    }

    public class AccountRiskData
    {
        public decimal TotalRisk { get; set; }
        public decimal AvailableRisk { get; set; }
        public decimal MaxSingleRisk { get; set; }
        public decimal SuggestedRisk { get; set; }
    }

    public class AccountItem
    {
        public string AccountId { get; set; } = "";
        public string AccountName { get; set; } = "";

        public override string ToString()
        {
            return AccountName;
        }
    }
} 