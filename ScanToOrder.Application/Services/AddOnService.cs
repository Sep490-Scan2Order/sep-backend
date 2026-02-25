using AutoMapper;
using ScanToOrder.Application.DTOs.Plan;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Services
{
    public class AddOnService : IAddOnService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public AddOnService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<AddOnDto> CreateAddOn(CreateAddOnRequest addOnDto)
        {
            var addOnEntity = _mapper.Map<AddOn>(addOnDto);


            await _unitOfWork.AddOns.AddAsync(addOnEntity);

            await _unitOfWork.SaveAsync();

            return _mapper.Map<AddOnDto>(addOnEntity);
        }

        public async Task<IEnumerable<AddOnDto>> GetAllAddOns()
        {
            var addOns = await _unitOfWork.AddOns.GetAllAsync();

            if (addOns == null || !addOns.Any())
            {
                throw new DomainException("Hiện tại chưa có gói dịch vụ bổ sung (AddOn) nào trong hệ thống.");
            }

            return _mapper.Map<IEnumerable<AddOnDto>>(addOns);
        }


    }
}
