namespace ScanToOrder.Application.DTOs.External
{
    public class TaxValidationResponse
    {
        public string status { get; set; }
        public string taxCode { get; set; }
        public string fullName { get; set; }
        public string taxStatus { get; set; }
        public Dictionary<string, object> raw { get; set; }
    }

    public class TaxLookupResult
    {
        public bool IsValid { get; set; }
        public string? TaxCode { get; set; }
        public string FullName { get; set; }
        public string Status { get; set; }
        public string Representative { get; set; } // Người đại diện
        public string ManagedBy { get; set; }      // Cơ quan quản lý
        public bool IsPersonal { get; set; }       // true nếu là cá nhân, false nếu doanh nghiệp
    }
}
