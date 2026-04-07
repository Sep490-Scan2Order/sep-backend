using ScanToOrder.Application.DTOs.Plan;

namespace ScanToOrder.Application.Interfaces
{
    public interface IPlanService
    {
        Task<List<PlanResponse>> GetAllPlansAsync();
        Task<PlanResponse> GetPlanByIdAsync(int id);
        Task<PlanResponse> CreatePlanAsync(CreatePlanRequest request);
        Task<PlanResponse> UpdatePlanAsync(int id, UpdatePlanRequest request);
    }
}
