using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.DTOs.User
{
    public class UpdateStaffRequest
    {
        public required string Name { get; set; }
        public required string Phone { get; set; }
        public required bool IsActive { get; set; }
        public required Role Role { get; set; }
    }
}
