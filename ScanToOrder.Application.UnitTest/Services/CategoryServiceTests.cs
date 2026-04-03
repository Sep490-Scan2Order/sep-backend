using AutoMapper;
using FluentAssertions;
using Moq;
using ScanToOrder.Application.DTOs.Dishes;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.User; 
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using System.Linq.Expressions;

namespace ScanToOrder.Application.UnitTest.Services;

public class CategoryServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IMapper> _mockMapper;
    private readonly CategoryService _categoryService;

    private readonly Guid _validTenantId;
    private readonly Tenant _validTenant;
    private readonly Category _validCategory;
    private readonly CategoryDto _validCategoryDto;

    public CategoryServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockMapper = new Mock<IMapper>();
        _categoryService = new CategoryService(_mockUnitOfWork.Object, _mockMapper.Object);

        _validTenantId = Guid.NewGuid();
        _validTenant = new Tenant { Id = _validTenantId }; 
        _validCategory = new Category { Id = 1, CategoryName = "Khai vị", TenantId = _validTenantId, IsActive = true, IsDeleted = false };
        _validCategoryDto = new CategoryDto { Id = 1, CategoryName = "Khai vị" };

        _mockMapper.Setup(m => m.Map<CategoryDto>(It.IsAny<Category>())).Returns(_validCategoryDto);
    }

    #region 1. CreateCategory
    [Fact]
    public async Task CreateCategory_WhenCategoryNameAlreadyExists_ThrowsDomainException()
    {
        var request = new CreateCategoryRequest { CategoryName = "Khai vị" };
        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>()))
            .ReturnsAsync(_validCategory);

        var action = async () => await _categoryService.CreateCategory(_validTenantId, request);

        await action.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CreateCategory_WhenCategoryNameIsNew_AndTenantNotFound_ThrowsDomainException()
    {
        var request = new CreateCategoryRequest { CategoryName = "Mới" };
        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>()))
            .ReturnsAsync((Category)null); 
        
       
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(_validTenantId))
            .ReturnsAsync((Tenant)null); 

        var action = async () => await _categoryService.CreateCategory(_validTenantId, request);
        await action.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CreateCategory_WhenAllValid_ReturnsCreatedCategoryDto()
    {
        var request = new CreateCategoryRequest { CategoryName = "Mới" };
        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>())).ReturnsAsync((Category)null);
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(_validTenantId)).ReturnsAsync(_validTenant);
        _mockMapper.Setup(m => m.Map<Category>(request)).Returns(new Category());

        await _categoryService.CreateCategory(_validTenantId, request);

        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
    }
    #endregion

    #region 2. GetAllCategoriesByTenant
    [Fact]
    public async Task GetAllCategoriesByTenant_WhenTenantNotFound_ThrowsDomainException()
    {
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Tenant)null);
        var action = async () => await _categoryService.GetAllCategoriesByTenant(Guid.NewGuid());
        await action.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task GetAllCategoriesByTenant_WhenTenantExists_ReturnsList()
    {
        // Arrange
        var categories = new List<Category> { _validCategory };
        var categoryDtos = new List<CategoryDto> { _validCategoryDto };

        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(_validTenantId)).ReturnsAsync(_validTenant);
        _mockUnitOfWork.Setup(u => u.Categories.FindAsync(It.IsAny<Expression<Func<Category, bool>>>())).ReturnsAsync(categories);
        
        _mockMapper.Setup(m => m.Map<List<CategoryDto>>(It.IsAny<List<Category>>())).Returns(categoryDtos);

        // Act
        var result = await _categoryService.GetAllCategoriesByTenant(_validTenantId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
    }
    #endregion

    #region 3. UpdateCategory
    [Fact]
    public async Task UpdateCategory_WhenCategoryNotFound_ThrowsDomainException()
    {
        _mockUnitOfWork.Setup(u => u.Categories.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Category)null);
        var action = async () => await _categoryService.UpdateCategory(1, new UpdateCategoryRequest { CategoryName = "Update" });
        await action.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task UpdateCategory_WhenExists_UpdatesName()
    {
        _mockUnitOfWork.Setup(u => u.Categories.GetByIdAsync(1)).ReturnsAsync(_validCategory);
        await _categoryService.UpdateCategory(1, new UpdateCategoryRequest { CategoryName = "Món mới" });
        _validCategory.CategoryName.Should().Be("Món mới");
    }
    #endregion

    #region 4. DeleteCategory
    [Fact]
    public async Task DeleteCategory_WhenCategoryNotFound_ThrowsDomainException()
    {
        _mockUnitOfWork.Setup(u => u.Categories.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Category)null);
        var action = async () => await _categoryService.DeleteCategory(1);
        await action.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task DeleteCategory_WhenHasDishes_CallsRemoveRange()
    {
        _mockUnitOfWork.Setup(u => u.Categories.GetByIdAsync(1)).ReturnsAsync(_validCategory);
        _mockUnitOfWork.Setup(u => u.Dishes.FindAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(new List<Dish> { new Dish { Id = 10 } });
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(new List<BranchDishConfig> { new() });

        await _categoryService.DeleteCategory(1);

        _mockUnitOfWork.Verify(u => u.BranchDishConfigs.RemoveRange(It.IsAny<IEnumerable<BranchDishConfig>>()), Times.Once);
    }
    #endregion

    #region 5. DeActiveCategory
    [Fact]
    public async Task DeActiveCategory_WhenExists_SetsIsSellingFalse()
    {
        var configs = new List<BranchDishConfig> { new BranchDishConfig { IsSelling = true } };
        _mockUnitOfWork.Setup(u => u.Categories.GetByIdAsync(1)).ReturnsAsync(_validCategory);
        _mockUnitOfWork.Setup(u => u.Dishes.FindAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(new List<Dish> { new Dish { Id = 10 } });
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(configs);

        await _categoryService.DeActiveCategory(1);

        configs[0].IsSelling.Should().BeFalse();
        _mockUnitOfWork.Verify(u => u.BranchDishConfigs.Update(It.IsAny<BranchDishConfig>()), Times.Once);
    }

    [Fact]
    public async Task DeActiveCategory_WhenCategoryNotFound_ThrowsDomainException()
    {
        // Arrange
        _mockUnitOfWork.Setup(u => u.Categories.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((Category)null);

        // Act
        var action = async () => await _categoryService.DeActiveCategory(1);

        // Assert
        await action.Should().ThrowAsync<DomainException>();
    }
    #endregion

    #region 6. ActiveCategory
    [Fact]
    public async Task ActiveCategory_WhenExists_SetsIsSellingTrue()
    {
        var configs = new List<BranchDishConfig> { new BranchDishConfig { IsSelling = false } };
        _mockUnitOfWork.Setup(u => u.Categories.GetByIdAsync(1)).ReturnsAsync(_validCategory);
        _mockUnitOfWork.Setup(u => u.Dishes.FindAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(new List<Dish> { new Dish { Id = 10 } });
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(configs);

        await _categoryService.ActiveCategory(1);

        configs[0].IsSelling.Should().BeTrue();
        _mockUnitOfWork.Verify(u => u.BranchDishConfigs.UpdateRange(It.IsAny<IEnumerable<BranchDishConfig>>()), Times.Once);
    }

    [Fact]
    public async Task ActiveCategory_WhenCategoryNotFound_ThrowsDomainException()
    {
        // Arrange
        _mockUnitOfWork.Setup(u => u.Categories.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((Category)null);

        // Act
        var action = async () => await _categoryService.ActiveCategory(1);

        // Assert
        await action.Should().ThrowAsync<DomainException>();
    }
    #endregion
}