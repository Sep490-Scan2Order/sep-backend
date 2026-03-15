using AutoMapper;
using Microsoft.Extensions.Configuration;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using SlugGenerator;

namespace ScanToOrder.Application.Services
{
    public class RestaurantService : IRestaurantService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IQrCodeService _qrCodeService;
        private readonly IConfiguration _configuration;
        private readonly IStorageService _storageService;

        public RestaurantService(IUnitOfWork unitOfWork, IMapper mapper,
            IQrCodeService qrCodeService, IConfiguration configuration,
            IStorageService storageService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _qrCodeService = qrCodeService;
            _configuration = configuration;
            _storageService = storageService;
        }

        public async Task<RestaurantDto?> GetRestaurantByIdAsync(int id)
        {
            var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(id);
            if (restaurant == null)
                return null;
            var dto = _mapper.Map<RestaurantDto>(restaurant);
            return dto;
        }

        public async Task<PagedRestaurantResultDto> GetRestaurantsPagedAsync(double? latitude, double? longitude,
            int page = 1, int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize <= 0) pageSize = 20;

            if (latitude.HasValue && longitude.HasValue)
            {
                var (items, totalCount) = await _unitOfWork.Restaurants.GetRestaurantsSortedByDistancePagedAsync(
                    latitude.Value,
                    longitude.Value,
                    page,
                    pageSize);

                var dtos = items.Select(item =>
                {
                    var dto = _mapper.Map<RestaurantDto>(item.Restaurant);
                    dto.DistanceKm = item.DistanceKm;
                    return dto;
                }).ToList();

                return new PagedRestaurantResultDto
                {
                    Items = dtos,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            }

            var (restaurants, totalCountByOrder) =
                await _unitOfWork.Restaurants.GetRestaurantsSortedByTotalOrderPagedAsync(page, pageSize);
            var dtosByOrder = _mapper.Map<List<RestaurantDto>>(restaurants);

            return new PagedRestaurantResultDto
            {
                Items = dtosByOrder,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCountByOrder
            };
        }

        public async Task<List<RestaurantDto>> GetNearbyRestaurantsAsync(double latitude, double longitude,
            double radiusKm, int limit = 10)
        {
            var restaurantsWithDistance = await _unitOfWork.Restaurants.GetNearbyRestaurantsAsync(
                latitude,
                longitude,
                radiusKm,
                limit);

            var restaurantDtos = restaurantsWithDistance.Select(item =>
            {
                var dto = _mapper.Map<RestaurantDto>(item.Restaurant);
                dto.DistanceKm = item.DistanceKm;
                return dto;
            }).ToList();

            return restaurantDtos;
        }

        public async Task<RestaurantDto> CreateRestaurantAsync(Guid tenantId, CreateRestaurantRequest request)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);
            if (tenant == null) throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);

            // _ = true switch
            // {
            //     _ when string.IsNullOrEmpty(tenant.TaxNumber) => throw new DomainException(TenantMessage.TenantError
            //         .TENANT_MISSING_TAX_NUMBER),
            //     _ when tenant.BankId == null || tenant.BankId == Guid.Empty => throw new DomainException(TenantMessage
            //         .TenantError.TENANT_MISSING_BANK),
            //     _ when string.IsNullOrEmpty(tenant.CardNumber) => throw new DomainException(TenantMessage.TenantError
            //         .TENANT_MISSING_CARD),
            //     _ when string.IsNullOrEmpty(request.Phone) => throw new DomainException(TenantMessage.TenantError
            //         .TENANT_MISSING_PHONE),
            //     _ => true
            // };

            var restaurant = _mapper.Map<Restaurant>(request);
            restaurant.TenantId = tenantId;
            restaurant.IsActive = true;
            restaurant.IsOpened = false;

            string baseSlug = request.RestaurantName.GenerateSlug();
            restaurant.Slug = $"{baseSlug}-{Guid.NewGuid().ToString("N").Substring(0, 4)}";

            if (request.Image != null && request.Image.Length > 0)
            {
                using var ms = new MemoryStream();
                await request.Image.CopyToAsync(ms);

                restaurant.Image = await _storageService.UploadFromBytesAsync(
                    ms.ToArray(),
                    $"{restaurant.Slug}_main.png",
                    "restaurant_images"
                );
            }

            await _unitOfWork.Restaurants.AddAsync(restaurant);
            await _unitOfWork.SaveAsync();

            string baseUrl = _configuration["FrontEndUrl:scan2order_id_vn"]?.TrimEnd('/')!;
            restaurant.ProfileUrl = $"{baseUrl}/restaurant/{restaurant.Slug}";

            var qrBytes = _qrCodeService.GenerateRestaurantQrCodeBytes(restaurant.Slug);
            string fileName = $"{restaurant.Slug}_qr.png";


            restaurant.QrMenu = await _storageService.UploadFromBytesAsync(
                qrBytes,
                $"{restaurant.Slug}_qr.png"
            );

            _unitOfWork.Restaurants.Update(restaurant);
            await _unitOfWork.SaveAsync();

            return _mapper.Map<RestaurantDto>(restaurant);
        }

        public async Task<RestaurantDto> UpdateRestaurantAsync(int restaurantId, Guid tenantId,
            UpdateRestaurantRequest request)
        {
            var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(restaurantId);
            if (restaurant == null)
                throw new DomainException(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);

            if (restaurant.TenantId != tenantId)
                throw new DomainException(RestaurantMessage.RestaurantError.NOT_FOUND_RESTAURANT_FOR_USER);

            restaurant.RestaurantName = request.RestaurantName;
            restaurant.Address = request.Address;
            restaurant.Phone = request.Phone;
            restaurant.Description = request.Description;

            if (request.Latitude.HasValue || request.Longitude.HasValue)
            {
                if (!(request.Latitude.HasValue && request.Longitude.HasValue))
                    throw new DomainException(RestaurantMessage.RestaurantError.INVALID_RESTAURANT_LOCATION);

                restaurant.Location =
                    new NetTopologySuite.Geometries.Point(request.Longitude.Value, request.Latitude.Value)
                    {
                        SRID = 4326
                    };
            }

            if (request.Image != null && request.Image.Length > 0)
            {
                using var ms = new MemoryStream();
                await request.Image.CopyToAsync(ms);

                restaurant.Image = await _storageService.UploadFromBytesAsync(
                    ms.ToArray(),
                    $"{restaurant.Slug}_main.png",
                    "restaurant_images"
                );
            }

            _unitOfWork.Restaurants.Update(restaurant);
            await _unitOfWork.SaveAsync();

            return _mapper.Map<RestaurantDto>(restaurant);
        }

        public async Task<RestaurantDto> GetRestaurantBySlugAsync(string slug)
        {
            var restaurant = await _unitOfWork.Restaurants.FirstOrDefaultAsync(r => r.Slug == slug);
            if (restaurant == null)
                throw new DomainException(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
            return _mapper.Map<RestaurantDto>(restaurant);
        }

        public async Task<byte[]> GetRestaurantQrImageBySlugAsync(string slug)
        {
            var restaurant = await _unitOfWork.Restaurants.FirstOrDefaultAsync(r => r.Slug == slug);

            if (restaurant == null)
                throw new DomainException(QrMessage.QrError.NO_RESTAURANT_FOUND_TO_GENERATE_QR);

            string fullUrl = $"https://scan2order.id.vn/{slug}";

            return _qrCodeService.GenerateRestaurantQrCodeBytes(fullUrl);
        }

        public async Task<IEnumerable<RestaurantDto>> GetRestaurantsByTenantIdAsync(Guid tenantId)
        {
            var restaurants = await _unitOfWork.Restaurants.FindAsync(r => r.TenantId == tenantId);
            var dtos = restaurants.Select(r => _mapper.Map<RestaurantDto>(r));
            return dtos;
        }
        
        // This method is the core of the menu retrieval logic, combining restaurant data with active promotions to calculate real-time pricing and labels for the UI.
        public async Task<List<MenuCategoryDto>> GetRestaurantMenuAsync(int restaurantId)
        {
            // 0. Use consistent time with UTC+7 offset for local business logic
            var now = DateTime.UtcNow.AddHours(7);

            // 1. Verify restaurant and get TenantId
            var restaurantEntity = (await _unitOfWork.Restaurants.GetByIdAsync(restaurantId))
                .OrThrow(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);

            var tenantId = restaurantEntity.TenantId;

            // 2. Fetch "Base" promotions (Those that apply to ALL dishes in the restaurant)
            // Logic: IsGlobal (Tenant-wide) OR (Restaurant-mapped AND NO specific dishes assigned)
            var basePromotions = await _unitOfWork.Promotions.GetAllAsync(p =>
                p.TenantId == tenantId &&
                p.IsActive &&
                !p.IsDeleted &&
                p.Scope == PromotionScope.Dish &&
                (p.IsGlobal || (p.RestaurantPromotions.Any(rp => rp.RestaurantId == restaurantId)
                                && !p.PromotionDishes.Any()))
            );

            // 3. Get selling dishes (Ensure Repo includes PromotionDishes.Promotion)
            var branchDishes = await _unitOfWork.BranchDishConfigs.GetSellingDishesByRestaurantIdAsync(restaurantId);

            // 4. Build Menu structure
            var menu = branchDishes
                .GroupBy(bdc => new { bdc.Dish.Category.Id, bdc.Dish.Category.CategoryName })
                .Select(group => new MenuCategoryDto
                {
                    CategoryId = group.Key.Id,
                    CategoryName = group.Key.CategoryName,
                    Dishes = group.Select(bdc =>
                    {
                        // 5. Identify promotions specifically mapped to THIS dish
                        var specificDishPromos = bdc.Dish.PromotionDishes?
                                                     .Select(pd => pd.Promotion)
                                                     .Where(p => p.Scope == PromotionScope.Dish &&
                                                                 p.IsActive &&
                                                                 !p.IsDeleted)
                                                 ?? Enumerable.Empty<Promotion>();

                        // Combine Base promos (e.g., Grand Opening) with Specific promos (e.g., Happy Hour)
                        var allEligiblePromotions = basePromotions.Concat(specificDishPromos);

                        // 6. Identify the "Winning" promotion (Highest Priority, then Highest Value) 
                        var winningPromo = allEligiblePromotions
                            .Where(p => p.IsValidAt(now) && (bdc.Price - CalculateDiscountValue(bdc.Price, p) > 0))
                            .OrderByDescending(p => p.Priority)
                                .ThenByDescending(p => CalculateDiscountValue(bdc.Price, p))
                            .FirstOrDefault();

                        // 7. Calculate final price and UI labels
                        int discountedPrice = (int)bdc.Price;
                        string? promoLabel = null;

                        if (winningPromo != null)
                        {
                            var discountAmount = CalculateDiscountValue(bdc.Price, winningPromo);
                            discountedPrice = (int)Math.Max(bdc.Price - discountAmount, 0);

                            // Generate UI Label: "-20%" or "-15k"
                            promoLabel = winningPromo.DiscountType == DiscountType.Percentage
                                ? $"-{winningPromo.DiscountValue}%"
                                : $"-{(winningPromo.DiscountValue / 1000):G}k";
                        }

                        return new MenuDishItemDto
                        {
                            DishId = bdc.DishId,
                            DishName = bdc.Dish.DishName,
                            Description = bdc.Dish.Description,
                            ImageUrl = bdc.Dish.ImageUrl,
                            Price = (int)bdc.Price,
                            DiscountedPrice = discountedPrice,
                            PromotionName = winningPromo?.Name,
                            PromotionLabel = promoLabel,
                            PromoType = winningPromo?.Type,
                            // Calculate real-time expiration for UI countdown
                            Type = bdc.Dish.Type,
                            DishAvailabilityStock = bdc.DishAvailability,
                            ExpiredAt = winningPromo != null ? CalculateTrueExpiredAt(winningPromo, now) : null,
                            IsSoldOut = bdc.IsSoldOut
                        };
                    }).ToList()
                })
                .ToList();

            return menu;
        }

        private decimal CalculateDiscountValue(decimal price, Promotion p)
        {
            // Fixed amount discount (e.g., subtract 20,000đ)
            if (p.DiscountType == DiscountType.FixedAmount)
                return p.DiscountValue;

            // Percentage discount (e.g., 10% of 45,000đ)
            var discount = price * (p.DiscountValue / 100);

            // Apply cap if MaxDiscountValue is defined (e.g., 10% but max 50k)
            return p.MaxDiscountValue.HasValue
                ? Math.Min(discount, p.MaxDiscountValue.Value)
                : discount;
        }

        private DateTime? CalculateTrueExpiredAt(Promotion p, DateTime now)
        {
            // Use the Date from the 'now' parameter to stay consistent with UTC+7 context
            var today = now.Date;
            DateTime? trueExpiredAt = p.EndDate;

            switch (p.Type)
            {
                case PromotionType.HappyHour:
                case PromotionType.WeeklySpecial:
                    // If it has a specific time of day, that's the real expiration for today
                    if (p.DailyEndTime.HasValue)
                    {
                        trueExpiredAt = today.Add(p.DailyEndTime.Value);
                    }
                    else if (p.Type == PromotionType.WeeklySpecial)
                    {
                        // If no specific time, it ends at the end of the day
                        trueExpiredAt = today.AddDays(1).AddTicks(-1);
                    }

                    break;

                case PromotionType.Clearance:
                case PromotionType.Standard:
                    // For campaign-based promos, use the overall campaign end date
                    trueExpiredAt = p.EndDate;
                    break;
            }

            // Guard: The "today's" end time should never exceed the campaign's final EndDate
            if (p.EndDate.HasValue && trueExpiredAt > p.EndDate.Value)
            {
                trueExpiredAt = p.EndDate.Value;
            }

            return trueExpiredAt;
        }

        public async Task<string> UpdateReceivingOrdersAsync(int restaurantId, bool isReceivingOrders)
        {
            var restaurant = await _unitOfWork.Restaurants
                .GetByIdAsync(restaurantId);

            if (restaurant == null)
                return null;

            restaurant.IsReceivingOrders = isReceivingOrders;
            _unitOfWork.Restaurants.Update(restaurant);
            await _unitOfWork.SaveAsync();

            return RestaurantMessage.RestaurantSuccess.RESTAURANT_RECEIVING_STATUS_UPDATED;
        }

        public async Task<AssignPresentCashierDto> AssignPresentCashier(int restaurantId, Guid cashierId)
        {
            var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(restaurantId);

            if (restaurant == null)
            {
                throw new DomainException(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
            }

            var staff = await _unitOfWork.Staffs.GetByIdAsync(cashierId);

            if (staff == null)
            {
                throw new DomainException(StaffMessage.StaffError.STAFF_NOT_FOUND);
            }

            if (staff.RestaurantId != restaurantId)
            {
                throw new DomainException(StaffMessage.StaffError.STAFF_NOT_IN_RESTAURANT);
            }

            restaurant.PresentCashierId = staff.Id;
            restaurant.IsAvailableShift = true;


            _unitOfWork.Restaurants.Update(restaurant);
            await _unitOfWork.SaveAsync();

            return new AssignPresentCashierDto
            {
                CashierId = staff.Id,
                CashierName = staff.Name,
            };
        }

        public async Task<string> ConfigMinCashAmountAsync(int restaurantId, decimal minCashAmount)
        {
            var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(restaurantId);

            if (restaurant == null)
            {
                throw new DomainException(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
            }

            restaurant.MinCashAmount = minCashAmount;

            _unitOfWork.Restaurants.Update(restaurant);
            await _unitOfWork.SaveAsync();

            return Message.RestaurantMessage.RestaurantSuccess.RESTAURANT_UPDATED ;
        }


    }
}