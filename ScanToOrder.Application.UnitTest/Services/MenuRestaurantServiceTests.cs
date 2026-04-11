using AutoMapper;
using FluentAssertions;
using Moq;
using ScanToOrder.Application.DTOs.Menu;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Menu;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Application.Message;
using System.Linq.Expressions;

namespace ScanToOrder.Application.UnitTest.Services
{
    public class MenuRestaurantServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IPlanLimitationService> _mockPlanLimitation;
        private readonly MenuRestaurantService _service;

        public MenuRestaurantServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
            _mockMapper = new Mock<IMapper>();
            _mockPlanLimitation = new Mock<IPlanLimitationService>();

            // Default: allow custom menu template for all restaurants
            _mockPlanLimitation
                .Setup(p => p.GetRestaurantFeaturesAsync(It.IsAny<int>()))
                .ReturnsAsync(new PlanFeaturesConfig { CanCustomMenuTemplate = true });

            _service = new MenuRestaurantService(_mockUnitOfWork.Object, _mockMapper.Object, _mockPlanLimitation.Object);
        }

        #region 1. GetMenuByRestaurantIdAsync

        [Fact]
        public async Task GetMenuByRestaurantIdAsync_WhenMenuNotFound_ThrowsDomainException()
        {
            // Arrange
            _mockUnitOfWork.Setup(u => u.MenuRestaurants.GetByFieldsIncludeAsync(
                It.IsAny<Expression<Func<MenuRestaurant, bool>>>(),
                It.IsAny<Expression<Func<MenuRestaurant, object>>[]>()))
                .ReturnsAsync((MenuRestaurant)null);

            // Act
            Func<Task> action = async () => await _service.GetMenuByRestaurantIdAsync(1);

            // Assert
            await action.Should().ThrowAsync<DomainException>()
                .WithMessage(MenuTemplateMessage.MenuTemplateError.MENU_RESTAURANT_NOT_FOUND);
        }

        [Fact]
        public async Task GetMenuByRestaurantIdAsync_WhenMenuExists_ReturnsDto()
        {
            // Arrange
            var existingMenu = new MenuRestaurant { RestaurantId = 1 };
            var expectedDto = new MenuRestaurantDto { RestaurantId = 1 };

            _mockUnitOfWork.Setup(u => u.MenuRestaurants.GetByFieldsIncludeAsync(
                It.IsAny<Expression<Func<MenuRestaurant, bool>>>(),
                It.IsAny<Expression<Func<MenuRestaurant, object>>[]>()))
                .ReturnsAsync(existingMenu);

            _mockMapper.Setup(m => m.Map<MenuRestaurantDto>(existingMenu)).Returns(expectedDto);

            // Act
            var result = await _service.GetMenuByRestaurantIdAsync(1);

            // Assert
            result.Should().BeEquivalentTo(expectedDto);
        }

        #endregion

        #region 2. ApplyRestaurantWithTemplateAsync

        [Fact]
        public async Task ApplyRestaurantWithTemplateAsync_WhenTemplateNotFound_ThrowsException()
        {
            // Arrange
            var request = new CreateMenuRestaurantRequestDto { TemplateId = 1, RestaurantId = 1 };

            _mockUnitOfWork.Setup(u => u.MenuTemplates.GetByIdAsync(request.TemplateId))
                .ReturnsAsync((MenuTemplate)null);

            // Act
            Func<Task> action = async () => await _service.ApplyRestaurantWithTemplateAsync(request);

            // Assert
            await action.Should().ThrowAsync<Exception>()
                .WithMessage(MenuTemplateMessage.MenuTemplateError.TEMPLATE_NOT_FOUND);
        }

        [Fact]
        public async Task ApplyRestaurantWithTemplateAsync_WhenExistingMenuFound_UpdatesAndReturnsDto()
        {
            // Arrange
            var request = new CreateMenuRestaurantRequestDto { TemplateId = 1, RestaurantId = 1 };
            var template = new MenuTemplate { Id = 1 };
            var existingMenu = new MenuRestaurant { RestaurantId = 1 };
            var expectedDto = new MenuRestaurantDto { RestaurantId = 1 };

            _mockUnitOfWork.Setup(u => u.MenuTemplates.GetByIdAsync(request.TemplateId))
                .ReturnsAsync(template);

            // Mock hàm FirstOrDefaultAsync
            _mockUnitOfWork.Setup(u => u.MenuRestaurants.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<MenuRestaurant, bool>>>(), It.IsAny<string>()))
                .ReturnsAsync(existingMenu);

            _mockMapper.Setup(m => m.Map(request, existingMenu)).Returns(existingMenu);
            _mockMapper.Setup(m => m.Map<MenuRestaurantDto>(existingMenu)).Returns(expectedDto);

            // Act
            var result = await _service.ApplyRestaurantWithTemplateAsync(request);

            // Assert
            _mockUnitOfWork.Verify(u => u.MenuRestaurants.Update(existingMenu), Times.Once); 
            _mockUnitOfWork.Verify(u => u.MenuRestaurants.AddAsync(It.IsAny<MenuRestaurant>()), Times.Never);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
            result.Should().Be(expectedDto);
        }

        [Fact]
        public async Task ApplyRestaurantWithTemplateAsync_WhenNoExistingMenu_AddsAndReturnsDto()
        {
            // Arrange
            var request = new CreateMenuRestaurantRequestDto { TemplateId = 1, RestaurantId = 1 };
            var template = new MenuTemplate { Id = 1 };
            var newMenu = new MenuRestaurant { RestaurantId = 1 };
            var expectedDto = new MenuRestaurantDto { RestaurantId = 1 };

            _mockUnitOfWork.Setup(u => u.MenuTemplates.GetByIdAsync(request.TemplateId))
                .ReturnsAsync(template);

            // Mock trả về null để ép hệ thống tạo mới
            _mockUnitOfWork.Setup(u => u.MenuRestaurants.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<MenuRestaurant, bool>>>(), It.IsAny<string>()))
                .ReturnsAsync((MenuRestaurant)null);

            _mockMapper.Setup(m => m.Map<MenuRestaurant>(request)).Returns(newMenu);
            _mockMapper.Setup(m => m.Map<MenuRestaurantDto>(newMenu)).Returns(expectedDto);

            // Act
            var result = await _service.ApplyRestaurantWithTemplateAsync(request);

            // Assert
            _mockUnitOfWork.Verify(u => u.MenuRestaurants.AddAsync(newMenu), Times.Once); 
            _mockUnitOfWork.Verify(u => u.MenuRestaurants.Update(It.IsAny<MenuRestaurant>()), Times.Never);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
            result.Should().Be(expectedDto);
        }

        #endregion
    }
}