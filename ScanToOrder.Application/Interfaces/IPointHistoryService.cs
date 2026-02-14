using ScanToOrder.Application.DTOs.PointHistory;

namespace ScanToOrder.Application.Interfaces
{
    public interface IPointHistoryService
    {
        Task<AddPointHistoryDtoResponse> PlusPointHistoryAsync(AddPointHistoryDtoRequest pointHistoryDto);
        Task<AddPointHistoryDtoResponse> MinusPointHistoryAsync(AddPointHistoryDtoRequest pointHistoryDto);
    }
}
