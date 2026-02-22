using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScanToOrder.Domain.Entities.Blogs
{
    [Table("SystemBlog")] 
    public class SystemBlog : BaseEntity<int>
    {
        public int SystemBlogId { get; set; }

        public string Content { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ColorTitle { get; set; } = string.Empty;

        public new DateOnly CreatedAt { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public new DateOnly UpdatedAt { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public int TotalViews { get; set; }

        [Column(TypeName = "jsonb")]
        public string ImageUrl { get; set; } = "[]";

        public BlogType BlogType { get; set; }
        [NotMapped]
        public int Id { get; set; }
        [NotMapped]
        public new bool IsDeleted { get; set; }
    }
}