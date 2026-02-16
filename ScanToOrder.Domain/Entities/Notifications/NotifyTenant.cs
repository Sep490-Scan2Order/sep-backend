using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.User;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScanToOrder.Domain.Entities.Notifications
{

    [Table("NotifyTenant")]
    public class NotifyTenant : BaseEntity<int>
    {
        [Key]
        public int NotifyTenantId { get; set; }

        public int NotificationId { get; set; }

        [ForeignKey("NotificationId")]
        public virtual Notification Notification { get; set; } = null!;

        public Guid TenantId { get; set; }

        [ForeignKey("TenantId")]
        public virtual Tenant Tenant { get; set; } = null!;

        [NotMapped]
        public bool IsDeleted { get; set; }
        [NotMapped]
        public DateTime CreatedAt { get; set; }
        [NotMapped]
        public int Id { get; set; }
        [NotMapped]
        public DateTime? UpdatedAt { get; set; }

    }
}
