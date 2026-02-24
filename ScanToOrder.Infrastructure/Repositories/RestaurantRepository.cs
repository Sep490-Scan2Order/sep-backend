using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using ScanToOrder.Domain.Entities.Restaurant;
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
            var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            var userLocation = geometryFactory.CreatePoint(new Coordinate(longitude, latitude));
            
            var maxRadiusMeters = radiusKm * 1000;
            var restaurants = await _context.Restaurants
                .Where(r => (bool)r.IsActive! == true && r.IsDeleted == false && r.Location != null)
                .Where(r => r.Location != null && r.Location.IsWithinDistance(userLocation, maxRadiusMeters))
                .OrderBy(r => r.Location!.Distance(userLocation))
                .Take(limit)
                .ToListAsync();
            
            // FormattableString sql = $@"
            //     SELECT r.*
            //     FROM ""Restaurants"" r
            //     WHERE r.""Location"" IS NOT NULL
            //       AND r.""IsActive"" = true
            //       AND r.""IsDeleted"" = false
            //       AND ST_DWithin(
            //           r.""Location""::geography, 
            //           ST_SetSRID(ST_MakePoint({longitude}, {latitude}), 4326)::geography, 
            //           {maxRadiusMeters}
            //       )
            //     ORDER BY ST_DistanceSphere(
            //         r.""Location"", 
            //         ST_SetSRID(ST_MakePoint({longitude}, {latitude}), 4326)
            //     )
            //     LIMIT {limit}";
            //
            // var restaurants = await _context.Restaurants
            //     .FromSqlInterpolated(sql)
            //     .ToListAsync();

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

        public async Task<(List<(Restaurant Restaurant, double DistanceKm)> Items, int TotalCount)> GetRestaurantsSortedByDistancePagedAsync(
            double latitude,
            double longitude,
            int page,
            int pageSize)
        {
            var offset = (page - 1) * pageSize;
            if (offset < 0) offset = 0;
            if (pageSize <= 0) pageSize = 20;

            var totalCount = await _context.Restaurants
                .Where(r => r.Location != null && r.IsActive == true && r.IsDeleted == false)
                .CountAsync();

            FormattableString dataSql = $@"
                SELECT r.*
                FROM ""Restaurants"" r
                WHERE r.""Location"" IS NOT NULL
                  AND r.""IsActive"" = true
                  AND r.""IsDeleted"" = false
                ORDER BY r.""Location"" <-> ST_SetSRID(ST_MakePoint({longitude}, {latitude}), 4326)::geometry
                OFFSET {offset}
                LIMIT {pageSize}";

            var restaurants = await _context.Restaurants
                .FromSqlInterpolated(dataSql)
                .ToListAsync();

            var items = new List<(Restaurant Restaurant, double DistanceKm)>();
            foreach (var restaurant in restaurants)
            {
                if (restaurant.Location != null)
                {
                    var distanceKm = CalculateHaversineDistanceKm(latitude, longitude, restaurant.Location.Y, restaurant.Location.X);
                    items.Add((restaurant, Math.Round(distanceKm, 2)));
                }
            }

            return (items, totalCount);
        }

        public async Task<(List<Restaurant> Items, int TotalCount)> GetRestaurantsSortedByTotalOrderPagedAsync(int page, int pageSize)
        {
            var offset = (page - 1) * pageSize;
            if (offset < 0) offset = 0;
            if (pageSize <= 0) pageSize = 20;

            var query = _context.Restaurants
                .Where(r => r.IsActive == true && r.IsDeleted == false);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(r => r.TotalOrder ?? 0)
                .Skip(offset)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

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
