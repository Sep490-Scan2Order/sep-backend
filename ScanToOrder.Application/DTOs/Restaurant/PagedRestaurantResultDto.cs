namespace ScanToOrder.Application.DTOs.Restaurant
{
    public class PagedRestaurantResultDto
    {
        public List<RestaurantDto> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public bool HasNextPage => Page * PageSize < TotalCount;
    }
}
