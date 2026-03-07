namespace ScanToOrder.Domain.Entities.Configuration
{
    public class Configurations
    {
        public int CommissionRate { get; set; }
        public int ExpiredDuration { get; set; }
        public int RedeemRate { get; set; }
        public DateOnly LastUpdated { get; set; }
    }
}
