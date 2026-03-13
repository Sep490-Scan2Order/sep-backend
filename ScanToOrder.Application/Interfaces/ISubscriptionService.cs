using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScanToOrder.Application.DTOs.Payment;
using ScanToOrder.Application.DTOs.Plan;

namespace ScanToOrder.Application.Interfaces
{
    public interface ISubscriptionService
    {
        Task<CheckoutPreviewResponse> CalculatePreviewAsync(PlanCheckoutRequest request, Guid currentTenantId);
        Task<string> CreatePaymentAsync(PlanCheckoutRequest request, Guid currentTenantId);
        Task ProcessPaymentSuccessAsync(long transactionCode);
        Task MarkPaymentFailedAsync(long transactionCode);
        Task MarkPaymentCanceledAsync(long transactionCode, Guid currentTenantId);
        Task<PaymentStatusResponse> GetPaymentStatusAsync(long transactionCode, Guid currentTenantId);
        Task<List<RestaurantSubscriptionDto>> GetSubscriptionsByTenantAsync(Guid tenantId);
    }
}
