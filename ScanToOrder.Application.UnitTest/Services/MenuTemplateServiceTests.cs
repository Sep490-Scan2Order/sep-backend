using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using ScanToOrder.Application.DTOs.Menu;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Menu;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace ScanToOrder.Application.UnitTest.Services
{
    public class MenuTemplateServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IStorageService> _mockStorageService;
        private readonly Mock<IRestaurantMenuService> _mockRestaurantMenuService;
        private readonly Mock<IGeminiService> _mockGeminiService;
        private readonly Mock<IHuggingFaceService> _mockHuggingFaceService;
        private readonly Mock<IPlanLimitationService> _mockPlanLimitationService;
        private readonly MenuTemplateService _service;

        public MenuTemplateServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
            _mockMapper = new Mock<IMapper>();
            _mockStorageService = new Mock<IStorageService>();
            _mockRestaurantMenuService = new Mock<IRestaurantMenuService>();
            _mockGeminiService = new Mock<IGeminiService>();
            _mockHuggingFaceService = new Mock<IHuggingFaceService>();
            _mockPlanLimitationService = new Mock<IPlanLimitationService>();

            _service = new MenuTemplateService(
                _mockUnitOfWork.Object,
                _mockMapper.Object,
                _mockStorageService.Object,
                _mockRestaurantMenuService.Object,
                _mockGeminiService.Object,
                _mockHuggingFaceService.Object,
                _mockPlanLimitationService.Object
            );
        }

        private Mock<IFormFile> CreateMockFile(string fileName = "test.jpg", long length = 100)
        {
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns(fileName);
            fileMock.Setup(f => f.Length).Returns(length);
            fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<Stream, CancellationToken>((stream, token) =>
                {
                    var bytes = new byte[length];
                    stream.Write(bytes, 0, bytes.Length);
                })
                .Returns(Task.CompletedTask);
            return fileMock;
        }

        #region 1. CreateTemplateAsync

        [Fact]
        public async Task CreateTemplateAsync_WhenImageIsNull_CreatesAndReturnsDto()
        {
            var request = new CreateTemplateRequestDto { BackgroundImageUrl = null };
            var entity = new MenuTemplate { Id = 1 };
            var responseDto = new CreateTemplateResponseDto { Id = 1 };

            _mockMapper.Setup(m => m.Map<MenuTemplate>(request)).Returns(entity);
            _mockMapper.Setup(m => m.Map<CreateTemplateResponseDto>(entity)).Returns(responseDto);

            var result = await _service.CreateTemplateAsync(request);

            result.Should().Be(responseDto);
            _mockUnitOfWork.Verify(u => u.MenuTemplates.AddAsync(entity), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
            _mockStorageService.Verify(s => s.UploadFromBytesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CreateTemplateAsync_WhenImageLengthIsZero_CreatesAndReturnsDto()
        {
            var fileMock = CreateMockFile("empty.jpg", 0);
            var request = new CreateTemplateRequestDto { BackgroundImageUrl = fileMock.Object };
            var entity = new MenuTemplate { Id = 1 };
            var responseDto = new CreateTemplateResponseDto { Id = 1 };

            _mockMapper.Setup(m => m.Map<MenuTemplate>(request)).Returns(entity);
            _mockMapper.Setup(m => m.Map<CreateTemplateResponseDto>(entity)).Returns(responseDto);

            var result = await _service.CreateTemplateAsync(request);

            result.Should().Be(responseDto);
            _mockUnitOfWork.Verify(u => u.MenuTemplates.AddAsync(entity), Times.Once);
            _mockStorageService.Verify(s => s.UploadFromBytesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CreateTemplateAsync_WhenImageIsValid_UploadsCreatesAndReturnsDto()
        {
            var fileMock = CreateMockFile("valid.jpg", 100);
            var request = new CreateTemplateRequestDto { BackgroundImageUrl = fileMock.Object };
            var entity = new MenuTemplate { Id = 1 };
            var responseDto = new CreateTemplateResponseDto { Id = 1 };

            _mockMapper.Setup(m => m.Map<MenuTemplate>(request)).Returns(entity);
            _mockStorageService.Setup(s => s.UploadFromBytesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                               .ReturnsAsync("uploaded_url.jpg");
            _mockMapper.Setup(m => m.Map<CreateTemplateResponseDto>(entity)).Returns(responseDto);

            var result = await _service.CreateTemplateAsync(request);

            result.Should().Be(responseDto);
            entity.BackgroundImageUrl.Should().Be("uploaded_url.jpg");
            _mockStorageService.Verify(s => s.UploadFromBytesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), "restaurant_template"), Times.Once);
            _mockUnitOfWork.Verify(u => u.MenuTemplates.AddAsync(entity), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        #endregion

        #region 2. GetTemplatesAsync

        [Fact]
        public async Task GetTemplatesAsync_ReturnsDtoList()
        {
            var templates = new List<MenuTemplate> { new MenuTemplate { Id = 1 } };
            var expectedDtos = new List<MenuTemplateDto> { new MenuTemplateDto { Id = 1 } };

            _mockUnitOfWork.Setup(u => u.MenuTemplates.GetAllAsync(
                It.IsAny<Expression<Func<MenuTemplate, bool>>>(),
                It.IsAny<Expression<Func<MenuTemplate, object>>[]>()))
                .ReturnsAsync(templates);

            _mockMapper.Setup(m => m.Map<IEnumerable<MenuTemplateDto>>(templates)).Returns(expectedDtos);

            var result = await _service.GetTemplatesAsync();

            result.Should().BeEquivalentTo(expectedDtos);
        }

        #endregion

        #region 3. GetTemplateByIdAsync

        [Fact]
        public async Task GetTemplateByIdAsync_WhenNotFound_ThrowsException()
        {
            _mockUnitOfWork.Setup(u => u.MenuTemplates.GetByIdAsync(It.IsAny<int>()))
                           .ReturnsAsync((MenuTemplate)null);

            Func<Task> action = async () => await _service.GetTemplateByIdAsync(1);

            await action.Should().ThrowAsync<Exception>()
                        .WithMessage(MenuTemplateMessage.MenuTemplateError.TEMPLATE_NOT_FOUND);
        }

        [Fact]
        public async Task GetTemplateByIdAsync_WhenFound_ReturnsDto()
        {
            var template = new MenuTemplate { Id = 1 };
            var expectedDto = new MenuTemplateDto { Id = 1 };

            _mockUnitOfWork.Setup(u => u.MenuTemplates.GetByIdAsync(1)).ReturnsAsync(template);
            _mockMapper.Setup(m => m.Map<MenuTemplateDto>(template)).Returns(expectedDto);

            var result = await _service.GetTemplateByIdAsync(1);

            result.Should().BeEquivalentTo(expectedDto);
        }

        #endregion

        #region 4. UpdateTemplateAsync

        [Fact]
        public async Task UpdateTemplateAsync_WhenNotFound_ThrowsException()
        {
            _mockUnitOfWork.Setup(u => u.MenuTemplates.GetByIdAsync(It.IsAny<int>()))
                           .ReturnsAsync((MenuTemplate)null);

            Func<Task> action = async () => await _service.UpdateTemplateAsync(1, new UpdateMenuTemplateDto());

            await action.Should().ThrowAsync<Exception>()
                        .WithMessage(MenuTemplateMessage.MenuTemplateError.TEMPLATE_NOT_FOUND);
        }

        [Fact]
        public async Task UpdateTemplateAsync_WhenFound_UpdatesAndReturnsDto()
        {
            var template = new MenuTemplate { Id = 1 };
            var request = new UpdateMenuTemplateDto
            {
                TemplateName = "New Name",
                ThemeColor = "#000",
                FontFamily = "Arial",
                BackgroundImageUrl = "bg.jpg",
                LayoutConfigJson = "{}"
            };
            var expectedDto = new MenuTemplateDto { Id = 1, TemplateName = "New Name" };

            _mockUnitOfWork.Setup(u => u.MenuTemplates.GetByIdAsync(1)).ReturnsAsync(template);
            _mockMapper.Setup(m => m.Map<MenuTemplateDto>(template)).Returns(expectedDto);

            var result = await _service.UpdateTemplateAsync(1, request);

            result.Should().BeEquivalentTo(expectedDto);
            template.TemplateName.Should().Be("New Name");
            _mockUnitOfWork.Verify(u => u.MenuTemplates.Update(template), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        #endregion

        #region 5. GetRestaurantMenuFromTemplateAsync

        [Fact]
        public async Task GetRestaurantMenuFromTemplateAsync_WhenMenuRestaurantNull_ThrowsException()
        {
            _mockUnitOfWork.Setup(u => u.MenuRestaurants.GetByFieldsIncludeAsync(
                It.IsAny<Expression<Func<MenuRestaurant, bool>>>(),
                It.IsAny<Expression<Func<MenuRestaurant, object>>[]>()))
                .ReturnsAsync((MenuRestaurant)null);

            Func<Task> action = async () => await _service.GetRestaurantMenuFromTemplateAsync(1);

            await action.Should().ThrowAsync<Exception>()
                        .WithMessage(MenuTemplateMessage.MenuTemplateError.MENU_RESTAURANT_NOT_FOUND);
        }

        [Fact]
        public async Task GetRestaurantMenuFromTemplateAsync_WhenMenuTemplateNull_ThrowsException()
        {
            var menuRestaurant = new MenuRestaurant { RestaurantId = 1, MenuTemplate = null };

            _mockUnitOfWork.Setup(u => u.MenuRestaurants.GetByFieldsIncludeAsync(
                It.IsAny<Expression<Func<MenuRestaurant, bool>>>(),
                It.IsAny<Expression<Func<MenuRestaurant, object>>[]>()))
                .ReturnsAsync(menuRestaurant);

            Func<Task> action = async () => await _service.GetRestaurantMenuFromTemplateAsync(1);

            await action.Should().ThrowAsync<Exception>()
                        .WithMessage(MenuTemplateMessage.MenuTemplateError.MENU_RESTAURANT_NOT_FOUND);
        }

        [Fact]
        public async Task GetRestaurantMenuFromTemplateAsync_WhenValid_ReturnsRenderDto()
        {
            var template = new MenuTemplate { Id = 1, ThemeColor = "#FFF", FontFamily = "Arial", LayoutConfigJson = "{}" };
            var menuRestaurant = new MenuRestaurant { RestaurantId = 1, MenuTemplate = template };
            var mockMenuData = new List<MenuCategoryDto>();

            _mockUnitOfWork.Setup(u => u.MenuRestaurants.GetByFieldsIncludeAsync(
                It.IsAny<Expression<Func<MenuRestaurant, bool>>>(),
                It.IsAny<Expression<Func<MenuRestaurant, object>>[]>()))
                .ReturnsAsync(menuRestaurant);

            _mockRestaurantMenuService.Setup(s => s.GetMenuForRestaurantAsync(1))
                                      .ReturnsAsync(mockMenuData);

            var result = await _service.GetRestaurantMenuFromTemplateAsync(1);

            result.Should().NotBeNull();
            result.TemplateId.Should().Be(1);
            result.RestaurantId.Should().Be(1);
            result.ThemeColor.Should().Be("#FFF");
            result.MenuData.Should().BeEquivalentTo(mockMenuData);
        }

        #endregion

        #region 6. GenerateHolidayThemeAsync

        [Fact]
        public async Task GenerateHolidayThemeAsync_WhenPromptIsEmpty_ReturnsDtoWithoutImage()
        {
            var request = new AiHolidayTemplateRequestDto { HolidayName = "Tet" };

            _mockGeminiService.Setup(g => g.GenerateHolidayVisualConfigAsync(request.HolidayName))
                              .ReturnsAsync(new ScanToOrder.Application.DTOs.Menu.AiHolidayVisualDto
                              {
                                  BackgroundImagePrompt = "",
                                  TemplateName = "Tet Theme",
                                  ThemeColor = "#FF0000",
                                  FontFamily = "Arial",
                                  BackgroundColor = "#FFF",
                                  LayoutConfigJson = "{}"
                              });

            var result = await _service.GenerateHolidayThemeAsync(request);

            result.Should().NotBeNull();
            result.BackgroundImageUrl.Should().BeEmpty();
            result.TemplateName.Should().Be("Tet Theme");

            _mockHuggingFaceService.Verify(h => h.GenerateImageBytesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GenerateHolidayThemeAsync_WhenPromptIsValid_UploadsImageAndReturnsDto()
        {
            var request = new AiHolidayTemplateRequestDto { HolidayName = "Tet" };
            var imageBytes = new byte[] { 1, 2, 3 };

            _mockGeminiService.Setup(g => g.GenerateHolidayVisualConfigAsync(request.HolidayName))
                              .ReturnsAsync(new ScanToOrder.Application.DTOs.Menu.AiHolidayVisualDto
                              {
                                  BackgroundImagePrompt = "Generate tet background",
                                  TemplateName = "Tet Theme",
                                  ThemeColor = "#FF0000",
                                  FontFamily = "Arial",
                                  BackgroundColor = null,
                                  LayoutConfigJson = "{}"
                              });

            _mockHuggingFaceService.Setup(h => h.GenerateImageBytesAsync("Generate tet background", It.IsAny<int>(), It.IsAny<int>()))
                                   .ReturnsAsync(imageBytes);

            _mockStorageService.Setup(s => s.UploadFromBytesAsync(imageBytes, It.IsAny<string>(), "templates"))
                               .ReturnsAsync("holiday_bg.png");

            var result = await _service.GenerateHolidayThemeAsync(request);

            result.Should().NotBeNull();
            result.BackgroundImageUrl.Should().Be("holiday_bg.png");
            result.BackgroundColor.Should().Be("#FFFFFF");
        }

        [Fact]
        public async Task GenerateHolidayThemeAsync_WhenHuggingFaceThrowsException_CatchesAndReturnsDtoWithoutImage()
        {
            var request = new AiHolidayTemplateRequestDto { HolidayName = "Tet" };

            _mockGeminiService.Setup(g => g.GenerateHolidayVisualConfigAsync(request.HolidayName))
                              .ReturnsAsync(new ScanToOrder.Application.DTOs.Menu.AiHolidayVisualDto
                              {
                                  BackgroundImagePrompt = "Generate tet background",
                                  TemplateName = "Tet Theme",
                                  ThemeColor = "#FF0000",
                                  FontFamily = "Arial",
                                  BackgroundColor = "#FFF",
                                  LayoutConfigJson = "{}"
                              });

            _mockHuggingFaceService.Setup(h => h.GenerateImageBytesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                                   .ThrowsAsync(new Exception("API Error"));

            var result = await _service.GenerateHolidayThemeAsync(request);

            result.Should().NotBeNull();
            result.BackgroundImageUrl.Should().Be("");
        }

        #endregion


        #region 8. GetTemplatesForRestaurantAsync

        [Fact]
        public async Task GetTemplatesForRestaurantAsync_WhenCannotCustom_ReturnsOnlyDefaultTemplates()
        {
            // Arrange
            int restaurantId = 1;
            var defaultTemplates = new List<MenuTemplate> { new MenuTemplate { Id = 1, IsDefault = true } };
            var expectedDtos = new List<MenuTemplateDto> { new MenuTemplateDto { Id = 1 } };

            _mockPlanLimitationService.Setup(p => p.GetRestaurantFeaturesAsync(restaurantId))
                .ReturnsAsync(new PlanFeaturesConfig { CanCustomMenuTemplate = false });

            _mockUnitOfWork.Setup(u => u.MenuTemplates.GetAllAsync(It.IsAny<Expression<Func<MenuTemplate, bool>>>(), It.IsAny<Expression<Func<MenuTemplate, object>>[]>()))
                .ReturnsAsync(defaultTemplates);

            _mockMapper.Setup(m => m.Map<IEnumerable<MenuTemplateDto>>(defaultTemplates)).Returns(expectedDtos);

            // Act
            var result = await _service.GetTemplatesForRestaurantAsync(restaurantId);

            // Assert
            result.Should().BeEquivalentTo(expectedDtos);
            _mockUnitOfWork.Verify(u => u.MenuTemplates.GetAllAsync(It.IsAny<Expression<Func<MenuTemplate, bool>>>(), It.IsAny<Expression<Func<MenuTemplate, object>>[]>()), Times.Once);
        }

        [Fact]
        public async Task GetTemplatesForRestaurantAsync_WhenCanCustom_ReturnsAllTemplates()
        {
            // Arrange
            int restaurantId = 1;
            var allTemplates = new List<MenuTemplate> { new MenuTemplate { Id = 1 }, new MenuTemplate { Id = 2 } };
            var expectedDtos = new List<MenuTemplateDto> { new MenuTemplateDto { Id = 1 }, new MenuTemplateDto { Id = 2 } };

            _mockPlanLimitationService.Setup(p => p.GetRestaurantFeaturesAsync(restaurantId))
                .ReturnsAsync(new PlanFeaturesConfig { CanCustomMenuTemplate = true });

            _mockUnitOfWork.Setup(u => u.MenuTemplates.GetAllAsync(It.IsAny<Expression<Func<MenuTemplate, bool>>>(), It.IsAny<Expression<Func<MenuTemplate, object>>[]>()))
                .ReturnsAsync(allTemplates);

            _mockMapper.Setup(m => m.Map<IEnumerable<MenuTemplateDto>>(allTemplates)).Returns(expectedDtos);

            // Act
            var result = await _service.GetTemplatesForRestaurantAsync(restaurantId);

            // Assert
            result.Should().BeEquivalentTo(expectedDtos);
        }

        #endregion
    }
}