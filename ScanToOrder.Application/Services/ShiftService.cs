using AutoMapper;
using ScanToOrder.Application.DTOs.Shift;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.Shifts;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public  class ShiftService : IShiftService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public ShiftService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }
        public async Task<ShiftDto> CheckInShiftAsync(int restaurantId, Guid staffId, decimal openingCashAmount, string? note)
        {
            var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(restaurantId);

            if (restaurant == null)
            {
                throw new DomainException(Message.RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
            }

            if (openingCashAmount < restaurant.MinCashAmount)
            {
                throw new DomainException(Message.ShiftMessage.ShiftError.OPENING_CASH_INVALID);
            }

            var activeShift = await _unitOfWork.Shifts
                .FirstOrDefaultAsync(x => x.StaffId == staffId && x.Status == ShiftStatus.Open);

            if (activeShift != null)
            {
                throw new DomainException(Message.ShiftMessage.ShiftError.SHIFT_ALREADY_OPEN);
            }

            var shift = new Shift
            {
                RestaurantId = restaurantId,
                StaffId = staffId,
                StartDate = DateTime.UtcNow,
                OpeningCashAmount = openingCashAmount,
                Note = note ?? string.Empty,
                Status = ShiftStatus.Open
            };

            await _unitOfWork.Shifts.AddAsync(shift);
            await _unitOfWork.SaveAsync();

            return _mapper.Map<ShiftDto>(shift);
        }

        public async Task<ShiftDto> CheckOutShiftAsync(int shiftId, decimal cashAmount,string? note)
        {
            var shift = await _unitOfWork.Shifts.GetByIdAsync(shiftId);

            if (shift == null)
            {
                throw new DomainException(Message.ShiftMessage.ShiftError.SHIFT_NOT_FOUND);
            }

            if (shift.Status != ShiftStatus.Open)
            {
                throw new DomainException(Message.ShiftMessage.ShiftError.SHIFT_ALREADY_CLOSED);
            }

            var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(shift.RestaurantId);

            if (restaurant == null)
            {
                throw new DomainException(Message.RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
            }

            if (cashAmount < restaurant.MinCashAmount)
            {
                throw new DomainException(Message.ShiftMessage.ShiftError.CASH_AMOUNT_INVALID);
            }

            shift.OpeningCashAmount = cashAmount;

            shift.EndDate = DateTime.UtcNow;
            shift.Status = ShiftStatus.Closed;
            shift.Note = note ?? string.Empty;

            _unitOfWork.Shifts.Update(shift);
            await _unitOfWork.SaveAsync();

            return _mapper.Map<ShiftDto>(shift);
        }
    }
}
