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
        private readonly IStorageService _storageService;
        private readonly IRestaurantMenuService _restaurantMenuService;

        public MenuTemplateService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IStorageService storageService,
            IRestaurantMenuService restaurantMenuService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _storageService = storageService;
            _restaurantMenuService = restaurantMenuService;
        }

        public async Task<CreateTemplateResponseDto> CreateTemplateAsync(CreateTemplateRequestDto request)
        {
            var templateEntity = _mapper.Map<MenuTemplate>(request);
            if (request.BackgroundImageUrl != null && request.BackgroundImageUrl.Length > 0)
            {
                using var memoryStream = new MemoryStream();
                await request.BackgroundImageUrl.CopyToAsync(memoryStream);
                var fileBytes = memoryStream.ToArray();

                var fileName = $"{Guid.NewGuid()}_{request.BackgroundImageUrl.FileName}";

                var imageUrl = await _storageService.UploadFromBytesAsync(fileBytes, fileName, "restaurant_template");

                templateEntity.BackgroundImageUrl = imageUrl;
            }

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

        public async Task<MenuTemplateDto> UpdateTemplateAsync(int templateId, UpdateMenuTemplateDto request)
        {
            var template = await _unitOfWork.MenuTemplates.GetByIdAsync(templateId);
            if (template == null)
            {
                throw new Exception(MenuTemplateMessage.MenuTemplateError.TEMPLATE_NOT_FOUND);
            }

            template.TemplateName = request.TemplateName;
            template.ThemeColor = request.ThemeColor;
            template.FontFamily = request.FontFamily;
            template.BackgroundImageUrl = request.BackgroundImageUrl;
            template.LayoutConfigJson = request.LayoutConfigJson;

            _unitOfWork.MenuTemplates.Update(template);
            await _unitOfWork.SaveAsync();

            return _mapper.Map<MenuTemplateDto>(template);
        }

        public async Task<MenuTemplateRenderDto> GetRestaurantMenuFromTemplateAsync(int restaurantId, int templateId)
        {
            var template = await _unitOfWork.MenuTemplates.GetByIdAsync(templateId);
            if (template == null)
            {
                throw new Exception(MenuTemplateMessage.MenuTemplateError.TEMPLATE_NOT_FOUND);
            }

            var menuData = await _restaurantMenuService.GetMenuForRestaurantAsync(restaurantId);

            return new MenuTemplateRenderDto
            {
                TemplateId = template.Id,
                RestaurantId = restaurantId,
                LayoutConfigJson = template.LayoutConfigJson,
                MenuData = menuData
            };
        }
    }
}
