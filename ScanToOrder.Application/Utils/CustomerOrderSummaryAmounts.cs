using System.Linq;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.Utils;

public static class CustomerOrderSummaryAmounts
{
    public static void ApplyOriginalAndFinalFromEntities(IReadOnlyList<Order> orders, List<CustomerOrderSummaryDto> dtos)
    {
        if (orders.Count != dtos.Count)
            return;

        var refundSumByOriginalId = orders
            .Where(o => o.typeOrder == TypeOrder.Refund && o.RefundOrderId.HasValue)
            .GroupBy(o => o.RefundOrderId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.FinalAmount));

        for (var i = 0; i < dtos.Count; i++)
        {
            var entity = orders[i];
            var dto = dtos[i];

            if (entity.typeOrder == TypeOrder.Refund)
            {
                dto.OriginalFinalAmount = dto.FinalAmount;
                continue;
            }

            refundSumByOriginalId.TryGetValue(entity.Id, out var refundedSum);

            if (!HasOrderLevelPromotion(entity))
            {
                var origLines = SumOriginalLineSubTotals(entity);
                if (origLines > 0m)
                {
                    var remainLines = SumRemainingLineSubTotals(entity);
                    dto.OriginalFinalAmount = (decimal)PricingUtils.RoundToNearestThousand(origLines);
                    dto.FinalAmount = (decimal)PricingUtils.RoundToNearestThousand(remainLines);
                    continue;
                }
            }

            dto.OriginalFinalAmount = refundedSum > 0
                ? dto.FinalAmount + refundedSum
                : dto.FinalAmount;
        }
    }

    private static bool HasOrderLevelPromotion(Order o) =>
        o.PromotionId.HasValue || o.PromotionDiscount != 0;

    private static decimal SumOriginalLineSubTotals(Order o)
    {
        if (o.OrderDetails == null || !o.OrderDetails.Any())
            return 0m;
        return o.OrderDetails.Sum(d => d.SubTotal);
    }

    private static decimal SumRemainingLineSubTotals(Order o)
    {
        if (o.OrderDetails == null || !o.OrderDetails.Any())
            return 0m;

        decimal s = 0m;
        foreach (var d in o.OrderDetails)
        {
            if (d.Quantity <= 0) continue;
            var rem = Math.Max(0, d.Quantity - d.RefundedQuantity);
            s += d.SubTotal * (decimal)rem / d.Quantity;
        }

        return s;
    }
}
