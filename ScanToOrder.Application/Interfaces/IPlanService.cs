using ScanToOrder.Application.DTOs.Plan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
