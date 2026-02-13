using ScanToOrder.Application.DTOs.MemberPoint;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.Points;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class MemberPointService : IMemberPointService
    {
        private readonly IUnitOfWork _unitOfWork;
        public MemberPointService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task<AddMemberPointDtoResponse> AddMemberPointAsync(AddMemberPointDtoRequest memberPointDto)
        {
            var memberPoint = new MemberPoint
            {
                CurrentPoint = memberPointDto.CurrentPoint,
                RedeemAt = memberPointDto.RedeemAt,
                CustomerId = memberPointDto.CustomerId
            };

            await _unitOfWork.MemberPoints.AddAsync(memberPoint);
            await _unitOfWork.SaveAsync();

            return new AddMemberPointDtoResponse
            {
                MemberPointId = memberPoint.MemberPointId,
                CurrentPoint = memberPoint.CurrentPoint,
                RedeemAt = memberPoint.RedeemAt,
                CustomerId = memberPoint.CustomerId
            };
        }
    }
}
