namespace Transfers.API.Model
{
    public class Limit
    {
        public string CIF { get; set; }
        public double DailyLimit { get; set; }
        public double MonthlyLimit { get; set; }
        public double DailyUsedLimit { get; set; }
        public double MonthlyUsedLimit { get; set; }
    }
}
