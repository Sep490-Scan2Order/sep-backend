using ScanToOrder.Application.DTOs.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Interfaces
{
    public interface ITenantService
    {
        Task<TenantDto> RegisterTenantAsync(RegisterTenantRequest request);
    }
}
