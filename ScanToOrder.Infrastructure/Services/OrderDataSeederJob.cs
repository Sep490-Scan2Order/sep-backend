using Hangfire;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ScanToOrder.Infrastructure.Services
{
    public class OrderDataSeederJob
    {
        private readonly IUnitOfWork _unitOfWork;

        public OrderDataSeederJob(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [AutomaticRetry(Attempts = 0)]
        public async Task ExecuteAsync(int restaurantId, int numberOfOrders = 1000)
        {
            var branchConfigs = await _unitOfWork.BranchDishConfigs.GetSellingDishesByRestaurantIdAsync(restaurantId);
            if (branchConfigs == null || !branchConfigs.Any())
                return;

            var dishIds = branchConfigs.Select(x => x.DishId).ToList();
            if (dishIds.Count < 2) return; // Need at least 2 dishes to form pairs

            var staffs = await _unitOfWork.Staffs.GetAllAsync(
                x => x.RestaurantId == restaurantId && !x.IsDeleted && x.Account.Role == Role.Cashier,
                x => x.Account
            );
            var staffIds = staffs.Select(x => x.Id).ToList();

            var orders = new List<Order>();
            var random = new Random();

            var orderTypes = new[] { "SePay", "Cash", "Bank Transfer" };
            var notesList = new[] { "Ít cay", "Không hành", "Thêm nhiều sốt", "Không lấy muỗng đũa nhựa", "", "", "", "Lấy thêm tương ớt", "Giao nhanh giúp mình" };

            // Find the maximum OrderCode currently in DB to continue sequentially
            int maxOrderCode = await _unitOfWork.Orders.GetQueryable().AnyAsync(o => o.RestaurantId == restaurantId)
                               ? await _unitOfWork.Orders.GetQueryable().Where(o => o.RestaurantId == restaurantId).MaxAsync(x => x.OrderCode)
                               : 0;

            for (int i = 0; i < numberOfOrders; i++)
            {
                var orderId = Guid.NewGuid();
                var baseDate = DateTime.UtcNow.AddDays(-random.Next(1, 45)).Date;
                var createdAt = baseDate.AddHours(random.Next(8, 22)).AddMinutes(random.Next(0, 60));
                bool isPreOrder = random.NextDouble() < 0.15; // 15% are pre-orders

                var order = new Order
                {
                    Id = orderId,
                    RestaurantId = restaurantId,
                    NumberPhone = "09" + random.Next(10000000, 99999999).ToString("D8"),
                    OrderCode = maxOrderCode + i + 1,
                    QrCodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=150x150&data={orderId}",
                    IsPreOrder = isPreOrder,
                    RequestedPickupAt = isPreOrder ? createdAt.AddHours(random.Next(1, 24)) : null,
                    ConfirmedPickupAt = isPreOrder ? createdAt.AddHours(random.Next(1, 24)).AddMinutes(random.Next(5, 30)) : null,
                    Note = notesList[random.Next(notesList.Length)],
                    Status = OrderStatus.Served, // Make them successful orders
                    Type = orderTypes[random.Next(orderTypes.Length)],
                    typeOrder = TypeOrder.Regular,
                    PaymentProofUrl = random.NextDouble() > 0.5 ? "https://example.com/payment-proofs/sample-proof.jpg" : null,
                    ResponsibleStaffId = staffIds.Any() ? staffIds[random.Next(staffIds.Count)] : null,
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt.AddMinutes(random.Next(15, 60)), // Finished ~15-60 mins later
                    TotalAmount = 0,
                    FinalAmount = 0,
                    IsScanned = true,
                    PromotionDiscount = 0,
                    OrderDetails = new List<OrderDetail>()
                };

                // Create somewhat predictable patterns to help Matrix Factorization learn better
                double rValue = random.NextDouble();
                var selectedDishIds = new List<int>();

                if (rValue < 0.4 && dishIds.Count >= 2)
                {
                    // Combo 1 (40%): Dish 0 + Dish 1
                    selectedDishIds.Add(dishIds[0]);
                    selectedDishIds.Add(dishIds[1]);
                }
                else if (rValue < 0.7 && dishIds.Count >= 3)
                {
                    // Combo 2 (30%): Dish 0 + Dish 2
                    selectedDishIds.Add(dishIds[0]);
                    selectedDishIds.Add(dishIds[2]);
                }
                else if (rValue < 0.9 && dishIds.Count >= 4)
                {
                    // Combo 3 (20%): Dish 2 + Dish 3
                    selectedDishIds.Add(dishIds[2]);
                    selectedDishIds.Add(dishIds[3]);
                }
                else
                {
                    // Random 2 to 4 dishes (10% noise)
                    int count = random.Next(2, Math.Min(5, dishIds.Count + 1));
                    selectedDishIds.AddRange(dishIds.OrderBy(x => random.Next()).Take(count));
                }

                decimal total = 0;
                foreach (var dId in selectedDishIds.Distinct())
                {
                    var config = branchConfigs.First(x => x.DishId == dId);
                    var price = config.Price;
                    int qty = random.Next(1, 3); // 1 or 2 items

                    order.OrderDetails.Add(new OrderDetail
                    {
                        OrderId = orderId,
                        DishId = dId,
                        Quantity = qty,
                        OriginalPrice = price,
                        DiscountedPrice = price, // Simple seed, no item-level discount simulated here
                        SubTotal = price * qty
                    });

                    total += price * qty;
                }

                order.TotalAmount = total;
                order.PromotionDiscount = 0;
                order.FinalAmount = total;
                
                orders.Add(order);
            }

            await _unitOfWork.Orders.AddRangeAsync(orders);

            // Group orders by date to create shifts
            var ordersByDate = orders.GroupBy(o => o.CreatedAt.Date).OrderBy(g => g.Key).ToList();
            var shifts = new List<ScanToOrder.Domain.Entities.Shifts.Shift>();
            var transactions = new List<Transaction>();

            int transactionCounter = 1;

            foreach (var dateGroup in ordersByDate)
            {
                var shiftDate = dateGroup.Key;

                // Create a shift for this day
                var staffIdToUse = staffIds.Any() ? staffIds[random.Next(staffIds.Count)] : Guid.Empty;
                if (staffIdToUse != Guid.Empty)
                {
                    var shift = new ScanToOrder.Domain.Entities.Shifts.Shift
                    {
                        RestaurantId = restaurantId,
                        StaffId = staffIdToUse,
                        StartDate = shiftDate.AddHours(8), // Start at 8 AM
                        EndDate = shiftDate.AddHours(22),  // End at 10 PM
                        OpeningCashAmount = 1000000,       // 1,000,000 VND 
                        Note = "Seeded shift",
                        Status = ShiftStatus.Closed,       // Shifts in the past are closed
                        CreatedAt = shiftDate.AddHours(8)
                    };
                    
                    shifts.Add(shift);

                    // Assign transactions for this day's orders
                    foreach (var order in dateGroup)
                    {
                        // Determine payment method based on Order Type
                        var pm = order.Type == "Cash" ? PaymentMethod.Cash : PaymentMethod.BankTransfer;

                        var trans = new Transaction
                        {
                            OrderId = order.Id,
                            Status = OrderTransactionStatus.Success,
                            TotalAmount = order.FinalAmount,
                            TransactionCode = $"TX-{shiftDate:yyyyMMdd}-{transactionCounter++:D4}",
                            PaymentMethod = pm,
                            Shift = shift, // Direct object reference prevents N+1 query need
                            TransactionType = ScanToOrder.Domain.Enums.TransactionType.Payment,
                            CreatedAt = order.CreatedAt.AddMinutes(5) // Payment done 5 mins after order
                        };
                        transactions.Add(trans);
                    }
                }
            }

            if (shifts.Any())
            {
                await _unitOfWork.Shifts.AddRangeAsync(shifts);
            }

            if (transactions.Any())
            {
                await _unitOfWork.Transactions.AddRangeAsync(transactions);
            }

            // Save all newly added transactions and shifts in ONE query batch
            await _unitOfWork.SaveAsync();
        }
    }
}
