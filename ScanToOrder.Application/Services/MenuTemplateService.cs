using AutoMapper;
using ScanToOrder.Application.DTOs.Menu;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Entities.Menu;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class MenuTemplateService : IMenuTemplateService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        public MenuTemplateService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<CreateTemplateResponseDto> CreateTemplateAsync(CreateTemplateRequestDto request)
        {
            var templateEntity = _mapper.Map<MenuTemplate>(request);
            await _unitOfWork.MenuTemplates.AddAsync(templateEntity);
            await _unitOfWork.SaveAsync();
            return _mapper.Map<CreateTemplateResponseDto>(templateEntity);
        }

        public async Task<IEnumerable<MenuTemplateDto>> GetTemplatesAsync()
        {
            var templates = await _unitOfWork.MenuTemplates.GetAllAsync();
            return _mapper.Map<IEnumerable<MenuTemplateDto>>(templates);
        }

        public async Task<MenuTemplateDto> GetTemplateByIdAsync(int templateId)
        {
            var template = await _unitOfWork.MenuTemplates.GetByIdAsync(templateId);
            if (template == null)
            {
                throw new Exception(MenuTemplateMessage.MenuTemplateError.TEMPLATE_NOT_FOUND);
            }
            return _mapper.Map<MenuTemplateDto>(template);
        }
    }
}
