using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class BranchDishConfigRepository : GenericRepository<BranchDishConfig>, IBranchDishConfigRepository
    {
        public BranchDishConfigRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<BranchDishConfig>> GetByRestaurantIdWithIncludeAsync(int restaurantId)
        {
            return await _dbSet
                .Include(x => x.Restaurant)
                .Include(x => x.Dish)
                .Where(x => x.RestaurantId == restaurantId)
                .ToListAsync();
        }

        public async Task<BranchDishConfig?> GetByIdWithIncludeAsync(int id)
        {
            return await _dbSet
                .Include(x => x.Restaurant)
                .Include(x => x.Dish)
                .FirstOrDefaultAsync(x => x.Id == id);
        }
        
        public async Task AddRangeAsync(List<BranchDishConfig> configs)
        {
            await _dbSet.AddRangeAsync(configs);
        }

        public async Task<List<BranchDishConfig>> GetSellingDishesByRestaurantIdAsync(int restaurantId)
        {
            return await _dbSet
                .AsNoTracking()
                .Include(bdc => bdc.Dish)
                    .ThenInclude(d => d.Category)
                .Include(bdc => bdc.Dish)
                    .ThenInclude(d => d.PromotionDishes)
                        .ThenInclude(pd => pd.Promotion)
                .Include(bdc => bdc.Dish)
                    .ThenInclude(d => d.ComboDetails)
                        .ThenInclude(cd => cd.ItemDish)
                .Where(bdc => bdc.RestaurantId == restaurantId
                              && bdc.IsSelling
                              && !bdc.IsDeleted
                              && !bdc.Dish.IsDeleted)
                .Select(bdc => new BranchDishConfig
                {
                    Id = bdc.Id,
                    RestaurantId = bdc.RestaurantId,
                    DishId = bdc.DishId,
                    IsSelling = bdc.IsSelling,
                    Price = bdc.Price,
                    DishAvailability = bdc.DishAvailability,
                    IsSoldOut = bdc.IsSoldOut,
                    Dish = new Dish
                    {
                        Id = bdc.Dish.Id,
                        DishName = bdc.Dish.DishName,
                        Description = bdc.Dish.Description,
                        ImageUrl = bdc.Dish.ImageUrl,
                        Type = bdc.Dish.Type,
                        Category = bdc.Dish.Category,
                        PromotionDishes = bdc.Dish.PromotionDishes
                            .Where(pd => pd.Promotion.IsActive && !pd.Promotion.IsDeleted)
                            .ToList(),
                        ComboDetails = bdc.Dish.ComboDetails
                            .Select(cd => new ComboDetail
                            {
                                ItemDishId = cd.ItemDishId,
                                Quantity = cd.Quantity,
                                ItemDish = new Dish
                                {
                                    Id = cd.ItemDish.Id,
                                    DishName = cd.ItemDish.DishName,
                                    ImageUrl = cd.ItemDish.ImageUrl
                                }
                            })
                            .ToList()
                    }
                })
                .ToListAsync();
        }
        
        public async Task<List<BranchDishConfig>> GetAllDishesByRestaurantIdAsync(int restaurantId)
        {
            return await _dbSet
                .AsNoTracking()
                .Include(bdc => bdc.Dish)
                .ThenInclude(d => d.Category)
                .Include(bdc => bdc.Dish)
                .ThenInclude(d => d.PromotionDishes)
                .ThenInclude(pd => pd.Promotion)
                .Include(bdc => bdc.Dish)
                .ThenInclude(d => d.ComboDetails)
                .ThenInclude(cd => cd.ItemDish)
                .Where(bdc => bdc.RestaurantId == restaurantId
                              && !bdc.IsDeleted
                              && !bdc.Dish.IsDeleted)
                .Select(bdc => new BranchDishConfig
                {
                    Id = bdc.Id,
                    RestaurantId = bdc.RestaurantId,
                    DishId = bdc.DishId,
                    IsSelling = bdc.IsSelling,
                    Price = bdc.Price,
                    DishAvailability = bdc.DishAvailability,
                    IsSoldOut = bdc.IsSoldOut,
                    Dish = new Dish
                    {
                        Id = bdc.Dish.Id,
                        DishName = bdc.Dish.DishName,
                        Description = bdc.Dish.Description,
                        ImageUrl = bdc.Dish.ImageUrl,
                        Type = bdc.Dish.Type,
                        Category = bdc.Dish.Category,
                        PromotionDishes = bdc.Dish.PromotionDishes
                            .Where(pd => pd.Promotion.IsActive && !pd.Promotion.IsDeleted)
                            .ToList(),
                        ComboDetails = bdc.Dish.ComboDetails
                            .Select(cd => new ComboDetail
                            {
                                ItemDishId = cd.ItemDishId,
                                Quantity = cd.Quantity,
                                ItemDish = new Dish
                                {
                                    Id = cd.ItemDish.Id,
                                    DishName = cd.ItemDish.DishName,
                                    ImageUrl = cd.ItemDish.ImageUrl
                                }
                            })
                            .ToList()
                    }
                })
                .ToListAsync();
        }
        
        public async Task<List<BranchDishConfig>> GetSellingDishesByRestaurantIdAndDishIdsAsync(int restaurantId,
            List<int> dishIds)
        {
            return await _dbSet
                .AsNoTracking()
                .Include(bdc => bdc.Dish)
                    .ThenInclude(d => d.Category)
                .Include(bdc => bdc.Dish)
                    .ThenInclude(d => d.PromotionDishes)
                        .ThenInclude(pd => pd.Promotion)
                .Include(bdc => bdc.Dish)
                    .ThenInclude(d => d.ComboDetails)
                        .ThenInclude(cd => cd.ItemDish)
                .Where(bdc => bdc.RestaurantId == restaurantId
                              && bdc.IsSelling
                              && !bdc.IsDeleted
                              && !bdc.Dish.IsDeleted
                              && dishIds.Contains(bdc.DishId))
                .Select(bdc => new BranchDishConfig
                {
                    Id = bdc.Id,
                    RestaurantId = bdc.RestaurantId,
                    DishId = bdc.DishId,
                    IsSelling = bdc.IsSelling,
                    Price = bdc.Price,
                    DishAvailability = bdc.DishAvailability,
                    IsSoldOut = bdc.IsSoldOut,
                    Dish = new Dish
                    {
                        Id = bdc.Dish.Id,
                        DishName = bdc.Dish.DishName,
                        Description = bdc.Dish.Description,
                        ImageUrl = bdc.Dish.ImageUrl,
                        Type = bdc.Dish.Type,
                        Category = bdc.Dish.Category,
                        PromotionDishes = bdc.Dish.PromotionDishes
                            .Where(pd => pd.Promotion.IsActive && !pd.Promotion.IsDeleted)
                            .ToList(),
                        ComboDetails = bdc.Dish.ComboDetails
                            .Select(cd => new ComboDetail
                            {
                                ItemDishId = cd.ItemDishId,
                                Quantity = cd.Quantity,
                                ItemDish = new Dish
                                {
                                    Id = cd.ItemDish.Id,
                                    DishName = cd.ItemDish.DishName,
                                    ImageUrl = cd.ItemDish.ImageUrl
                                }
                            })
                            .ToList()
                    }
                })
                .ToListAsync();
        }

        public async Task<bool> ReserveDishAvailabilityAsync(int restaurantId, int dishId, int quantity)
        {
            var affected = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ""BranchDishConfigs""
                SET ""DishAvailability"" = ""DishAvailability"" - {quantity},
                    ""IsSoldOut"" = CASE WHEN ""DishAvailability"" - {quantity} <= 0 THEN TRUE ELSE ""IsSoldOut"" END
                WHERE ""RestaurantId"" = {restaurantId}
                  AND ""DishId"" = {dishId}
                  AND ""IsDeleted"" = FALSE
                  AND ""IsSelling"" = TRUE
                  AND ""IsSoldOut"" = FALSE
                  AND ""DishAvailability"" >= {quantity};
                ");

            return affected > 0;
        }

        public async Task<List<int>> ReserveDishAvailabilityBatchAsync(int restaurantId, Dictionary<int, int> dishQuantities)
        {
            if (dishQuantities == null || !dishQuantities.Any()) return new List<int>();

            var valuesBuilder = new System.Text.StringBuilder();
            var index = 0;
            foreach (var kvp in dishQuantities)
            {
                if (index > 0) valuesBuilder.Append(", ");
                valuesBuilder.Append($"({kvp.Key}::integer, {kvp.Value}::integer)");
                index++;
            }

            var sql = $@"
                WITH request(""DishId"", ""Quantity"") AS (
                    VALUES {valuesBuilder.ToString()}
                ),
                updated AS (
                    UPDATE ""BranchDishConfigs"" b
                    SET ""DishAvailability"" = b.""DishAvailability"" - r.""Quantity"",
                        ""IsSoldOut"" = CASE WHEN b.""DishAvailability"" - r.""Quantity"" <= 0 THEN TRUE ELSE b.""IsSoldOut"" END
                    FROM request r
                    WHERE b.""RestaurantId"" = {restaurantId} AND b.""DishId"" = r.""DishId""
                      AND b.""IsDeleted"" = FALSE AND b.""IsSelling"" = TRUE AND b.""IsSoldOut"" = FALSE
                      AND b.""DishAvailability"" >= r.""Quantity""
                    RETURNING b.""DishId""
                )
                SELECT r.""DishId"" AS ""Value"" FROM request r
                LEFT JOIN updated u ON r.""DishId"" = u.""DishId""
                WHERE u.""DishId"" IS NULL;
            ";

            return await _context.Database.SqlQueryRaw<int>(sql).ToListAsync();
        }

        public async Task<bool> RefundDishAvailabilityAsync(int restaurantId, int dishId, int quantity)
        {
            var affected = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ""BranchDishConfigs""
                SET ""DishAvailability"" = ""DishAvailability"" + {quantity},
                    ""IsSoldOut"" = CASE WHEN ""DishAvailability"" + {quantity} > 0 THEN FALSE ELSE ""IsSoldOut"" END
                WHERE ""RestaurantId"" = {restaurantId}
                  AND ""DishId"" = {dishId}
                  AND ""IsDeleted"" = FALSE;
                ");

            return affected > 0;
        }

        public async Task<bool> RefundDishAvailabilityBatchAsync(int restaurantId, Dictionary<int, int> dishQuantities)
        {
            if (dishQuantities == null || !dishQuantities.Any()) return false;

            var sqlBuilder = new System.Text.StringBuilder();
            foreach (var kvp in dishQuantities)
            {
                sqlBuilder.AppendLine($@"
                    UPDATE ""BranchDishConfigs""
                    SET ""DishAvailability"" = ""DishAvailability"" + {kvp.Value},
                        ""IsSoldOut"" = CASE WHEN ""DishAvailability"" + {kvp.Value} > 0 THEN FALSE ELSE ""IsSoldOut"" END
                    WHERE ""RestaurantId"" = {restaurantId}
                      AND ""DishId"" = {kvp.Key}
                      AND ""IsDeleted"" = FALSE;");
            }

            var affected = await _context.Database.ExecuteSqlRawAsync(sqlBuilder.ToString());
            return affected > 0;
        }
    }
}