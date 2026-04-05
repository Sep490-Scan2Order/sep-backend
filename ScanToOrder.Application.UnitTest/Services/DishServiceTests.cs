using AutoMapper;
using ClosedXML.Excel;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Moq;
using ScanToOrder.Application.DTOs.Dishes;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using System.Linq.Expressions;

namespace ScanToOrder.Application.UnitTest.Services;

public class DishServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IStorageService> _mockStorageService;
    private readonly Mock<IBranchDishConfigService> _mockBranchDishConfigService;
    private readonly Mock<IMenuCacheService> _mockMenuCacheService;
    private readonly Mock<IValidator<UpdateDishRequest>> _mockUpdateDishValidator;
    private readonly Mock<IDishRedisService> _mockDishRedisService;
    private readonly Mock<IBackgroundJobService> _mockBackgroundJobService;
    private readonly DishService _service;

    public DishServiceTests()
    {
        // Tự động mock tất cả các Repositories bên trong UnitOfWork
        _mockUnitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };

        _mockMapper = new Mock<IMapper>();
        _mockStorageService = new Mock<IStorageService>();
        _mockBranchDishConfigService = new Mock<IBranchDishConfigService>();
        _mockMenuCacheService = new Mock<IMenuCacheService>();
        _mockUpdateDishValidator = new Mock<IValidator<UpdateDishRequest>>();
        _mockDishRedisService = new Mock<IDishRedisService>();
        _mockBackgroundJobService = new Mock<IBackgroundJobService>();

        _service = new DishService(
            _mockUnitOfWork.Object,
            _mockMapper.Object,
            _mockStorageService.Object,
            _mockBranchDishConfigService.Object,
            _mockMenuCacheService.Object,
            _mockUpdateDishValidator.Object,
            _mockDishRedisService.Object,
            _mockBackgroundJobService.Object
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

    #region 1. CreateDish
    [Fact]
    public async Task CreateDish_TenantNotFound_ThrowsDomainException()
    {
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Tenant)null);
        var action = async () => await _service.CreateDish(Guid.NewGuid(), 1, new CreateDishRequest());
        await action.Should().ThrowAsync<DomainException>().WithMessage(TenantMessage.TenantError.TENANT_NOT_FOUND);
    }

    [Fact]
    public async Task CreateDish_CategoryNotFound_ThrowsDomainException()
    {
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<Expression<Func<Category, object>>[]>())).ReturnsAsync((Category)null);
        var action = async () => await _service.CreateDish(Guid.NewGuid(), 1, new CreateDishRequest());
        await action.Should().ThrowAsync<DomainException>().WithMessage(CategoryMessage.CategoryError.CATEGORY_NOT_FOUND);
    }

    [Fact]
    public async Task CreateDish_ImageUploadFails_ThrowsDomainException()
    {
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<Expression<Func<Category, object>>[]>())).ReturnsAsync(new Category());
        _mockStorageService.Setup(s => s.UploadFromBytesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("S3 Error"));

        var request = new CreateDishRequest { ImageUrl = CreateMockFile().Object };
        var action = async () => await _service.CreateDish(Guid.NewGuid(), 1, request);

        await action.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CreateDish_ValidRequest_NoImage_CreatesDishAndConfigs()
    {
        var request = new CreateDishRequest { DishName = "Test", Price = 100, Description = "Desc", ImageUrl = null };
        var dishEntity = new Dish { Id = 1, DishName = "Test" };
        var restaurants = new List<Restaurant> { new Restaurant { Id = 1, Slug = "slug-1" } };

        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<Expression<Func<Category, object>>[]>())).ReturnsAsync(new Category());
        _mockMapper.Setup(m => m.Map<Dish>(request)).Returns(dishEntity);
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(It.IsAny<Guid>())).ReturnsAsync(restaurants);
        _mockMapper.Setup(m => m.Map<DishDto>(It.IsAny<Dish>())).Returns(new DishDto());

        var result = await _service.CreateDish(Guid.NewGuid(), 1, request);

        result.Should().NotBeNull();
        _mockUnitOfWork.Verify(u => u.Dishes.AddAsync(It.IsAny<Dish>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.BranchDishConfigs.AddRangeAsync(It.IsAny<List<BranchDishConfig>>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Exactly(2));
        _mockBackgroundJobService.Verify(j => j.EnqueueSearchIndexDish(1), Times.Once);
    }

    [Fact]
    public async Task CreateDish_ValidRequest_WithImage_CreatesDish()
    {
        var request = new CreateDishRequest { DishName = "Test", Price = 100, ImageUrl = CreateMockFile().Object };
        var dishEntity = new Dish { Id = 1 };

        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<Expression<Func<Category, object>>[]>())).ReturnsAsync(new Category());
        _mockStorageService.Setup(s => s.UploadFromBytesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("url");
        _mockMapper.Setup(m => m.Map<Dish>(request)).Returns(dishEntity);
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(It.IsAny<Guid>())).ReturnsAsync(new List<Restaurant>());
        _mockMapper.Setup(m => m.Map<DishDto>(It.IsAny<Dish>())).Returns(new DishDto());

        var result = await _service.CreateDish(Guid.NewGuid(), 1, request);

        result.Should().NotBeNull();
        dishEntity.ImageUrl.Should().Be("url");
    }
    #endregion

    #region 2. CreateCombo
    [Fact]
    public async Task CreateCombo_TenantOrCategoryNotFound_ThrowsDomainException()
    {
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Tenant)null);
        await _service.Invoking(s => s.CreateCombo(Guid.NewGuid(), 1, new CreateComboRequest()))
            .Should().ThrowAsync<DomainException>().WithMessage(TenantMessage.TenantError.TENANT_NOT_FOUND);

        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<Expression<Func<Category, object>>[]>())).ReturnsAsync((Category)null);
        await _service.Invoking(s => s.CreateCombo(Guid.NewGuid(), 1, new CreateComboRequest()))
            .Should().ThrowAsync<DomainException>().WithMessage(CategoryMessage.CategoryError.CATEGORY_NOT_FOUND);
    }

    [Fact]
    public async Task CreateCombo_ItemsEmptyOrInvalidDishes_ThrowsDomainException()
    {
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<Expression<Func<Category, object>>[]>())).ReturnsAsync(new Category());

        var req1 = new CreateComboRequest { Items = null };
        await _service.Invoking(s => s.CreateCombo(Guid.NewGuid(), 1, req1))
            .Should().ThrowAsync<DomainException>().WithMessage("Combo phải có ít nhất 1 món ăn.");

        var req2 = new CreateComboRequest { Items = new List<ComboItemRequest> { new ComboItemRequest { DishId = 1 } } };
        _mockUnitOfWork.Setup(u => u.Dishes.FindAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(new List<Dish>());
        await _service.Invoking(s => s.CreateCombo(Guid.NewGuid(), 1, req2))
            .Should().ThrowAsync<DomainException>().WithMessage("Một hoặc nhiều món ăn không tồn tại.");

        var dishes = new List<Dish> { new Dish { Id = 1, Type = DishType.Combo } };
        _mockUnitOfWork.Setup(u => u.Dishes.FindAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(dishes);
        await _service.Invoking(s => s.CreateCombo(Guid.NewGuid(), 1, req2))
            .Should().ThrowAsync<DomainException>().WithMessage("Combo chỉ được bao gồm các món ăn lẻ (Single).");
    }

    [Fact]
    public async Task CreateCombo_ImageUploadFails_ThrowsDomainException()
    {
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<Expression<Func<Category, object>>[]>())).ReturnsAsync(new Category());
        _mockUnitOfWork.Setup(u => u.Dishes.FindAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(new List<Dish> { new Dish { Id = 1, Type = DishType.Single } });
        _mockStorageService.Setup(s => s.UploadFromBytesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new Exception("S3 error"));

        var req = new CreateComboRequest { Items = new List<ComboItemRequest> { new ComboItemRequest { DishId = 1 } }, ImageUrl = CreateMockFile().Object };
        await _service.Invoking(s => s.CreateCombo(Guid.NewGuid(), 1, req))
            .Should().ThrowAsync<DomainException>().WithMessage("Lỗi khi tải ảnh lên: S3 error");
    }

    [Fact]
    public async Task CreateCombo_ValidRequest_WithTransaction_CreatesCombo()
    {
        var req = new CreateComboRequest
        {
            ComboName = "Combo 1",
            Price = 100,
            Items = new List<ComboItemRequest>
            {
                new ComboItemRequest { DishId = 1, Quantity = 2 },
                new ComboItemRequest { DishId = 2, Quantity = 0 }
            }
        };
        var dishes = new List<Dish>
        {
            new Dish { Id = 1, Type = DishType.Single },
            new Dish { Id = 2, Type = DishType.Single }
        };

        var mockTx = new Mock<ScanToOrder.Domain.Interfaces.IDbTransaction>();

        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<Expression<Func<Category, object>>[]>())).ReturnsAsync(new Category());
        _mockUnitOfWork.Setup(u => u.Dishes.FindAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(dishes);
        _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(It.IsAny<Guid>())).ReturnsAsync(new List<Restaurant> { new Restaurant { Id = 1, Slug = "slug-1" } });
        _mockMapper.Setup(m => m.Map<DishDto>(It.IsAny<Dish>())).Returns(new DishDto());

        var result = await _service.CreateCombo(Guid.NewGuid(), 1, req);

        result.Should().NotBeNull();
        _mockUnitOfWork.Verify(u => u.Dishes.AddAsync(It.Is<Dish>(d => d.Type == DishType.Combo)), Times.Once);
        
        _mockUnitOfWork.Verify(u => u.ComboDetails.AddRangeAsync(It.IsAny<List<ComboDetail>>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.BranchDishConfigs.AddRangeAsync(It.IsAny<List<BranchDishConfig>>()), Times.Once);

        mockTx.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockBackgroundJobService.Verify(j => j.EnqueueSearchIndexDish(It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task CreateCombo_TransactionFails_RollbacksAndThrows()
    {
        var req = new CreateComboRequest { Items = new List<ComboItemRequest> { new ComboItemRequest { DishId = 1 } } };
        var mockTx = new Mock<ScanToOrder.Domain.Interfaces.IDbTransaction>();

        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<Expression<Func<Category, object>>[]>())).ReturnsAsync(new Category());
        _mockUnitOfWork.Setup(u => u.Dishes.FindAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(new List<Dish> { new Dish { Id = 1, Type = DishType.Single } });
        _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        _mockUnitOfWork.Setup(u => u.SaveAsync()).ThrowsAsync(new Exception("DB Error"));

        var action = async () => await _service.CreateCombo(Guid.NewGuid(), 1, req);

        await action.Should().ThrowAsync<Exception>();
        mockTx.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task CreateCombo_ValidRequestWithImage_UploadsSuccessfully()
    {
        var req = new CreateComboRequest
        {
            ComboName = "Combo Có Ảnh",
            Price = 100,
            Items = new List<ComboItemRequest> { new ComboItemRequest { DishId = 1, Quantity = 1 } },
            ImageUrl = CreateMockFile().Object 
        };
        
        var mockTx = new Mock<ScanToOrder.Domain.Interfaces.IDbTransaction>();

        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<Expression<Func<Category, object>>[]>())).ReturnsAsync(new Category());
        _mockUnitOfWork.Setup(u => u.Dishes.FindAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(new List<Dish> { new Dish { Id = 1, Type = DishType.Single } });
        
        _mockStorageService.Setup(s => s.UploadFromBytesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("combo_image_url_thanh_cong");
            
        _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(It.IsAny<Guid>())).ReturnsAsync(new List<Restaurant> { new Restaurant { Id = 1, Slug = "slug-1" } });
        _mockMapper.Setup(m => m.Map<DishDto>(It.IsAny<Dish>())).Returns(new DishDto());

        // Act
        var result = await _service.CreateCombo(Guid.NewGuid(), 1, req);

        // Assert
        result.Should().NotBeNull();
        _mockStorageService.Verify(s => s.UploadFromBytesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.Dishes.AddAsync(It.Is<Dish>(d => d.Type == DishType.Combo && d.ImageUrl == "combo_image_url_thanh_cong")), Times.Once);
    }
    #endregion

    #region 3. GetAllDishesByTenant
    [Fact]
    public async Task GetAllDishesByTenant_TenantNotFound_ThrowsDomainException()
    {
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Tenant)null);
        await _service.Invoking(s => s.GetAllDishesByTenant(Guid.NewGuid(), false))
            .Should().ThrowAsync<DomainException>().WithMessage(TenantMessage.TenantError.TENANT_NOT_FOUND);
    }

    [Fact]
    public async Task GetAllDishesByTenant_Valid_ReturnsList()
    {
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Dishes.GetAllDishesByTenant(It.IsAny<Guid>(), false)).ReturnsAsync(new List<Dish> { new Dish() });
        _mockMapper.Setup(m => m.Map<List<DishDto>>(It.IsAny<List<Dish>>())).Returns(new List<DishDto> { new DishDto() });

        var result = await _service.GetAllDishesByTenant(Guid.NewGuid());
        result.Should().HaveCount(1);
    }
    #endregion

    #region 4. UpdateDish
    [Fact]
    public async Task UpdateDish_ValidationFails_ThrowsValidationException()
    {
        var failures = new List<ValidationFailure> { new ValidationFailure("Prop", "Error") };
        _mockUpdateDishValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateDishRequest>(), default))
            .ReturnsAsync(new ValidationResult(failures));

        await _service.Invoking(s => s.UpdateDish(Guid.NewGuid(), 1, 1, new UpdateDishRequest()))
            .Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UpdateDish_TenantCategoryOrDishNotFound_ThrowsException()
    {
        _mockUpdateDishValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateDishRequest>(), default)).ReturnsAsync(new ValidationResult());

        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Tenant)null);
        await _service.Invoking(s => s.UpdateDish(Guid.NewGuid(), 1, 1, new UpdateDishRequest()))
            .Should().ThrowAsync<DomainException>().WithMessage(TenantMessage.TenantError.TENANT_NOT_FOUND);

        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<Expression<Func<Category, object>>[]>())).ReturnsAsync((Category)null);
        await _service.Invoking(s => s.UpdateDish(Guid.NewGuid(), 1, 1, new UpdateDishRequest()))
            .Should().ThrowAsync<DomainException>().WithMessage(CategoryMessage.CategoryError.CATEGORY_NOT_FOUND);

        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<Expression<Func<Category, object>>[]>())).ReturnsAsync(new Category());
        _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Dish, bool>>>(), It.IsAny<Expression<Func<Dish, object>>[]>())).ReturnsAsync((Dish)null);
        await _service.Invoking(s => s.UpdateDish(Guid.NewGuid(), 1, 1, new UpdateDishRequest()))
            .Should().ThrowAsync<DomainException>().WithMessage(DishMessage.DishError.DISH_NOT_FOUND);
    }

    [Fact]
    public async Task UpdateDish_ImageUploadFails_ThrowsDomainException()
    {
        _mockUpdateDishValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateDishRequest>(), default)).ReturnsAsync(new ValidationResult());
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<Expression<Func<Category, object>>[]>())).ReturnsAsync(new Category());

        var existingDish = new Dish { Category = new Category { TenantId = Guid.Empty } };
        _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Dish, bool>>>(), It.IsAny<Expression<Func<Dish, object>>[]>())).ReturnsAsync(existingDish);

        _mockStorageService.Setup(s => s.UploadFromBytesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new Exception("Upload Error"));

        var req = new UpdateDishRequest { ImageUrl = CreateMockFile().Object };
        await _service.Invoking(s => s.UpdateDish(Guid.Empty, 1, 1, req))
            .Should().ThrowAsync<DomainException>().WithMessage("Lỗi khi tải ảnh lên: Upload Error");
    }

    [Fact]
    public async Task UpdateDish_ValidRequest_UpdatesDishAndRedis()
    {
        _mockUpdateDishValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateDishRequest>(), default)).ReturnsAsync(new ValidationResult());
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<Expression<Func<Category, object>>[]>())).ReturnsAsync(new Category());

        var tenantId = Guid.NewGuid();
        var existingDish = new Dish { Id = 1, Price = 50, DishName = "Old", Description = "Old", Category = new Category { TenantId = tenantId } };
        _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Dish, bool>>>(), It.IsAny<Expression<Func<Dish, object>>[]>())).ReturnsAsync(existingDish);

        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>()))
            .ReturnsAsync(new List<BranchDishConfig> { new BranchDishConfig { RestaurantId = 10 } });
        _mockMapper.Setup(m => m.Map<DishDto>(It.IsAny<Dish>())).Returns(new DishDto());

        var req = new UpdateDishRequest { DishName = "New ", Price = 100, Description = "New Desc" };
        var result = await _service.UpdateDish(tenantId, 1, 1, req);

        existingDish.DishName.Should().Be("New");
        existingDish.Price.Should().Be(100);
        existingDish.Description.Should().Be("New Desc");

        _mockUnitOfWork.Verify(u => u.Dishes.Update(existingDish), Times.Once);
        _mockDishRedisService.Verify(r => r.SetDishPriceAsync(10, 1, 100), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        _mockBackgroundJobService.Verify(j => j.EnqueueSearchIndexDish(1), Times.Once);
    }
    
    [Fact]
    public async Task UpdateDish_ValidRequestWithImage_UploadsSuccessfully()
    {
        _mockUpdateDishValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateDishRequest>(), default)).ReturnsAsync(new ValidationResult());
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<Expression<Func<Category, object>>[]>())).ReturnsAsync(new Category());

        var existingDish = new Dish { Id = 1, Price = 50m, DishName = "Old", Description = "Old", ImageUrl = "old_url", Category = new Category { TenantId = Guid.Empty } };
        _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Dish, bool>>>(), It.IsAny<Expression<Func<Dish, object>>[]>())).ReturnsAsync(existingDish);
        
        _mockStorageService.Setup(s => s.UploadFromBytesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("new_url");
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(new List<BranchDishConfig>());
        _mockMapper.Setup(m => m.Map<DishDto>(It.IsAny<Dish>())).Returns(new DishDto());
        
        var req = new UpdateDishRequest { DishName = "New", Price = 100m, Description = "New Desc", ImageUrl = CreateMockFile().Object };
        
        await _service.UpdateDish(Guid.Empty, 1, 1, req);
        
        existingDish.ImageUrl.Should().Be("new_url");
        _mockStorageService.Verify(s => s.UploadFromBytesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UpdateDish_PriceIsNull_DoesNotUpdatePrice()
    {
        _mockUpdateDishValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateDishRequest>(), default)).ReturnsAsync(new ValidationResult());
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<Expression<Func<Category, object>>[]>())).ReturnsAsync(new Category());

        var existingDish = new Dish { Id = 1, Price = 50m, DishName = "Old", Category = new Category { TenantId = Guid.Empty } };
        _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Dish, bool>>>(), It.IsAny<Expression<Func<Dish, object>>[]>())).ReturnsAsync(existingDish);
        
        var req = new UpdateDishRequest { Price = null, DishName = "New Name" };
        
        await _service.UpdateDish(Guid.Empty, 1, 1, req);
        
        existingDish.Price.Should().Be(50m); 
        _mockDishRedisService.Verify(r => r.SetDishPriceAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
    }
    #endregion

    #region 5. Delete, DeActive, Active
    
    // Đã bổ sung cover 100% cho Null và Wrong Tenant
    [Fact]
    public async Task DeleteDish_NotFoundOrWrongTenant_ThrowsDomainException()
    {
        var targetTenantId = Guid.NewGuid();
        
        // Null dish
        _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Dish, bool>>>(), It.IsAny<Expression<Func<Dish, object>>[]>())).ReturnsAsync((Dish)null);
        await _service.Invoking(s => s.DeleteDish(targetTenantId, 1, 1)).Should().ThrowAsync<DomainException>().WithMessage(DishMessage.DishError.DISH_NOT_FOUND);

        // Wrong tenant
        var wrongTenantDish = new Dish { Category = new Category { TenantId = Guid.NewGuid() } };
        _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Dish, bool>>>(), It.IsAny<Expression<Func<Dish, object>>[]>())).ReturnsAsync(wrongTenantDish);
        await _service.Invoking(s => s.DeleteDish(targetTenantId, 1, 1)).Should().ThrowAsync<DomainException>().WithMessage(DishMessage.DishError.DISH_NOT_FOUND);
    }

    [Fact]
    public async Task DeleteDish_AlreadyDeleted_ReturnsTrue()
    {
        var tenantId = Guid.NewGuid();
        var dish = new Dish { IsDeleted = true, Category = new Category { TenantId = tenantId } };
        _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Dish, bool>>>(), It.IsAny<Expression<Func<Dish, object>>[]>())).ReturnsAsync(dish);
        var result = await _service.DeleteDish(tenantId, 1, 1);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteDish_Valid_UpdatesAndRemovesConfigs()
    {
        var tenantId = Guid.NewGuid();
        var dish = new Dish { Id = 1, IsDeleted = false, Category = new Category { TenantId = tenantId } };
        var configs = new List<BranchDishConfig> { new BranchDishConfig() };

        _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Dish, bool>>>(), It.IsAny<Expression<Func<Dish, object>>[]>())).ReturnsAsync(dish);
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(configs);

        var result = await _service.DeleteDish(tenantId, 1, 1);

        result.Should().BeTrue();
        dish.IsDeleted.Should().BeTrue();
        _mockUnitOfWork.Verify(u => u.BranchDishConfigs.RemoveRange(configs), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
    }

    // Đã bổ sung cover 100% cho Null và Wrong Tenant
    [Fact]
    public async Task DeActiveDish_NotFoundOrWrongTenant_ThrowsDomainException()
    {
        var targetTenantId = Guid.NewGuid();
        
        // Null dish
        _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Dish, bool>>>(), It.IsAny<Expression<Func<Dish, object>>[]>())).ReturnsAsync((Dish)null);
        await _service.Invoking(s => s.DeActiveDish(targetTenantId, 1, 1)).Should().ThrowAsync<DomainException>().WithMessage(DishMessage.DishError.DISH_NOT_FOUND);

        // Wrong tenant
        var wrongTenantDish = new Dish { Category = new Category { TenantId = Guid.NewGuid() } };
        _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Dish, bool>>>(), It.IsAny<Expression<Func<Dish, object>>[]>())).ReturnsAsync(wrongTenantDish);
        await _service.Invoking(s => s.DeActiveDish(targetTenantId, 1, 1)).Should().ThrowAsync<DomainException>().WithMessage(DishMessage.DishError.DISH_NOT_FOUND);
    }

    [Fact]
    public async Task DeActiveDish_Valid_UpdatesConfigsSellingFalse()
    {
        var tenantId = Guid.NewGuid();
        var dish = new Dish { Id = 1, IsAvailable = true, Category = new Category { TenantId = tenantId } };
        var configs = new List<BranchDishConfig> { new BranchDishConfig { IsSelling = true } };

        _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Dish, bool>>>(), It.IsAny<Expression<Func<Dish, object>>[]>())).ReturnsAsync(dish);
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(configs);

        var result = await _service.DeActiveDish(tenantId, 1, 1);

        result.Should().BeTrue();
        dish.IsAvailable.Should().BeFalse();
        configs.First().IsSelling.Should().BeFalse();
        _mockUnitOfWork.Verify(u => u.BranchDishConfigs.UpdateRange(configs), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
    }

    // Đã bổ sung cover 100% cho Null và Wrong Tenant
    [Fact]
    public async Task ActiveDish_NotFoundOrWrongTenant_ThrowsDomainException()
    {
        var targetTenantId = Guid.NewGuid();
        
        // Null dish
        _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Dish, bool>>>(), It.IsAny<Expression<Func<Dish, object>>[]>())).ReturnsAsync((Dish)null);
        await _service.Invoking(s => s.ActiveDish(targetTenantId, 1, 1)).Should().ThrowAsync<DomainException>().WithMessage(DishMessage.DishError.DISH_NOT_FOUND);

        // Wrong tenant
        var wrongTenantDish = new Dish { Category = new Category { TenantId = Guid.NewGuid() } };
        _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Dish, bool>>>(), It.IsAny<Expression<Func<Dish, object>>[]>())).ReturnsAsync(wrongTenantDish);
        await _service.Invoking(s => s.ActiveDish(targetTenantId, 1, 1)).Should().ThrowAsync<DomainException>().WithMessage(DishMessage.DishError.DISH_NOT_FOUND);
    }

    [Fact]
    public async Task ActiveDish_Valid_UpdatesConfigsSellingTrue()
    {
        var tenantId = Guid.NewGuid();
        var dish = new Dish { Id = 1, IsAvailable = false, Category = new Category { TenantId = tenantId } };
        var configs = new List<BranchDishConfig> { new BranchDishConfig { IsSelling = false } };

        _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Dish, bool>>>(), It.IsAny<Expression<Func<Dish, object>>[]>())).ReturnsAsync(dish);
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(configs);

        var result = await _service.ActiveDish(tenantId, 1, 1);

        result.Should().BeTrue();
        dish.IsAvailable.Should().BeTrue();
        configs.First().IsSelling.Should().BeTrue();
        _mockUnitOfWork.Verify(u => u.BranchDishConfigs.UpdateRange(configs), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
    }
    #endregion

    #region 6. GetComboById
    
    // Đã bổ sung cover 100% trường hợp Null list và Empty list
    [Fact]
    public async Task GetComboById_ComboDetailsNull_ThrowsDomainException()
    {
        _mockUnitOfWork.Setup(u => u.ComboDetails.GetAllAsync(It.IsAny<Expression<Func<ComboDetail, bool>>>(), It.IsAny<Expression<Func<ComboDetail, object>>[]>()))
            .ReturnsAsync((List<ComboDetail>)null);

        await _service.Invoking(s => s.GetComboById(1))
            .Should().ThrowAsync<DomainException>().WithMessage(DishMessage.DishError.DISH_COMBO_NOT_FOUND);
    }

    [Fact]
    public async Task GetComboById_ComboDetailsEmpty_ThrowsDomainException()
    {
        _mockUnitOfWork.Setup(u => u.ComboDetails.GetAllAsync(It.IsAny<Expression<Func<ComboDetail, bool>>>(), It.IsAny<Expression<Func<ComboDetail, object>>[]>()))
            .ReturnsAsync(new List<ComboDetail>());

        await _service.Invoking(s => s.GetComboById(1))
            .Should().ThrowAsync<DomainException>().WithMessage(DishMessage.DishError.DISH_COMBO_NOT_FOUND);
    }

    [Fact]
    public async Task GetComboById_Valid_ReturnsList()
    {
        var details = new List<ComboDetail> { new ComboDetail { Quantity = 2, ItemDish = new Dish() } };
        _mockUnitOfWork.Setup(u => u.ComboDetails.GetAllAsync(It.IsAny<Expression<Func<ComboDetail, bool>>>(), It.IsAny<Expression<Func<ComboDetail, object>>[]>()))
            .ReturnsAsync(details);
        _mockMapper.Setup(m => m.Map<DishDto>(It.IsAny<Dish>())).Returns(new DishDto());

        var result = await _service.GetComboById(1);
        result.Should().HaveCount(1);
        result[0].Quantity.Should().Be(2);
    }
    #endregion

    #region 7. ImportDishesFromExcelAsync
    private Mock<IFormFile> CreateMockExcelFile(Action<IXLWorksheet> populateData)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Sheet1");

        ws.Cell(1, 1).Value = "CategoryName";
        ws.Cell(1, 2).Value = "DishName";
        ws.Cell(1, 3).Value = "Price";
        ws.Cell(1, 4).Value = "Description";
        ws.Cell(1, 5).Value = "DishType";
        ws.Cell(1, 6).Value = "ComboItems";

        populateData(ws);

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, CancellationToken>((target, token) =>
            {
                ms.Position = 0;
                ms.CopyTo(target);
            }).Returns(Task.CompletedTask);

        return mockFile;
    }

    [Fact]
    public async Task ImportDishes_TenantOrRestaurantNotFound_ThrowsException()
    {
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Tenant)null);
        await _service.Invoking(s => s.ImportDishesFromExcelAsync(Guid.NewGuid(), CreateMockExcelFile(ws => { }).Object))
            .Should().ThrowAsync<DomainException>().WithMessage(TenantMessage.TenantError.TENANT_NOT_FOUND);

        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(It.IsAny<Guid>())).ReturnsAsync(new List<Restaurant>());
        await _service.Invoking(s => s.ImportDishesFromExcelAsync(Guid.NewGuid(), CreateMockExcelFile(ws => { }).Object))
            .Should().ThrowAsync<DomainException>().WithMessage(RestaurantMessage.RestaurantError.NOT_FOUND_RESTAURANT_FOR_USER);
    }

    [Fact]
    public async Task ImportDishes_EmptyExcel_ReturnsZero()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(tenantId)).ReturnsAsync(new List<Restaurant> { new Restaurant { Slug = "slug-1" } });
        _mockUnitOfWork.Setup(u => u.Categories.GetAllCategoriesByTenant(tenantId)).ReturnsAsync(new List<Category>());
        _mockUnitOfWork.Setup(u => u.Dishes.GetAllDishesByTenant(tenantId, true)).ReturnsAsync(new List<Dish>());

        var mockFile = CreateMockExcelFile(ws => { ws.Clear(); });
        var result = await _service.ImportDishesFromExcelAsync(tenantId, mockFile.Object);
        result.Should().Be(0);
    }

    [Fact]
    public async Task ImportDishes_ComplexExcel_CoversAllLogicBranches()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant());

        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(tenantId)).ReturnsAsync(new List<Restaurant> {
            new Restaurant { Id = 1, Slug = "slug-1" },
            new Restaurant { Id = 2, Slug = "slug-2" }
        });

        var existingCategories = new List<Category> { new Category { Id = 1, CategoryName = "Drinks" } };
        _mockUnitOfWork.Setup(u => u.Categories.GetAllCategoriesByTenant(tenantId)).ReturnsAsync(existingCategories);

        var existingDishes = new List<Dish>
        {
            new Dish { Id = 1, CategoryId = 1, DishName = "Coke", Price = 10m, Type = DishType.Single },
            new Dish { Id = 2, CategoryId = 1, DishName = "Pepsi Combo", Price = 20m, Type = DishType.Combo }
        };
        _mockUnitOfWork.Setup(u => u.Dishes.GetAllDishesByTenant(tenantId, true)).ReturnsAsync(existingDishes);

        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>()))
            .ReturnsAsync(new List<BranchDishConfig> { new BranchDishConfig { RestaurantId = 1, DishId = 1 } });

        _mockUnitOfWork.Setup(u => u.ComboDetails.FindAsync(It.IsAny<Expression<Func<ComboDetail, bool>>>())).ReturnsAsync(new List<ComboDetail>());

        var mockFile = CreateMockExcelFile(ws =>
        {
            ws.Cell(2, 1).Value = "  "; ws.Cell(2, 2).Value = "  ";
            ws.Cell(3, 1).Value = "Food"; ws.Cell(3, 2).Value = "Burger"; ws.Cell(3, 3).Value = 50m; ws.Cell(3, 4).Value = "Good"; ws.Cell(3, 5).Value = "Single";
            ws.Cell(4, 1).Value = "Drinks"; ws.Cell(4, 2).Value = "Coke"; ws.Cell(4, 3).Value = 15m;
            ws.Cell(5, 1).Value = "ComboCat"; ws.Cell(5, 2).Value = "Super Combo"; ws.Cell(5, 3).Value = 100m; ws.Cell(5, 5).Value = "Combo";
            ws.Cell(5, 6).Value = "Drinks|Coke:2; Coke:1";
            ws.Cell(6, 1).Value = "Drinks"; ws.Cell(6, 2).Value = "Pepsi Combo"; ws.Cell(6, 3).Value = 25m; ws.Cell(6, 5).Value = "Combo"; ws.Cell(6, 6).Value = "Coke:1";
        });

        _mockUnitOfWork.Setup(u => u.Categories.AddAsync(It.IsAny<Category>())).Callback<Category>(c => c.Id = 99);
        _mockUnitOfWork.Setup(u => u.Dishes.AddAsync(It.IsAny<Dish>())).Callback<Dish>(d => d.Id = 99);

        var result = await _service.ImportDishesFromExcelAsync(tenantId, mockFile.Object);

        result.Should().BeGreaterThan(0);
        _mockUnitOfWork.Verify(u => u.Dishes.AddAsync(It.IsAny<Dish>()), Times.Exactly(2));
        _mockUnitOfWork.Verify(u => u.Dishes.Update(It.IsAny<Dish>()), Times.Exactly(2));
        _mockUnitOfWork.Verify(u => u.BranchDishConfigs.AddRangeAsync(It.IsAny<List<BranchDishConfig>>()), Times.AtLeastOnce());
        _mockUnitOfWork.Verify(u => u.ComboDetails.AddRangeAsync(It.IsAny<List<ComboDetail>>()), Times.AtLeastOnce());
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.AtLeastOnce());
        _mockMenuCacheService.Verify(c => c.InvalidateMenuAsync(It.IsAny<int>()), Times.AtLeastOnce());
    }

    [Theory]
    [InlineData("Cat", "Combo", "InvalidQtyFormat")]
    [InlineData("Cat", "Combo", "Coke:0")]
    [InlineData("Cat", "Combo", "Coke:abc")]
    [InlineData("Cat", "Combo", ":2")]
    [InlineData("Cat", "Combo", "GhostDish:1")]
    public async Task ImportDishes_InvalidComboItems_ThrowsDomainException(string cat, string dish, string comboItems)
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(tenantId)).ReturnsAsync(new List<Restaurant> { new Restaurant { Slug = "slug-1" } });
        _mockUnitOfWork.Setup(u => u.Categories.GetAllCategoriesByTenant(tenantId)).ReturnsAsync(new List<Category>());
        _mockUnitOfWork.Setup(u => u.Dishes.GetAllDishesByTenant(tenantId, true)).ReturnsAsync(new List<Dish>());

        var mockFile = CreateMockExcelFile(ws =>
        {
            ws.Cell(2, 1).Value = cat;
            ws.Cell(2, 2).Value = dish;
            ws.Cell(2, 3).Value = 10m;
            ws.Cell(2, 5).Value = "Combo";
            ws.Cell(2, 6).Value = comboItems;
        });

        await _service.Invoking(s => s.ImportDishesFromExcelAsync(tenantId, mockFile.Object))
            .Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task ImportDishes_ComboItemNotSingle_ThrowsDomainException()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(tenantId)).ReturnsAsync(new List<Restaurant> { new Restaurant { Slug = "slug-1" } });
        _mockUnitOfWork.Setup(u => u.Categories.GetAllCategoriesByTenant(tenantId)).ReturnsAsync(new List<Category> { new Category { Id = 1, CategoryName = "Cat" } });
        _mockUnitOfWork.Setup(u => u.Dishes.GetAllDishesByTenant(tenantId, true)).ReturnsAsync(new List<Dish>
        {
            new Dish { Id = 1, CategoryId = 1, DishName = "ExistingCombo", Type = DishType.Combo }
        });

        var mockFile = CreateMockExcelFile(ws =>
        {
            ws.Cell(2, 1).Value = "NewCat";
            ws.Cell(2, 2).Value = "NewCombo";
            ws.Cell(2, 3).Value = 10m; 
            ws.Cell(2, 5).Value = "Combo";
            ws.Cell(2, 6).Value = "Cat|ExistingCombo:1";
        });

        await _service.Invoking(s => s.ImportDishesFromExcelAsync(tenantId, mockFile.Object))
            .Should().ThrowAsync<DomainException>().WithMessage("*chỉ được bao gồm Single dishes*");
    }

    [Fact]
    public async Task ImportDishes_DuplicateDishNamesAcrossCategories_ThrowsDomainException()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(tenantId)).ReturnsAsync(new List<Restaurant> { new Restaurant { Slug = "slug-1" } });
        _mockUnitOfWork.Setup(u => u.Categories.GetAllCategoriesByTenant(tenantId)).ReturnsAsync(new List<Category>());

        _mockUnitOfWork.Setup(u => u.Dishes.GetAllDishesByTenant(tenantId, true)).ReturnsAsync(new List<Dish>
        {
            new Dish { Id = 1, CategoryId = 1, DishName = "Coke", Type = DishType.Single },
            new Dish { Id = 2, CategoryId = 2, DishName = "Coke", Type = DishType.Single }
        });

        var mockFile = CreateMockExcelFile(ws =>
        {
            ws.Cell(2, 1).Value = "NewCat";
            ws.Cell(2, 2).Value = "NewCombo";
            ws.Cell(2, 3).Value = 10m;
            ws.Cell(2, 5).Value = "Combo";
            ws.Cell(2, 6).Value = "Coke:1";
        });

        await _service.Invoking(s => s.ImportDishesFromExcelAsync(tenantId, mockFile.Object))
            .Should().ThrowAsync<DomainException>().WithMessage("*trùng nhiều dish*");
    }
    #endregion

    #region 8. Additional Tests for 100% Coverage (Red & Yellow Lines)

    [Fact]
    public async Task CreateCombo_DishesReturnsNull_ThrowsDomainException()
    {
        var req = new CreateComboRequest { Items = new List<ComboItemRequest> { new ComboItemRequest { DishId = 1 } } };
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<Expression<Func<Category, object>>[]>())).ReturnsAsync(new Category());
        
        _mockUnitOfWork.Setup(u => u.Dishes.FindAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync((List<Dish>)null);

        await _service.Invoking(s => s.CreateCombo(Guid.NewGuid(), 1, req))
            .Should().ThrowAsync<DomainException>().WithMessage("Một hoặc nhiều món ăn không tồn tại.");
    }

    [Fact]
    public async Task UpdateDish_NoFieldsChanged_SkipsPriceAndNameUpdate()
    {
        _mockUpdateDishValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateDishRequest>(), default)).ReturnsAsync(new ValidationResult());
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Categories.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<Expression<Func<Category, object>>[]>())).ReturnsAsync(new Category());

        var existingDish = new Dish { Id = 1, Price = 50m, DishName = "Old", Category = new Category { TenantId = Guid.Empty } };
        _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Dish, bool>>>(), It.IsAny<Expression<Func<Dish, object>>[]>())).ReturnsAsync(existingDish);

        var req = new UpdateDishRequest { Price = 50m, DishName = null, ImageUrl = null };
        var result = await _service.UpdateDish(Guid.Empty, 1, 1, req);

        existingDish.DishName.Should().Be("Old"); 
        _mockDishRedisService.Verify(r => r.SetDishPriceAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never); 
    }

    [Fact]
    public async Task ImportDishes_RestaurantsIsNull_ThrowsDomainException()
    {
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(It.IsAny<Guid>())).ReturnsAsync((List<Restaurant>)null);

        await _service.Invoking(s => s.ImportDishesFromExcelAsync(Guid.NewGuid(), CreateMockExcelFile(ws => { }).Object))
            .Should().ThrowAsync<DomainException>().WithMessage(RestaurantMessage.RestaurantError.NOT_FOUND_RESTAURANT_FOR_USER);
    }

    [Fact]
    public async Task ImportDishes_ComboItemsHasEmptyParts_SkipsEmptyParts()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(tenantId)).ReturnsAsync(new List<Restaurant> { new Restaurant { Slug = "slug-1" } });
        _mockUnitOfWork.Setup(u => u.Categories.GetAllCategoriesByTenant(tenantId)).ReturnsAsync(new List<Category>());
        _mockUnitOfWork.Setup(u => u.Dishes.GetAllDishesByTenant(tenantId, true)).ReturnsAsync(new List<Dish> 
        { 
            new Dish { Id = 1, DishName = "Coke", CategoryId = 1, Type = DishType.Single } 
        });

        var mockFile = CreateMockExcelFile(ws =>
        {
            ws.Cell(2, 1).Value = "Cat"; ws.Cell(2, 2).Value = "Combo"; ws.Cell(2, 3).Value = 10m; ws.Cell(2, 5).Value = "Combo";
            ws.Cell(2, 6).Value = "Coke:1 ;   ; "; 
        });

        var result = await _service.ImportDishesFromExcelAsync(tenantId, mockFile.Object);
        _mockUnitOfWork.Verify(u => u.ComboDetails.AddRangeAsync(It.Is<List<ComboDetail>>(cd => cd.Count == 1)), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ImportDishes_ComboItemMissingDishName_ThrowsException()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(tenantId)).ReturnsAsync(new List<Restaurant> { new Restaurant { Slug = "slug-1" } });
        _mockUnitOfWork.Setup(u => u.Categories.GetAllCategoriesByTenant(tenantId)).ReturnsAsync(new List<Category>());
        _mockUnitOfWork.Setup(u => u.Dishes.GetAllDishesByTenant(tenantId, true)).ReturnsAsync(new List<Dish>());

        var mockFile = CreateMockExcelFile(ws =>
        {
            ws.Cell(2, 1).Value = "Cat"; 
            ws.Cell(2, 2).Value = "Combo"; 
            ws.Cell(2, 3).Value = 10m; 
            ws.Cell(2, 5).Value = "Combo";
    
            // FIX CUỐI CÙNG: Dùng chuỗi ": :1" để lọt qua hàm Split và Trim một cách hoàn hảo
            ws.Cell(2, 6).Value = ": :1"; 
        });

        await _service.Invoking(s => s.ImportDishesFromExcelAsync(tenantId, mockFile.Object))
            .Should().ThrowAsync<DomainException>().WithMessage("*invalid item*");
    }

    [Fact]
    public async Task ImportDishes_ExistingDishNoChanges_SkipsUpdate()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(tenantId)).ReturnsAsync(new List<Restaurant> { new Restaurant { Slug = "slug-1" } });
        _mockUnitOfWork.Setup(u => u.Categories.GetAllCategoriesByTenant(tenantId)).ReturnsAsync(new List<Category> { new Category { Id = 1, CategoryName = "Cat" } });
        
        var existingDish = new Dish { Id = 1, CategoryId = 1, DishName = "Coke", Price = 10m, Description = "Desc", IsAvailable = true, IsDeleted = false, Type = DishType.Single };
        _mockUnitOfWork.Setup(u => u.Dishes.GetAllDishesByTenant(tenantId, true)).ReturnsAsync(new List<Dish> { existingDish });
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(new List<BranchDishConfig>());

        var mockFile = CreateMockExcelFile(ws =>
        {
            ws.Cell(2, 1).Value = "Cat"; ws.Cell(2, 2).Value = "Coke"; ws.Cell(2, 3).Value = 10m; ws.Cell(2, 4).Value = "Desc"; ws.Cell(2, 5).Value = "Single";
        });

        await _service.ImportDishesFromExcelAsync(tenantId, mockFile.Object);

        _mockUnitOfWork.Verify(u => u.Dishes.Update(It.IsAny<Dish>()), Times.Never);
    }

    [Fact]
    public async Task ImportDishes_ExistingDishNoNameChange_UpdatesPriceOnly()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(tenantId)).ReturnsAsync(new List<Restaurant> { new Restaurant { Slug = "slug-1" } });
        _mockUnitOfWork.Setup(u => u.Categories.GetAllCategoriesByTenant(tenantId)).ReturnsAsync(new List<Category> { new Category { Id = 1, CategoryName = "Cat" } });
        
        var existingDish = new Dish { Id = 1, CategoryId = 1, DishName = "coke", Price = 10m, Type = DishType.Single };
        _mockUnitOfWork.Setup(u => u.Dishes.GetAllDishesByTenant(tenantId, true)).ReturnsAsync(new List<Dish> { existingDish });
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(new List<BranchDishConfig>());

        var mockFile = CreateMockExcelFile(ws =>
        {
            ws.Cell(2, 1).Value = "Cat"; ws.Cell(2, 2).Value = "COKE"; ws.Cell(2, 3).Value = 15m; 
        });

        await _service.ImportDishesFromExcelAsync(tenantId, mockFile.Object);
        
        existingDish.Price.Should().Be(15m);
        existingDish.DishName.Should().Be("coke");
        _mockUnitOfWork.Verify(u => u.Dishes.Update(existingDish), Times.Once);
    }

    [Fact]
    public async Task ImportDishes_ExistingComboNoNameChange_UpdatesPriceOnly()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(tenantId)).ReturnsAsync(new List<Restaurant> { new Restaurant { Slug = "slug-1" } });
        _mockUnitOfWork.Setup(u => u.Categories.GetAllCategoriesByTenant(tenantId)).ReturnsAsync(new List<Category> { new Category { Id = 1, CategoryName = "Cat" } });

        var existingCombo = new Dish { Id = 1, CategoryId = 1, DishName = "combo", Price = 10m, Type = DishType.Combo };
        _mockUnitOfWork.Setup(u => u.Dishes.GetAllDishesByTenant(tenantId, true)).ReturnsAsync(new List<Dish> {
            existingCombo,
            new Dish { Id = 2, CategoryId = 1, DishName = "Coke", Type = DishType.Single }
        });
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(new List<BranchDishConfig>());
        _mockUnitOfWork.Setup(u => u.ComboDetails.FindAsync(It.IsAny<Expression<Func<ComboDetail, bool>>>())).ReturnsAsync(new List<ComboDetail>());

        var mockFile = CreateMockExcelFile(ws =>
        {
            ws.Cell(2, 1).Value = "Cat"; ws.Cell(2, 2).Value = "COMBO"; ws.Cell(2, 3).Value = 20m; ws.Cell(2, 5).Value = "Combo"; ws.Cell(2, 6).Value = "Cat|Coke:1";
        });

        await _service.ImportDishesFromExcelAsync(tenantId, mockFile.Object);

        existingCombo.Price.Should().Be(20m);
        existingCombo.DishName.Should().Be("combo");
        _mockUnitOfWork.Verify(u => u.Dishes.Update(existingCombo), Times.Once);
    }

    [Fact]
    public async Task ImportDishes_ExistingComboDetails_RemovesOldDetails()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(tenantId)).ReturnsAsync(new List<Restaurant> { new Restaurant { Slug = "slug-1" } });
        _mockUnitOfWork.Setup(u => u.Categories.GetAllCategoriesByTenant(tenantId)).ReturnsAsync(new List<Category> { new Category { Id = 1, CategoryName = "Cat" } });
        
        var existingCombo = new Dish { Id = 1, CategoryId = 1, DishName = "Combo", Price = 10m, Type = DishType.Combo };
        _mockUnitOfWork.Setup(u => u.Dishes.GetAllDishesByTenant(tenantId, true)).ReturnsAsync(new List<Dish> 
        { 
            existingCombo,
            new Dish { Id = 2, CategoryId = 1, DishName = "Coke", Type = DishType.Single } 
        });

        var existingDetails = new List<ComboDetail> { new ComboDetail { DishId = 1, ItemDishId = 99 } };
        _mockUnitOfWork.Setup(u => u.ComboDetails.FindAsync(It.IsAny<Expression<Func<ComboDetail, bool>>>())).ReturnsAsync(existingDetails);
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(new List<BranchDishConfig>());

        var mockFile = CreateMockExcelFile(ws =>
        {
            ws.Cell(2, 1).Value = "Cat"; ws.Cell(2, 2).Value = "Combo"; ws.Cell(2, 3).Value = 20m; ws.Cell(2, 5).Value = "Combo"; ws.Cell(2, 6).Value = "Coke:2";
        });

        await _service.ImportDishesFromExcelAsync(tenantId, mockFile.Object);

        _mockUnitOfWork.Verify(u => u.ComboDetails.RemoveRange(existingDetails), Times.Once);
    }

    [Fact]
    public async Task ImportDishes_ComboComponentHasNewCategory_CreatesCategory()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(tenantId)).ReturnsAsync(new List<Restaurant> { new Restaurant { Slug = "slug-1" } });
        _mockUnitOfWork.Setup(u => u.Categories.GetAllCategoriesByTenant(tenantId)).ReturnsAsync(new List<Category> { new Category { Id = 1, CategoryName = "Cat" } });
        
        var singleDish = new Dish { Id = 2, CategoryId = 99, DishName = "Fanta", Type = DishType.Single };
        _mockUnitOfWork.Setup(u => u.Dishes.GetAllDishesByTenant(tenantId, true)).ReturnsAsync(new List<Dish> { singleDish });
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(new List<BranchDishConfig>());
        _mockUnitOfWork.Setup(u => u.ComboDetails.FindAsync(It.IsAny<Expression<Func<ComboDetail, bool>>>())).ReturnsAsync(new List<ComboDetail>());

        var mockFile = CreateMockExcelFile(ws =>
        {
            ws.Cell(2, 1).Value = "Cat"; ws.Cell(2, 2).Value = "Combo"; ws.Cell(2, 3).Value = 10m; ws.Cell(2, 5).Value = "Combo";
            ws.Cell(2, 6).Value = "NewCatForComponent|Fanta:1"; 
        });

        _mockUnitOfWork.Setup(u => u.Categories.AddAsync(It.IsAny<Category>())).Callback<Category>(c => c.Id = 99);

        await _service.ImportDishesFromExcelAsync(tenantId, mockFile.Object);

        _mockUnitOfWork.Verify(u => u.Categories.AddAsync(It.Is<Category>(c => c.CategoryName == "NewCatForComponent")), Times.Once);
    }
    
    [Fact]
    public async Task ImportDishes_ComboComponentDishNotFoundByCat_ThrowsException()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(tenantId)).ReturnsAsync(new List<Restaurant> { new Restaurant { Slug = "slug-1" } });
        _mockUnitOfWork.Setup(u => u.Categories.GetAllCategoriesByTenant(tenantId)).ReturnsAsync(new List<Category>());
        _mockUnitOfWork.Setup(u => u.Dishes.GetAllDishesByTenant(tenantId, true)).ReturnsAsync(new List<Dish>()); 

        var mockFile = CreateMockExcelFile(ws =>
        {
            ws.Cell(2, 1).Value = "Cat"; ws.Cell(2, 2).Value = "Combo"; ws.Cell(2, 3).Value = 10m; ws.Cell(2, 5).Value = "Combo";
            ws.Cell(2, 6).Value = "SomeCat|GhostDish:1"; 
        });

        await _service.Invoking(s => s.ImportDishesFromExcelAsync(tenantId, mockFile.Object))
            .Should().ThrowAsync<DomainException>().WithMessage("*không tìm thấy*");
    }
    
    [Fact]
    public async Task ImportDishes_ExistingComboNoChanges_SkipsUpdate()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(tenantId)).ReturnsAsync(new List<Restaurant> { new Restaurant { Slug = "slug-1" } });
        _mockUnitOfWork.Setup(u => u.Categories.GetAllCategoriesByTenant(tenantId)).ReturnsAsync(new List<Category> { new Category { Id = 1, CategoryName = "Cat" } });
        
        var existingCombo = new Dish { Id = 1, CategoryId = 1, DishName = "Combo", Price = 10m, Description = "Desc", IsAvailable = true, IsDeleted = false, Type = DishType.Combo };
        _mockUnitOfWork.Setup(u => u.Dishes.GetAllDishesByTenant(tenantId, true)).ReturnsAsync(new List<Dish> { existingCombo, new Dish { Id = 2, CategoryId = 1, DishName = "Coke", Type = DishType.Single } });
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(new List<BranchDishConfig>());
        _mockUnitOfWork.Setup(u => u.ComboDetails.FindAsync(It.IsAny<Expression<Func<ComboDetail, bool>>>())).ReturnsAsync(new List<ComboDetail>());

        var mockFile = CreateMockExcelFile(ws =>
        {
            ws.Cell(2, 1).Value = "Cat"; ws.Cell(2, 2).Value = "Combo"; ws.Cell(2, 3).Value = 10m; ws.Cell(2, 4).Value = "Desc"; ws.Cell(2, 5).Value = "Combo"; ws.Cell(2, 6).Value = "Coke:1";
        });

        await _service.ImportDishesFromExcelAsync(tenantId, mockFile.Object);

        // Verify không gọi Update cho Combo
        _mockUnitOfWork.Verify(u => u.Dishes.Update(It.Is<Dish>(d => d.Type == DishType.Combo)), Times.Never);
    }
    
    [Fact]
    public async Task ImportDishes_ExistingComboNameCaseChanged_UpdatesName()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(tenantId)).ReturnsAsync(new List<Restaurant> { new Restaurant { Slug = "slug-1" } });
        _mockUnitOfWork.Setup(u => u.Categories.GetAllCategoriesByTenant(tenantId)).ReturnsAsync(new List<Category> { new Category { Id = 1, CategoryName = "Cat" } });
        
        string dbName = "\u212A"; 
        string excelName = "k";

        var existingCombo = new Dish { Id = 1, CategoryId = 1, DishName = dbName, Price = 10m, Type = DishType.Combo };
        _mockUnitOfWork.Setup(u => u.Dishes.GetAllDishesByTenant(tenantId, true)).ReturnsAsync(new List<Dish> {
            existingCombo,
            new Dish { Id = 2, CategoryId = 1, DishName = "Coke", Type = DishType.Single }
        });
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(new List<BranchDishConfig>());
        _mockUnitOfWork.Setup(u => u.ComboDetails.FindAsync(It.IsAny<Expression<Func<ComboDetail, bool>>>())).ReturnsAsync(new List<ComboDetail>());

        var mockFile = CreateMockExcelFile(ws =>
        {
            ws.Cell(2, 1).Value = "Cat"; ws.Cell(2, 2).Value = excelName; ws.Cell(2, 3).Value = 20m; ws.Cell(2, 5).Value = "Combo"; ws.Cell(2, 6).Value = "Cat|Coke:1";
        });

        await _service.ImportDishesFromExcelAsync(tenantId, mockFile.Object);

        // Assert
        existingCombo.Price.Should().Be(20m);
        _mockUnitOfWork.Verify(u => u.Dishes.Update(existingCombo), Times.Once);
    }
    
    [Fact]
    public async Task ImportDishes_ExistingDishNameCaseChanged_UpdatesName()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(tenantId)).ReturnsAsync(new List<Restaurant> { new Restaurant { Slug = "slug-1" } });
        _mockUnitOfWork.Setup(u => u.Categories.GetAllCategoriesByTenant(tenantId)).ReturnsAsync(new List<Category> { new Category { Id = 1, CategoryName = "Cat" } });
        
        string dbName = "\u212A"; 
        string excelName = "k";

        var existingDish = new Dish { Id = 1, CategoryId = 1, DishName = dbName, Price = 10m, Type = DishType.Single };
        _mockUnitOfWork.Setup(u => u.Dishes.GetAllDishesByTenant(tenantId, true)).ReturnsAsync(new List<Dish> { existingDish });
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(new List<BranchDishConfig>());

        var mockFile = CreateMockExcelFile(ws =>
        {
            // Cố tình đổi giá thành 20m để kích hoạt cờ updated = true
            ws.Cell(2, 1).Value = "Cat"; 
            ws.Cell(2, 2).Value = excelName; 
            ws.Cell(2, 3).Value = 20m; 
            ws.Cell(2, 4).Value = "Desc"; 
            ws.Cell(2, 5).Value = "Single";
        });

        await _service.ImportDishesFromExcelAsync(tenantId, mockFile.Object);

        // Assert: Tên món ăn và giá tiền đã được cập nhật thành công!
        existingDish.DishName.Should().Be(excelName);
        existingDish.Price.Should().Be(20m);
        _mockUnitOfWork.Verify(u => u.Dishes.Update(existingDish), Times.Once);
    }
    
    [Fact]
    public async Task ImportDishes_ComboMissingComboItems_ThrowsDomainException()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(tenantId)).ReturnsAsync(new List<Restaurant> { new Restaurant { Slug = "slug-1" } });
        _mockUnitOfWork.Setup(u => u.Categories.GetAllCategoriesByTenant(tenantId)).ReturnsAsync(new List<Category>());
        _mockUnitOfWork.Setup(u => u.Dishes.GetAllDishesByTenant(tenantId, true)).ReturnsAsync(new List<Dish>());

        var mockFile = CreateMockExcelFile(ws =>
        {
            ws.Cell(2, 1).Value = "Cat";
            ws.Cell(2, 2).Value = "Combo Lỗi";
            ws.Cell(2, 3).Value = 10m;
            ws.Cell(2, 5).Value = "Combo";
            ws.Cell(2, 6).Value = ""; // Cố tình để rỗng
        });

        await _service.Invoking(s => s.ImportDishesFromExcelAsync(tenantId, mockFile.Object))
            .Should().ThrowAsync<DomainException>().WithMessage("*thiếu ComboItems*");
    }
    
    [Fact]
    public async Task ImportDishes_DishTypeIsSingleAndComboItemsIsBlank_CoversNullAndLogicalBranches()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(tenantId)).ReturnsAsync(new List<Restaurant> { new Restaurant { Id = 1, Slug = "slug-1" } });
        _mockUnitOfWork.Setup(u => u.Categories.GetAllCategoriesByTenant(tenantId)).ReturnsAsync(new List<Category>());
        _mockUnitOfWork.Setup(u => u.Dishes.GetAllDishesByTenant(tenantId, true)).ReturnsAsync(new List<Dish>());
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(new List<BranchDishConfig>());

        var mockFile = CreateMockExcelFile(ws =>
        {
            ws.Cell(2, 1).Value = "Cat";
            ws.Cell(2, 2).Value = "Dish 1";
            ws.Cell(2, 3).Value = 10m;
            ws.Cell(2, 4).Value = "Desc";
            ws.Cell(2, 5).Value = "Single"; 
            ws.Cell(2, 6).Clear(); 
        });

        var result = await _service.ImportDishesFromExcelAsync(tenantId, mockFile.Object);

        // Assert
        result.Should().Be(1);
        _mockUnitOfWork.Verify(u => u.Dishes.AddAsync(It.Is<Dish>(d => d.DishName == "Dish 1" && d.Type == DishType.Single)), Times.Once);
    }
    
    [Fact]
    public async Task ImportDishes_DishTypeAndComboItems_VariousEmptyStates_CoversAllBranches()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant());
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByTenantIdAsync(tenantId)).ReturnsAsync(new List<Restaurant> { new Restaurant { Id = 1, Slug = "slug-1" } });
        _mockUnitOfWork.Setup(u => u.Categories.GetAllCategoriesByTenant(tenantId)).ReturnsAsync(new List<Category>());
        _mockUnitOfWork.Setup(u => u.Dishes.GetAllDishesByTenant(tenantId, true)).ReturnsAsync(new List<Dish>());
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(new List<BranchDishConfig>());

        var mockFile = CreateMockExcelFile(ws =>
        {
            ws.Cell(2, 1).Value = "Cat";
            ws.Cell(2, 2).Value = "Dish 1";
            ws.Cell(2, 3).Value = 10m;
            ws.Cell(2, 5).Value = "Single"; 
            
            ws.Cell(2, 6).Value = ""; 
            
            ws.Cell(3, 1).Value = "Cat";
            ws.Cell(3, 2).Value = "Dish 2";
            ws.Cell(3, 3).Value = 20m;
            
            ws.Cell(3, 5).Value = ""; 
            ws.Cell(3, 6).Value = ""; 
        });

        var result = await _service.ImportDishesFromExcelAsync(tenantId, mockFile.Object);
        
        result.Should().Be(2);
        
        _mockUnitOfWork.Verify(u => u.Dishes.AddAsync(It.Is<Dish>(d => d.Type == DishType.Single)), Times.Exactly(2));
    }
    #endregion
}