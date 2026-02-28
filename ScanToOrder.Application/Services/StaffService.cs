using AutoMapper;
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
        public StaffService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
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

    }
}
