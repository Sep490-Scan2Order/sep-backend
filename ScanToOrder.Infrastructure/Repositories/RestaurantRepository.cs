using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class RestaurantRepository : GenericRepository<Restaurant>, IRestaurantRepository
    {
        private readonly AppDbContext _context;
        public RestaurantRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<(Restaurant Restaurant, double DistanceKm)>> GetNearbyRestaurantsAsync(
            double latitude,
            double longitude,
            double radiusKm,
            int limit = 10)
        {
            var maxRadiusMeters = radiusKm * 1000;

            FormattableString sql = $@"
                SELECT r.*
                FROM ""Restaurants"" r
                WHERE r.""Location"" IS NOT NULL
                  AND r.""IsActive"" = true
                  AND r.""IsDeleted"" = false
                  AND ST_DWithin(
                      r.""Location""::geography, 
                      ST_SetSRID(ST_MakePoint({longitude}, {latitude}), 4326)::geography, 
                      {maxRadiusMeters}
                  )
                ORDER BY ST_DistanceSphere(
                    r.""Location"", 
                    ST_SetSRID(ST_MakePoint({longitude}, {latitude}), 4326)
                )
                LIMIT {limit}";

            var restaurants = await _context.Restaurants
                .FromSqlInterpolated(sql)
                .ToListAsync();

            var result = new List<(Restaurant Restaurant, double DistanceKm)>();

            foreach (var restaurant in restaurants)
            {
                if (restaurant.Location != null)
                {
                    var resLon = restaurant.Location.X;
                    var resLat = restaurant.Location.Y;
                    var distanceKm = CalculateHaversineDistanceKm(latitude, longitude, resLat, resLon);
                    result.Add((restaurant, Math.Round(distanceKm, 2)));
                }
            }

            return result;
        }

        /// <summary>
        /// Tính khoảng cách giữa 2 tọa độ trên mặt cầu (Haversine).
        /// Tương đương ST_DistanceSphere của PostGIS, chạy trên RAM.
        /// </summary>
        private static double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0; 
            var dLat = (lat2 - lat1) * Math.PI / 180.0;
            var dLon = (lon2 - lon1) * Math.PI / 180.0;

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }
}
