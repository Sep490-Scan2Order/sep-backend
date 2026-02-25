using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Interfaces
{
    public interface ISubscriptionService
    {
        Task<string> SubscribePlanAsync(Guid tenantId, int planId);
    }
}
