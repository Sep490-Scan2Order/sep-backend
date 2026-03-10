using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScanToOrder.Application.DTOs.Plan;

namespace ScanToOrder.Application.Interfaces
{
    public interface ISubscriptionService
    {
        Task<CheckoutPreviewResponse> CalculatePreviewAsync(PlanCheckoutRequest request, Guid currentTenantId);
        Task<string> CreatePaymentAsync(PlanCheckoutRequest request, Guid currentTenantId);
        Task ProcessPaymentSuccessAsync(long transactionCode);
    }
}
