using ScanToOrder.Domain.Entities.Base;

namespace ScanToOrder.Domain.Entities.Bank
{
    public partial class Banks : BaseEntity<Guid>
    {
        public string Name { get; set; } = null!;
        public string Code { get; set; } = null!;
        public int Bin { get; set; }
        public string ShortName { get; set; } = null!;
        public string LogoUrl { get; set; } = null!;
        public string IconUrl { get; set; } = null!;
        public string SwiftCode { get; set; } = null!;
        public int LookupSupported { get; set; }
    }
}
