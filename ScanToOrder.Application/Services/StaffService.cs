using AutoMapper;
using ScanToOrder.Application.DTOs.Other;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Services
{
    public class StaffService : IStaffService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IPlanLimitationService _planLimitationService;

        public StaffService(IUnitOfWork unitOfWork, IMapper mapper, IPlanLimitationService planLimitationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _planLimitationService = planLimitationService;
        }

        public async Task<StaffDto> CreateStaff(CreateStaffRequest staffDto)
        {
            var existingUser = await _unitOfWork.AuthenticationUsers.GetByPhoneAsync(staffDto.Phone);
            if (existingUser != null)
            {
               throw new DomainException(StaffMessage.StaffError.STAFF_ALREADY_EXISTS);
            }
            var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(staffDto.RestaurantId);

            if (restaurant == null)
            {
                throw new DomainException(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
            }

            var features = await _planLimitationService.GetRestaurantFeaturesAsync(staffDto.RestaurantId);
            var currentStaffs = await _unitOfWork.Staffs.FindAsync(s => s.RestaurantId == staffDto.RestaurantId);
            if (currentStaffs.Count() >= features.MaxStaff)
            {
                throw new DomainException($"Gói dịch vụ (Plan) của cửa hàng hiện chỉ cho phép cấu hình tối đa {features.MaxStaff} nhân viên.");
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(staffDto.Password);
            var userEntity = _mapper.Map<AuthenticationUser>(staffDto);
            userEntity.Password = passwordHash;

            var staffEntity = _mapper.Map<Staff>(staffDto);
            staffEntity.AccountId = userEntity.Id;
            

            await _unitOfWork.AuthenticationUsers.AddAsync(userEntity);
            await _unitOfWork.Staffs.AddAsync(staffEntity);
            await _unitOfWork.SaveAsync();

            return _mapper.Map<StaffDto>(staffEntity);
        }

        public async Task<PagedResult<StaffDto>> GetAllStaff(int restaurantId, int page, int pageSize)
        {
            var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(restaurantId);
            var (data, totalCount) = await _unitOfWork.Staffs
                .GetStaffByRestaurantAsync(restaurantId, page, pageSize);

            var staffDtos = _mapper.Map<List<StaffDto>>(data);
         

            return new PagedResult<StaffDto>
            {
                Items = staffDtos,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<List<StaffDto>> GetAvailableCashiers()
        {
            var cashiers = await _unitOfWork.Staffs.GetAvailableCashiersAsync();

            return _mapper.Map<List<StaffDto>>(cashiers);
        }

        public async Task<List<StaffDto>> GetStaffByRestaurant(int restaurantId)
        {
            var staff = await _unitOfWork.Staffs.FindAsync(s => s.RestaurantId == restaurantId);
            return _mapper.Map<List<StaffDto>>(staff);
        }
    }
}
