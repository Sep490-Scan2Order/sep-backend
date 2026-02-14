using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.User
{
    public class RegisterTenantRequest
    {
        public required string Name { get; set; }
        public required string Phone { get; set; }
        public required string TaxNumber { get; set; }
        public required string BankName { get; set; }
        public required string CardNumber { get; set; }

        public required string Email { get; set; }
        public required string Password { get; set; }
    }
}
