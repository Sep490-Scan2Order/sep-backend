using AutoMapper;
using ScanToOrder.Application.DTOs.Dishes;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Services
{
    public class BranchDishConfigService : IBranchDishConfigService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public BranchDishConfigService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<BranchDishConfigDto> ConfigDishByRestaurant(CreateBranchDishConfig request)
        {
            var existingRestaurant = await _unitOfWork.Restaurants.GetByIdAsync(request.RestaurantId);
            if (existingRestaurant == null)
                throw new Exception(Message.RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);

            var existingDish = await _unitOfWork.Dishes.GetByIdAsync(request.DishId);
            if (existingDish == null)
                throw new Exception(Message.DishMessage.DishError.DISH_NOT_FOUND);

            var configExists = await _unitOfWork.BranchDishConfigs.ExistsAsync(
                x => x.RestaurantId == request.RestaurantId && x.DishId == request.DishId);
            if (configExists)
                throw new DomainException(BranchDishMessage.BranchDishError.BRANCH_DISH_ALREADY_EXISTS);

            var branchDishConfig = _mapper.Map<BranchDishConfig>(request);

            await _unitOfWork.BranchDishConfigs.AddAsync(branchDishConfig);
            await _unitOfWork.SaveAsync();

            return _mapper.Map<BranchDishConfigDto>(branchDishConfig);
        }


        public async Task<List<BranchDishConfigDto>> GetBranchDishByRestaurant(int restaurantId)
        {
            var existingRestaurant = await _unitOfWork.Restaurants.GetByIdAsync(restaurantId);
            if (existingRestaurant == null)
                throw new Exception(Message.RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);

            var branchDishConfigs = await _unitOfWork
                .BranchDishConfigs
                .GetByRestaurantIdWithIncludeAsync(restaurantId);

            return _mapper.Map<List<BranchDishConfigDto>>(branchDishConfigs);
        }

        public async Task<BranchDishConfigDto> ToggleSoldOutAsync(int branchDishConfigId, bool isSoldOut)
        {
            var branchDishConfig = await _unitOfWork.BranchDishConfigs
                .GetByIdWithIncludeAsync(branchDishConfigId);

            if (branchDishConfig == null)
                throw new Exception(Message.BranchDishMessage.BranchDishError.BRANCH_DISH_NOT_FOUND);

            branchDishConfig.IsSoldOut = isSoldOut;

            _unitOfWork.BranchDishConfigs.Update(branchDishConfig);
            await _unitOfWork.SaveAsync();

            return _mapper.Map<BranchDishConfigDto>(branchDishConfig);
        }
    }
}
