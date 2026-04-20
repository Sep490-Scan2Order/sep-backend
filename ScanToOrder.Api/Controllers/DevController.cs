using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Infrastructure.Services;
using ScanToOrder.Application.Wrapper;
using System.Threading.Tasks;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Enums;
using System;
using System.Collections.Generic;

namespace ScanToOrder.Api.Controllers
{
    [ApiController]
    [Route("api/dev")]
    [AllowAnonymous] // Only for development/testing
    public class DevController : BaseController
    {
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IUnitOfWork _unitOfWork;

        public DevController(IBackgroundJobClient backgroundJobClient, IUnitOfWork unitOfWork)
        {
            _backgroundJobClient = backgroundJobClient;
            _unitOfWork = unitOfWork;
        }

        [HttpPost("seed-orders/{restaurantId}")]
        public IActionResult SeedOrders(int restaurantId, [FromQuery] int numberOfOrders = 1000)
        {
            _backgroundJobClient.Enqueue<OrderDataSeederJob>(x => x.ExecuteAsync(restaurantId, numberOfOrders));
            
            return Ok(ApiResponse<string>.Success($"Đã đưa tiến trình tạo {numberOfOrders} đơn hàng mẫu cho nhà hàng {restaurantId} vào hàng đợi Hangfire."));
        }

        [HttpPost("train-ai")]
        public IActionResult TrainAI()
        {
            _backgroundJobClient.Enqueue<AITrainingJob>(x => x.ExecuteAsync());
            
            return Ok(ApiResponse<string>.Success("Đã đưa tiến trình huấn luyện AI (Matrix Factorization) vào hàng đợi Hangfire."));
        }

        [HttpDelete("clear-seeded-data/{restaurantId}")]
        public IActionResult ClearSeededData(int restaurantId)
        {
            _backgroundJobClient.Enqueue<OrderDataClearanceJob>(x => x.ExecuteAsync(restaurantId));
            
            return Ok(ApiResponse<string>.Success($"Đã đưa tiến trình dọn dẹp toàn bộ dữ liệu mẫu (Seeder) của nhà hàng {restaurantId} vào hàng đợi Hangfire."));
        }

        [HttpPost("seed-menu/{tenantId:guid}")]
        public async Task<ActionResult<ApiResponse<string>>> SeedMenuData(Guid tenantId)
        {
            await using var tx = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 1. Seed Categories
                var categoriesData = new[]
                {
                    new { OldId = 1, Name = "Gà Rán Truyền Thống", IsActive = true, IsDeleted = false },
                    new { OldId = 2, Name = "Gà Không Xương & Popcorn", IsActive = true, IsDeleted = false },
                    new { OldId = 3, Name = "Burger & Đồ Ăn Kèm", IsActive = true, IsDeleted = false },
                    new { OldId = 4, Name = "Thức Uống (Nước ngọt & Trà)", IsActive = true, IsDeleted = false },
                    new { OldId = 5, Name = "Tráng Miệng", IsActive = true, IsDeleted = false },
                    new { OldId = 6, Name = "Combo Gia Đình & Cặp Đôi", IsActive = true, IsDeleted = false }
                };

                var categoryMap = new Dictionary<int, int>();

                foreach (var item in categoriesData)
                {
                    var cat = new Category
                    {
                        TenantId = tenantId,
                        CategoryName = item.Name,
                        IsActive = item.IsActive,
                        IsDeleted = item.IsDeleted
                    };
                    await _unitOfWork.Categories.AddAsync(cat);
                    await _unitOfWork.SaveAsync(); 
                    categoryMap[item.OldId] = cat.Id;
                }

                // 2. Seed Dishes
                var dishesData = new[]
                {
                    new { OldId = 301, CatId = 1, Name = "Đùi gà rán giòn cay", Price = 38000m, Type = DishType.Single, IsAvail = true, Desc = "Đùi gà tẩm bột giòn rụm, vị cay nồng" },
                    new { OldId = 302, CatId = 1, Name = "Cánh gà sốt mật ong (3 miếng)", Price = 48000m, Type = DishType.Single, IsAvail = true, Desc = "Cánh gà phủ sốt mật ong ngọt thanh" },
                    new { OldId = 303, CatId = 2, Name = "Gà viên Popcorn", Price = 35000m, Type = DishType.Single, IsAvail = true, Desc = "Gà viên chiên giòn vừa miệng" },
                    new { OldId = 304, CatId = 2, Name = "Gà không xương sốt phô mai", Price = 55000m, Type = DishType.Single, IsAvail = true, Desc = "Phủ xốt phô mai béo ngậy" },
                    new { OldId = 305, CatId = 3, Name = "Burger Gà Zinger", Price = 45000m, Type = DishType.Single, IsAvail = true, Desc = "Burger ức gà chiên giòn, xà lách, sốt mayo" },
                    new { OldId = 306, CatId = 3, Name = "Khoai tây chiên lắc phô mai", Price = 25000m, Type = DishType.Single, IsAvail = true, Desc = "Khoai tây chiên giòn lắc bột phô mai" },
                    new { OldId = 307, CatId = 3, Name = "Salad cá ngừ", Price = 30000m, Type = DishType.Single, IsAvail = true, Desc = "Xà lách, cà chua, cá ngừ ngâm dầu" },
                    new { OldId = 308, CatId = 4, Name = "Pepsi tươi", Price = 18000m, Type = DishType.Single, IsAvail = true, Desc = "Nước ngọt có ga bùng nổ" },
                    new { OldId = 309, CatId = 4, Name = "Trà dâu tằm pha lê", Price = 35000m, Type = DishType.Single, IsAvail = true, Desc = "Trà dâu tằm chua ngọt, topping thạch" },
                    new { OldId = 310, CatId = 5, Name = "Bánh trứng nướng (Egg Tart)", Price = 20000m, Type = DishType.Single, IsAvail = true, Desc = "Vỏ xốp giòn, nhân trứng mềm tan" },
                    new { OldId = 311, CatId = 5, Name = "Kem tươi Vani", Price = 12000m, Type = DishType.Single, IsAvail = true, Desc = "Kem ốc quế mát lạnh" },
                    new { OldId = 312, CatId = 6, Name = "Combo Ăn Vặt Lên Ngôi", Price = 70000m, Type = DishType.Combo, IsAvail = true, Desc = "1 Popcorn + 1 Khoai phô mai + 1 Pepsi" },
                    new { OldId = 313, CatId = 6, Name = "Combo Cặp Đôi Phá Đảo", Price = 160000m, Type = DishType.Combo, IsAvail = true, Desc = "2 Đùi gà + 1 Burger + 2 Trà dâu + 2 Bánh trứng" }
                };

                var dishMap = new Dictionary<int, int>();

                foreach (var item in dishesData)
                {
                    var dish = new Dish
                    {
                        CategoryId = categoryMap[item.CatId],
                        DishName = item.Name,
                        Price = item.Price,
                        Type = item.Type,
                        IsAvailable = item.IsAvail,
                        Description = item.Desc,
                        ImageUrl = "", // Ảnh trống
                        IsDeleted = false
                    };
                    await _unitOfWork.Dishes.AddAsync(dish);
                    await _unitOfWork.SaveAsync();
                    dishMap[item.OldId] = dish.Id;
                }

                // 3. Seed Combo Details
                var combosData = new[]
                {
                    new { ComboId = 312, ItemId = 303, Qty = 1 },
                    new { ComboId = 312, ItemId = 306, Qty = 1 },
                    new { ComboId = 312, ItemId = 308, Qty = 1 },
                    new { ComboId = 313, ItemId = 301, Qty = 2 },
                    new { ComboId = 313, ItemId = 305, Qty = 1 },
                    new { ComboId = 313, ItemId = 309, Qty = 2 },
                    new { ComboId = 313, ItemId = 310, Qty = 2 }
                };

                foreach (var item in combosData)
                {
                    var comboDetail = new ComboDetail
                    {
                        DishId = dishMap[item.ComboId],
                        ItemDishId = dishMap[item.ItemId],
                        Quantity = item.Qty,
                        IsDeleted = false
                    };
                    await _unitOfWork.ComboDetails.AddAsync(comboDetail);
                }

                await _unitOfWork.SaveAsync();
                await tx.CommitAsync();

                return Ok(ApiResponse<string>.Success("Seeding data thành công!"));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                throw new Exception("Lỗi khi seeding data: " + ex.Message, ex);
            }
        }
    }
}
