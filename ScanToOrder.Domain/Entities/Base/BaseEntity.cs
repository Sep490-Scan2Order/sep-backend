namespace ScanToOrder.Domain.Entities.Base
{
    public class BaseEntity<TKey>
    {
        public TKey Id { get; set; } 
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; } 
        public bool IsDeleted { get; set; } = false;
    }
}
