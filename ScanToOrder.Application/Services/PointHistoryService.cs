using ScanToOrder.Application.DTOs.PointHistory;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.Points;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class PointHistoryService : IPointHistoryService
    {
        private readonly IUnitOfWork _unitOfWork;
        public PointHistoryService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task<AddPointHistoryDtoResponse> MinusPointHistoryAsync(AddPointHistoryDtoRequest pointHistoryDto)
        {
            var memberPoint = await _unitOfWork.MemberPoints.GetByIdAsync(pointHistoryDto.MemberPointId);
            if (memberPoint == null) throw new Exception("MemberPoint not found");

            int updatedBalance = memberPoint.CurrentPoint - pointHistoryDto.Point;

            var pointHistory = new PointHistory
            {
                Point = pointHistoryDto.Point,
                Type = pointHistoryDto.Type,
                CreateDate = DateTime.UtcNow,
                OrderId = pointHistoryDto.OrderId,
                MemberPointId = pointHistoryDto.MemberPointId
            };

            memberPoint.CurrentPoint = updatedBalance;

            await _unitOfWork.PointHistories.AddAsync(pointHistory);

            await _unitOfWork.SaveAsync();

            return new AddPointHistoryDtoResponse
            {
                PointHistoryId = pointHistory.PointHistoryId,
            };
        }

        public async Task<AddPointHistoryDtoResponse> PlusPointHistoryAsync(AddPointHistoryDtoRequest pointHistoryDto)
        {
            var memberPoint = await _unitOfWork.MemberPoints.GetByIdAsync(pointHistoryDto.MemberPointId);
            if (memberPoint == null) throw new Exception("MemberPoint not found");

            int updatedBalance = memberPoint.CurrentPoint + pointHistoryDto.Point;

            var pointHistory = new PointHistory
            {
                Point = pointHistoryDto.Point,
                Type = pointHistoryDto.Type,
                CreateDate = DateTime.UtcNow,
                OrderId = pointHistoryDto.OrderId,
                MemberPointId = pointHistoryDto.MemberPointId
            };

            memberPoint.CurrentPoint = updatedBalance;

            await _unitOfWork.PointHistories.AddAsync(pointHistory);

            await _unitOfWork.SaveAsync();

            return new AddPointHistoryDtoResponse
            {
                PointHistoryId = pointHistory.PointHistoryId,
            };
        }
    }
}
