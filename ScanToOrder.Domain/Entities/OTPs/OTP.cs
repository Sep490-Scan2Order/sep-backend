using ScanToOrder.Domain.Entities.Base;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScanToOrder.Domain.Entities.OTPs
{
    [Table("OTP")]
    public class OTP : BaseEntity<int>
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string OtpCode { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public bool IsUsed { get; set; }
        public new DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public new DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiredAt { get; set; }
        [NotMapped]
        public bool IsDeleted { get; set; }
    }
}
