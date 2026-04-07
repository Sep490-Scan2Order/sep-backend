using AutoMapper;
using ScanToOrder.Application.DTOs.Plan;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class PlanService : IPlanService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public PlanService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<List<PlanResponse>> GetAllPlansAsync()
        {
            var plans = await _unitOfWork.Plans.GetAllAsync();
            return _mapper.Map<List<PlanResponse>>(plans);
        }

        public async Task<PlanResponse> GetPlanByIdAsync(int id)
        {
            var plan = await _unitOfWork.Plans.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Không tìm thấy gói dịch vụ với Id = {id}.");

            return _mapper.Map<PlanResponse>(plan);
        }

        public async Task<PlanResponse> CreatePlanAsync(CreatePlanRequest request)
        {
            var exists = await _unitOfWork.Plans.ExistsAsync(p => p.Name == request.Name);
            if (exists)
                throw new InvalidOperationException($"Gói dịch vụ với tên '{request.Name}' đã tồn tại.");

            var plan = _mapper.Map<Plan>(request);
            plan.Features = _mapper.Map<PlanFeaturesConfig>(request.Features);

            await _unitOfWork.Plans.AddAsync(plan);
            await _unitOfWork.SaveAsync();

            return _mapper.Map<PlanResponse>(plan);
        }

        public async Task<PlanResponse> UpdatePlanAsync(int id, UpdatePlanRequest request)
        {
            var plan = await _unitOfWork.Plans.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Không tìm thấy gói dịch vụ với Id = {id}.");

            var nameConflict = await _unitOfWork.Plans.ExistsAsync(p => p.Name == request.Name && p.Id != id);
            if (nameConflict)
                throw new InvalidOperationException($"Gói dịch vụ với tên '{request.Name}' đã tồn tại.");

            _mapper.Map(request, plan);
            plan.Features = _mapper.Map<PlanFeaturesConfig>(request.Features);

            _unitOfWork.Plans.Update(plan);
            await _unitOfWork.SaveAsync();

            return _mapper.Map<PlanResponse>(plan);
        }
    }
}