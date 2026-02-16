using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScanToOrder.Domain.Entities.Notifications
{
    [Table("Notification")]
    public class Notification : BaseEntity<int>
    {
        [Key]
        public int NotificationId { get; set; }

        [Required]
        public string NotifyTitle { get; set; } = string.Empty;

        public string NotifySub { get; set; } = string.Empty;

        public NotifyStatus NotifyStatus { get; set; } = NotifyStatus.Sending;

        public string? SystemBlogUrl { get; set; }

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<NotifyTenant> NotifyTenants { get; set; } = new List<NotifyTenant>();

        [NotMapped]
        public new bool IsDeleted { get; set; }
        [NotMapped]
        public new DateTime CreatedAt { get; set; }
        [NotMapped]
        public int Id { get; set; }
        [NotMapped]
        public new DateTime? UpdatedAt { get; set; }
    }
}