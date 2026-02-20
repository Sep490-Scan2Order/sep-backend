using ScanToOrder.Application.DTOs.MemberPoint;

namespace ScanToOrder.Application.Interfaces
{
    public interface IMemberPointService
    {
        Task<AddMemberPointDtoResponse> AddMemberPointAsync(AddMemberPointDtoRequest memberPointDto);
        Task<int> GetCurrentPointAsync(Guid? accountId);
    }
}
