using AutoMapper;
using FluentAssertions;
using Moq;
using ScanToOrder.Application.DTOs.Dishes;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Application.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;

namespace ScanToOrder.Application.UnitTest.Services
{
    public class CategoryServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IAuthenticatedUserService> _mockAuthService;
        private readonly CategoryService _categoryService;

        private readonly Guid _validTenantId;
        private readonly Tenant _validTenant;
        private readonly Category _validCategory;
        private readonly CategoryDto _validCategoryDto;

        public CategoryServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
            _mockMapper = new Mock<IMapper>();
            _mockAuthService = new Mock<IAuthenticatedUserService>();

            _categoryService = new CategoryService(
                _mockUnitOfWork.Object,
                _mockMapper.Object,
                _mockAuthService.Object);

            _validTenantId = Guid.NewGuid();
            _validTenant = new Tenant { Id = _validTenantId };
            _validCategory = new Category { Id = 1, CategoryName = "Khai vị", TenantId = _validTenantId, IsActive = true, IsDeleted = false };
            _validCategoryDto = new CategoryDto { Id = 1, CategoryName = "Khai vị" };

            _mockAuthService.Setup(a => a.ProfileId).Returns(_validTenantId);
            _mockMapper.Setup(m => m.Map<CategoryDto>(It.IsAny<Category>())).Returns(_validCategoryDto);
        }

        #region 1. CreateCategory
        [Fact]
        public async Task CreateCategory_WhenCategoryNameAlreadyExists_ThrowsDomainException()
        {
            // Arrange
            var request = new CreateCategoryRequest { CategoryName = "Khai vị" };
            _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>()))
                .ReturnsAsync(_validCategory);

            // Act
            var action = async () => await _categoryService.CreateCategory(_validTenantId, request);

            // Assert
            await action.Should().ThrowAsync<DomainException>()
                .WithMessage(CategoryMessage.CategoryError.CATEGORY_ALREADY_EXISTS);
        }

        [Fact]
        public async Task CreateCategory_WhenTenantNotFound_ThrowsDomainException()
        {
            // Arrange
            var request = new CreateCategoryRequest { CategoryName = "Mới" };
            _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>()))
                .ReturnsAsync((Category)null);
            _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(_validTenantId))
                .ReturnsAsync((Tenant)null);

            // Act
            var action = async () => await _categoryService.CreateCategory(_validTenantId, request);

            // Assert
            await action.Should().ThrowAsync<DomainException>()
                .WithMessage(TenantMessage.TenantError.TENANT_NOT_FOUND);
        }

        [Fact]
        public async Task CreateCategory_WhenAllValid_ReturnsCreatedCategoryDto()
        {
            // Arrange
            var request = new CreateCategoryRequest { CategoryName = "Mới" };
            _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>())).ReturnsAsync((Category)null);
            _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(_validTenantId)).ReturnsAsync(_validTenant);
            _mockMapper.Setup(m => m.Map<Category>(request)).Returns(new Category());

            // Act
            await _categoryService.CreateCategory(_validTenantId, request);

            // Assert
            _mockUnitOfWork.Verify(u => u.Categories.AddAsync(It.IsAny<Category>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }
        #endregion

        #region 2. Permission & Auth Checks

        [Fact]
        public async Task ServiceMethods_WhenProfileIdIsNull_ThrowsDomainException()
        {
            // Arrange
            _mockAuthService.Setup(a => a.ProfileId).Returns((Guid?)null);

            // Act
            // Fix CS9035: Thêm CategoryName vào object initializer
            var actionUpdate = async () => await _categoryService.UpdateCategory(1, new UpdateCategoryRequest { CategoryName = "Test" });
            var actionDelete = async () => await _categoryService.DeleteCategory(1);

            // Assert
            await actionUpdate.Should().ThrowAsync<DomainException>().WithMessage(AuthMessage.AuthError.USER_PROFILE_NOT_FOUND);
            await actionDelete.Should().ThrowAsync<DomainException>().WithMessage(AuthMessage.AuthError.USER_PROFILE_NOT_FOUND);
        }

        [Fact]
        public async Task ServiceMethods_WhenTenantIdNotMatchProfileId_ThrowsDomainException()
        {
            // Arrange
            var differentTenantId = Guid.NewGuid();
            _mockAuthService.Setup(a => a.ProfileId).Returns(differentTenantId);
            _mockUnitOfWork.Setup(u => u.Categories.GetByIdAsync(1)).ReturnsAsync(_validCategory);

            // Act
            // Fix CS9035: Thêm CategoryName vào object initializer
            var actionUpdate = async () => await _categoryService.UpdateCategory(1, new UpdateCategoryRequest { CategoryName = "Test" });

            // Assert
            await actionUpdate.Should().ThrowAsync<DomainException>()
                .WithMessage(CategoryMessage.CategoryError.YOU_DONT_HAVE_PERMISSION);
        }

        #endregion

        #region 3. UpdateCategory
        [Fact]
        public async Task UpdateCategory_WhenCategoryNotFound_ThrowsDomainException()
        {
            // Arrange
            _mockUnitOfWork.Setup(u => u.Categories.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Category)null);

            // Act
            // Fix CS9035: Thêm CategoryName vào object initializer
            var action = async () => await _categoryService.UpdateCategory(1, new UpdateCategoryRequest { CategoryName = "Update" });

            // Assert
            await action.Should().ThrowAsync<DomainException>()
                .WithMessage(CategoryMessage.CategoryError.CATEGORY_NOT_FOUND);
        }

        [Fact]
        public async Task UpdateCategory_WhenValid_UpdatesName()
        {
            // Arrange
            _mockUnitOfWork.Setup(u => u.Categories.GetByIdAsync(1)).ReturnsAsync(_validCategory);

            // Act
            await _categoryService.UpdateCategory(1, new UpdateCategoryRequest { CategoryName = "Món mới" });

            // Assert
            _validCategory.CategoryName.Should().Be("Món mới");
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }
        #endregion

        #region 4. DeleteCategory
        [Fact]
        public async Task DeleteCategory_WhenValid_SoftDeletesAndClearsConfigs()
        {
            // Arrange
            var dishes = new List<Dish> { new Dish { Id = 10 } };
            var configs = new List<BranchDishConfig> { new BranchDishConfig() };

            _mockUnitOfWork.Setup(u => u.Categories.GetByIdAsync(1)).ReturnsAsync(_validCategory);
            _mockUnitOfWork.Setup(u => u.Dishes.FindAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(dishes);
            _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(configs);

            // Act
            var result = await _categoryService.DeleteCategory(1);

            // Assert
            result.Should().BeTrue();
            _validCategory.IsDeleted.Should().BeTrue();
            dishes[0].IsDeleted.Should().BeTrue();
            _mockUnitOfWork.Verify(u => u.BranchDishConfigs.RemoveRange(configs), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }
        #endregion

        #region 5. DeActive & Active Category
        [Fact]
        public async Task DeActiveCategory_WhenValid_SetsIsSellingFalse()
        {
            // Arrange
            var configs = new List<BranchDishConfig> { new BranchDishConfig { IsSelling = true } };
            _mockUnitOfWork.Setup(u => u.Categories.GetByIdAsync(1)).ReturnsAsync(_validCategory);
            _mockUnitOfWork.Setup(u => u.Dishes.FindAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(new List<Dish> { new Dish { Id = 10 } });
            _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(configs);

            // Act
            await _categoryService.DeActiveCategory(1);

            // Assert
            configs[0].IsSelling.Should().BeFalse();
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task ActiveCategory_WhenValid_SetsIsSellingTrue()
        {
            // Arrange
            var configs = new List<BranchDishConfig> { new BranchDishConfig { IsSelling = false } };
            _mockUnitOfWork.Setup(u => u.Categories.GetByIdAsync(1)).ReturnsAsync(_validCategory);
            _mockUnitOfWork.Setup(u => u.Dishes.FindAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(new List<Dish> { new Dish { Id = 10, IsDeleted = false, IsAvailable = true } });
            _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(configs);

            // Act
            await _categoryService.ActiveCategory(1);

            // Assert
            configs[0].IsSelling.Should().BeTrue();
            _mockUnitOfWork.Verify(u => u.BranchDishConfigs.UpdateRange(It.IsAny<IEnumerable<BranchDishConfig>>()), Times.Once);
        }
        #endregion

        #region 6. DTO Coverage 
        [Fact]
        public void CategoryDto_Properties_GetAndSetCorrectly()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var date = DateTime.UtcNow;

            // Act
            var dto = new CategoryDto
            {
                Id = 1,
                TenantId = tenantId,
                CategoryName = "Test",
                IsActive = true,
                CreatedAt = date
            };

            // Assert
            dto.Id.Should().Be(1);
            dto.TenantId.Should().Be(tenantId);
            dto.CategoryName.Should().Be("Test");
            dto.CreatedAt.Should().Be(date);
        }
        #endregion
    }
}