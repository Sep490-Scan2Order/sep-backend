using System.ComponentModel.DataAnnotations;

namespace ScanToOrder.Application.DTOs.User
{
    public class UpdateTenantDtoRequest
    {
        [Required]
        public required string TaxNumber { get; set; }
        [Required]
        public required string CardNumber { get; set; }
        public Guid BankId { get; set; }
    }
}
