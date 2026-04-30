using System.Collections;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.ML;
using Moq;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Infrastructure.Models.AI;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Services;

namespace ScanToOrder.Infrastructure.UnitTest.Services;

public class AIUpsellServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);
    private readonly Mock<IPlanLimitationService> _planLimitationService = new(MockBehavior.Strict);
    private readonly Mock<IAIUpsellPredictor> _predictor = new(MockBehavior.Strict);
    private readonly Mock<IBranchDishConfigRepository> _branchDishConfigs = new(MockBehavior.Strict);
    private readonly Mock<IComboDetailRepository> _comboDetails = new(MockBehavior.Strict);
    private readonly Mock<IOrderRepository> _orders = new(MockBehavior.Strict);
    private readonly Mock<IOrderDetailRepository> _orderDetails = new(MockBehavior.Strict);
    private readonly Mock<IAIUpsellRedisService> _aiUpsellRedisService = new(MockBehavior.Strict);

    public AIUpsellServiceTests()
    {
        _unitOfWork.SetupGet(x => x.BranchDishConfigs).Returns(_branchDishConfigs.Object);
        _unitOfWork.SetupGet(x => x.ComboDetails).Returns(_comboDetails.Object);
        _unitOfWork.SetupGet(x => x.Orders).Returns(_orders.Object);
        _unitOfWork.SetupGet(x => x.OrderDetails).Returns(_orderDetails.Object);
    }

    [Fact]
    public async Task GetRecommendationsAsync_WhenPlanDoesNotAllowAIUpsell_ReturnsPlanLimited()
    {
        _planLimitationService
            .Setup(x => x.GetRestaurantFeaturesAsync(1))
            .ReturnsAsync(new PlanFeaturesConfig { CanUseAIUpsell = false });

        var sut = new AIUpsellService(_unitOfWork.Object, _aiUpsellRedisService.Object, _planLimitationService.Object, null);

        var (dishIds, source) = await sut.GetRecommendationsAsync(1, new List<int> { 10 }, 3);

        dishIds.Should().BeEmpty();
        source.Should().Be("Plan-Limited");
        _planLimitationService.VerifyAll();
    }

    [Fact]
    public async Task GetRecommendationsAsync_WhenNoCandidatesAfterCartExclusion_ReturnsEmpty()
    {
        _planLimitationService
            .Setup(x => x.GetRestaurantFeaturesAsync(1))
            .ReturnsAsync(new PlanFeaturesConfig { CanUseAIUpsell = true });

        var branchConfigs = new List<BranchDishConfig>
        {
            BuildBranchDishConfig(restaurantId: 1, dishId: 10, dishType: DishType.Single)
        };

        _branchDishConfigs.Setup(x => x.GetQueryable()).Returns(AsAsyncQueryable(branchConfigs));
        _comboDetails.Setup(x => x.GetQueryable()).Returns(AsAsyncQueryable(new List<ComboDetail>()));

        var sut = new AIUpsellService(_unitOfWork.Object, _aiUpsellRedisService.Object, _planLimitationService.Object, null);

        var (dishIds, source) = await sut.GetRecommendationsAsync(1, new List<int> { 10 }, 3);

        dishIds.Should().BeEmpty();
        source.Should().Be("empty");
    }

    [Fact]
    public async Task GetRecommendationsAsync_WhenComboInCart_ExcludesItsChildDishes()
    {
        _planLimitationService
            .Setup(x => x.GetRestaurantFeaturesAsync(1))
            .ReturnsAsync(new PlanFeaturesConfig { CanUseAIUpsell = true });

        var branchConfigs = new List<BranchDishConfig>
        {
            BuildBranchDishConfig(restaurantId: 1, dishId: 100, dishType: DishType.Combo),
            BuildBranchDishConfig(restaurantId: 1, dishId: 101, dishType: DishType.Single)
        };

        var comboDetails = new List<ComboDetail>
        {
            new() { DishId = 100, ItemDishId = 101, Quantity = 1 }
        };

        _branchDishConfigs.Setup(x => x.GetQueryable()).Returns(AsAsyncQueryable(branchConfigs));
        _comboDetails.Setup(x => x.GetQueryable()).Returns(AsAsyncQueryable(comboDetails));

        var sut = new AIUpsellService(_unitOfWork.Object, _aiUpsellRedisService.Object, _planLimitationService.Object, null);

        var (dishIds, source) = await sut.GetRecommendationsAsync(1, new List<int> { 100 }, 3);

        dishIds.Should().BeEmpty();
        source.Should().Be("empty");
    }

    [Fact]
    public async Task GetRecommendationsAsync_WhenBestSellersAvailable_ReturnsBestSellerFallback()
    {
        _planLimitationService
            .Setup(x => x.GetRestaurantFeaturesAsync(1))
            .ReturnsAsync(new PlanFeaturesConfig { CanUseAIUpsell = true });

        var branchConfigs = new List<BranchDishConfig>
        {
            BuildBranchDishConfig(restaurantId: 1, dishId: 10, dishType: DishType.Single),
            BuildBranchDishConfig(restaurantId: 1, dishId: 20, dishType: DishType.Single),
            BuildBranchDishConfig(restaurantId: 1, dishId: 30, dishType: DishType.Single)
        };

        var order = new Order { IsDeleted = false, RestaurantId = 1 };
        var orderDetails = new List<OrderDetail>
        {
            new() { DishId = 20, Quantity = 10, Order = order },
            new() { DishId = 30, Quantity = 4, Order = order }
        };

        _branchDishConfigs.Setup(x => x.GetQueryable()).Returns(AsAsyncQueryable(branchConfigs));
        _comboDetails.Setup(x => x.GetQueryable()).Returns(AsAsyncQueryable(new List<ComboDetail>()));
        _orderDetails.Setup(x => x.GetQueryable()).Returns(AsAsyncQueryable(orderDetails));

        _aiUpsellRedisService.Setup(x => x.GetBestSellersAsync(1))
            .ReturnsAsync(new List<int> { 20, 30 });
        _aiUpsellRedisService.Setup(x => x.GetAIEligibilityAsync(1))
            .ReturnsAsync(false);

        var sut = new AIUpsellService(_unitOfWork.Object, _aiUpsellRedisService.Object, _planLimitationService.Object, null);

        var (dishIds, source) = await sut.GetRecommendationsAsync(1, new List<int> { 10 }, 2);

        source.Should().Be("BestSellers_Fallback");
        dishIds.Should().Equal(20, 30);
    }

    [Fact]
    public async Task GetRecommendationsAsync_WhenNoBestSellers_ReturnsRandomColdStart()
    {
        _planLimitationService
            .Setup(x => x.GetRestaurantFeaturesAsync(1))
            .ReturnsAsync(new PlanFeaturesConfig { CanUseAIUpsell = true });

        var branchConfigs = new List<BranchDishConfig>
        {
            BuildBranchDishConfig(restaurantId: 1, dishId: 10, dishType: DishType.Single),
            BuildBranchDishConfig(restaurantId: 1, dishId: 20, dishType: DishType.Single),
            BuildBranchDishConfig(restaurantId: 1, dishId: 30, dishType: DishType.Single)
        };

        _branchDishConfigs.Setup(x => x.GetQueryable()).Returns(AsAsyncQueryable(branchConfigs));
        _comboDetails.Setup(x => x.GetQueryable()).Returns(AsAsyncQueryable(new List<ComboDetail>()));
        _orderDetails.Setup(x => x.GetQueryable()).Returns(AsAsyncQueryable(new List<OrderDetail>()));

        _aiUpsellRedisService.Setup(x => x.GetBestSellersAsync(1))
            .ReturnsAsync(new List<int>());
        _aiUpsellRedisService.Setup(x => x.GetAIEligibilityAsync(1))
            .ReturnsAsync(false);

        var sut = new AIUpsellService(_unitOfWork.Object, _aiUpsellRedisService.Object, _planLimitationService.Object, null);

        var (dishIds, source) = await sut.GetRecommendationsAsync(1, new List<int> { 10 }, 2);

        source.Should().Be("Random_ColdStart");
        dishIds.Should().HaveCount(2);
        dishIds.Should().OnlyContain(id => id == 20 || id == 30);
    }

    [Fact]
    public async Task GetRecommendationsAsync_WhenAiPredictorAndEnoughOrders_ReturnsAiMatrixFactorization()
    {
        _planLimitationService
            .Setup(x => x.GetRestaurantFeaturesAsync(1))
            .ReturnsAsync(new PlanFeaturesConfig { CanUseAIUpsell = true });

        var branchConfigs = new List<BranchDishConfig>
        {
            BuildBranchDishConfig(restaurantId: 1, dishId: 10, dishType: DishType.Single),
            BuildBranchDishConfig(restaurantId: 1, dishId: 11, dishType: DishType.Single),
            BuildBranchDishConfig(restaurantId: 1, dishId: 20, dishType: DishType.Single),
            BuildBranchDishConfig(restaurantId: 1, dishId: 30, dishType: DishType.Single)
        };

        var orders = Enumerable.Range(1, 60)
            .Select(_ => new Order { Id = Guid.NewGuid(), IsDeleted = false, RestaurantId = 1 })
            .ToList();

        _branchDishConfigs.Setup(x => x.GetQueryable()).Returns(AsAsyncQueryable(branchConfigs));
        _comboDetails.Setup(x => x.GetQueryable()).Returns(AsAsyncQueryable(new List<ComboDetail>()));
        _orders.Setup(x => x.GetQueryable()).Returns(AsAsyncQueryable(orders));

        _predictor.Setup(x => x.PredictScore(10, 20)).Returns(0.2f);
        _predictor.Setup(x => x.PredictScore(10, 30)).Returns(0.9f);
        _predictor.Setup(x => x.PredictScore(11, 20)).Returns(0.3f);
        _predictor.Setup(x => x.PredictScore(11, 30)).Returns(0.1f);

        _aiUpsellRedisService.Setup(x => x.GetAIEligibilityAsync(1))
            .ReturnsAsync(true);

        var sut = new AIUpsellService(_unitOfWork.Object, _aiUpsellRedisService.Object, _planLimitationService.Object, null, _predictor.Object);

        var (dishIds, source) = await sut.GetRecommendationsAsync(1, new List<int> { 10, 11 }, 1);

        source.Should().Be("AI_MatrixFactorization");
        dishIds.Should().Equal(30);
    }

    [Fact]
    public async Task GetRecommendationsAsync_WhenAiPredictorButNotEnoughOrders_FallsBackToBestSellers()
    {
        _planLimitationService
            .Setup(x => x.GetRestaurantFeaturesAsync(1))
            .ReturnsAsync(new PlanFeaturesConfig { CanUseAIUpsell = true });

        var branchConfigs = new List<BranchDishConfig>
        {
            BuildBranchDishConfig(restaurantId: 1, dishId: 10, dishType: DishType.Single),
            BuildBranchDishConfig(restaurantId: 1, dishId: 20, dishType: DishType.Single)
        };

        var orders = Enumerable.Range(1, 10)
            .Select(_ => new Order { Id = Guid.NewGuid(), IsDeleted = false, RestaurantId = 1 })
            .ToList();

        var order = new Order { IsDeleted = false, RestaurantId = 1 };
        var orderDetails = new List<OrderDetail>
        {
            new() { DishId = 20, Quantity = 6, Order = order }
        };

        _branchDishConfigs.Setup(x => x.GetQueryable()).Returns(AsAsyncQueryable(branchConfigs));
        _comboDetails.Setup(x => x.GetQueryable()).Returns(AsAsyncQueryable(new List<ComboDetail>()));
        _orders.Setup(x => x.GetQueryable()).Returns(AsAsyncQueryable(orders));
        _orderDetails.Setup(x => x.GetQueryable()).Returns(AsAsyncQueryable(orderDetails));

        _aiUpsellRedisService.Setup(x => x.GetAIEligibilityAsync(1))
            .ReturnsAsync(false);
        _aiUpsellRedisService.Setup(x => x.GetBestSellersAsync(1))
            .ReturnsAsync(new List<int> { 20 });

        var sut = new AIUpsellService(_unitOfWork.Object, _aiUpsellRedisService.Object, _planLimitationService.Object, null, _predictor.Object);

        var (dishIds, source) = await sut.GetRecommendationsAsync(1, new List<int> { 10 }, 1);

        source.Should().Be("BestSellers_Fallback");
        dishIds.Should().Equal(20);
    }

    [Fact]
    public async Task GetRecommendationsAsync_WhenPredictionPoolPresentAndPredictorNull_UsesPoolBranch()
    {
        _planLimitationService
            .Setup(x => x.GetRestaurantFeaturesAsync(1))
            .ReturnsAsync(new PlanFeaturesConfig { CanUseAIUpsell = true });

        var branchConfigs = new List<BranchDishConfig>
        {
            BuildBranchDishConfig(restaurantId: 1, dishId: 10, dishType: DishType.Single),
            BuildBranchDishConfig(restaurantId: 1, dishId: 20, dishType: DishType.Single),
            BuildBranchDishConfig(restaurantId: 1, dishId: 30, dishType: DishType.Single)
        };

        var orders = Enumerable.Range(1, 55)
            .Select(_ => new Order { Id = Guid.NewGuid(), IsDeleted = false, RestaurantId = 1 })
            .ToList();

        _branchDishConfigs.Setup(x => x.GetQueryable()).Returns(AsAsyncQueryable(branchConfigs));
        _comboDetails.Setup(x => x.GetQueryable()).Returns(AsAsyncQueryable(new List<ComboDetail>()));
        _orders.Setup(x => x.GetQueryable()).Returns(AsAsyncQueryable(orders));

        var fakePool = (PredictionEnginePool<DishCoOccurrence, DishPrediction>)
            RuntimeHelpers.GetUninitializedObject(typeof(PredictionEnginePool<DishCoOccurrence, DishPrediction>));

        float PoolScoreProvider(int targetDishId, int candidateId)
            => candidateId == 30 ? 0.8f : 0.1f;

        _aiUpsellRedisService.Setup(x => x.GetAIEligibilityAsync(1))
            .ReturnsAsync(true);

        var sut = new AIUpsellService(
            _unitOfWork.Object,
            _aiUpsellRedisService.Object,
            _planLimitationService.Object,
            fakePool,
            predictor: null,
            poolScoreProvider: PoolScoreProvider);

        var (dishIds, source) = await sut.GetRecommendationsAsync(1, new List<int> { 10 }, 1);

        source.Should().Be("AI_MatrixFactorization");
        dishIds.Should().Equal(30);
    }

    private static BranchDishConfig BuildBranchDishConfig(int restaurantId, int dishId, DishType dishType)
    {
        return new BranchDishConfig
        {
            RestaurantId = restaurantId,
            DishId = dishId,
            IsDeleted = false,
            IsSelling = true,
            IsSoldOut = false,
            Dish = new Dish
            {
                Id = dishId,
                Type = dishType,
                IsDeleted = false,
                DishName = $"Dish-{dishId}",
                Description = "desc",
                ImageUrl = "img",
                IsAvailable = true
            }
        };
    }

    private static IQueryable<T> AsAsyncQueryable<T>(IEnumerable<T> source)
    {
        return new TestAsyncEnumerable<T>(source);
    }

    private sealed class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;

        public TestAsyncQueryProvider(IQueryProvider inner)
        {
            _inner = inner;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            return new TestAsyncEnumerable<TEntity>(expression);
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new TestAsyncEnumerable<TElement>(expression);
        }

        public object? Execute(Expression expression)
        {
            return _inner.Execute(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return _inner.Execute<TResult>(expression);
        }

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var expectedResultType = typeof(TResult).GetGenericArguments()[0];
            var executionResult = typeof(IQueryProvider)
                .GetMethod(nameof(IQueryProvider.Execute), 1, new[] { typeof(Expression) })
                !.MakeGenericMethod(expectedResultType)
                .Invoke(_inner, new object[] { expression });

            return (TResult)typeof(Task)
                .GetMethod(nameof(Task.FromResult))
                !.MakeGenericMethod(expectedResultType)
                .Invoke(null, new[] { executionResult })!;
        }
    }

    private sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable)
            : base(enumerable)
        {
        }

        public TestAsyncEnumerable(Expression expression)
            : base(expression)
        {
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
        }

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
    }

    private sealed class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;

        public TestAsyncEnumerator(IEnumerator<T> inner)
        {
            _inner = inner;
        }

        public T Current => _inner.Current;

        public ValueTask DisposeAsync()
        {
            _inner.Dispose();
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return new ValueTask<bool>(_inner.MoveNext());
        }
    }
}
