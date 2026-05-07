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

            // Ensure only one Trial plan can exist at a time
            if (request.IsTrial)
            {
                var trialExists = await _unitOfWork.Plans.ExistsAsync(p => p.IsTrial);
                if (trialExists)
                    throw new InvalidOperationException("Hệ thống chỉ cho phép tồn tại 1 gói trải nghiệm (Trial) duy nhất. Vui lòng cập nhật gói hiện có hoặc xóa gói cũ trước khi tạo mới.");
            }

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

            // Ensure only one Trial plan can exist: if marking this plan as Trial,
            // no OTHER plan should already be Trial
            if (request.IsTrial && !plan.IsTrial)
            {
                var otherTrialExists = await _unitOfWork.Plans.ExistsAsync(p => p.IsTrial && p.Id != id);
                if (otherTrialExists)
                    throw new InvalidOperationException("Hệ thống chỉ cho phép tồn tại 1 gói trải nghiệm (Trial) duy nhất. Vui lòng bỏ cờ Trial khỏi gói cũ trước.");
            }

            _mapper.Map(request, plan);
            plan.Features = _mapper.Map<PlanFeaturesConfig>(request.Features);

            _unitOfWork.Plans.Update(plan);
            await _unitOfWork.SaveAsync();

            return _mapper.Map<PlanResponse>(plan);
        }
    }
}