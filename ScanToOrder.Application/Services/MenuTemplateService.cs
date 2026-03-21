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
        private readonly IGeminiService _geminiService;
        private readonly IHuggingFaceService _huggingFaceService;

        public MenuTemplateService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IStorageService storageService,
            IRestaurantMenuService restaurantMenuService,
            IGeminiService geminiService,
            IHuggingFaceService huggingFaceService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _storageService = storageService;
            _restaurantMenuService = restaurantMenuService;
            _geminiService = geminiService;
            _huggingFaceService = huggingFaceService;
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
            template.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.MenuTemplates.Update(template);
            await _unitOfWork.SaveAsync();

            return _mapper.Map<MenuTemplateDto>(template);
        }

        public async Task<MenuTemplateRenderDto> GetRestaurantMenuFromTemplateAsync(int restaurantId)
        {
            // Tìm mapping menu-template cho nhà hàng
            var menuRestaurant = await _unitOfWork.MenuRestaurants.GetByFieldsIncludeAsync(
                x => x.RestaurantId == restaurantId && !x.IsDeleted,
                x => x.MenuTemplate
            );

            if (menuRestaurant == null || menuRestaurant.MenuTemplate == null)
            {
                throw new Exception(MenuTemplateMessage.MenuTemplateError.MENU_RESTAURANT_NOT_FOUND);
            }

            var template = menuRestaurant.MenuTemplate;
            var menuData = await _restaurantMenuService.GetMenuForRestaurantAsync(restaurantId);

            return new MenuTemplateRenderDto
            {
                TemplateId = template.Id,
                RestaurantId = restaurantId,
                ThemeColor = template.ThemeColor,
                FontFamily = template.FontFamily,
                LayoutConfigJson = template.LayoutConfigJson,
                MenuData = menuData
            };
        }

        public async Task<AiHolidayTemplateResponseDto> GenerateHolidayThemeAsync(AiHolidayTemplateRequestDto request)
        {
            var visualConfig = await _geminiService.GenerateHolidayVisualConfigAsync(request.HolidayName);

            string uploadedImageUrl = "";

            if (!string.IsNullOrWhiteSpace(visualConfig.BackgroundImagePrompt))
            {
                try
                {
                    var imageBytes = await _huggingFaceService.GenerateImageBytesAsync(visualConfig.BackgroundImagePrompt);

                    var fileName = $"bg_{Guid.NewGuid():N}.png";

                    uploadedImageUrl = await _storageService.UploadFromBytesAsync(imageBytes, fileName, "templates");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AI Image Error]: {ex.Message}");
                    uploadedImageUrl = "";
                }
            }

            return new AiHolidayTemplateResponseDto
            {
                TemplateName = visualConfig.TemplateName,
                ThemeColor = visualConfig.ThemeColor,
                FontFamily = visualConfig.FontFamily,
                BackgroundColor = visualConfig.BackgroundColor ?? "#FFFFFF",
                BackgroundImageUrl = uploadedImageUrl,
                LayoutConfigJson = visualConfig.LayoutConfigJson 
            };
        }
    }
}
