using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Infrastructure.Services;
using StackExchange.Redis;

namespace ScanToOrder.Infrastructure.UnitTest.Services;

public class MenuCacheServiceTests
{
    private static (MenuCacheService sut, Mock<IDatabase> database, Mock<ILogger<MenuCacheService>> logger)
        CreateSut(string? instanceName = null)
    {
        var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        var database = new Mock<IDatabase>(MockBehavior.Loose);
        var config = new Mock<IConfiguration>(MockBehavior.Strict);
        var logger = new Mock<ILogger<MenuCacheService>>();

        connection
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        config.SetupGet(x => x["RedisSettings:InstanceName"]).Returns(instanceName);

        var sut = new MenuCacheService(connection.Object, config.Object, logger.Object);
        return (sut, database, logger);
    }

    private static void VerifyLog(Mock<ILogger<MenuCacheService>> logger, LogLevel level, string messagePart, Times times)
    {
        logger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains(messagePart)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }

    private static void AssertStringSetInvocation(Mock<IDatabase> database, string expectedKey, string expectedValue, string expectedTtl)
    {
        var invocation = database.Invocations.Single(invocation => invocation.Method.Name == nameof(IDatabase.StringSetAsync));

        invocation.Arguments[0].ToString().Should().Be(expectedKey);
        invocation.Arguments[1].ToString().Should().Be(expectedValue);
        invocation.Arguments[2].ToString().Should().Be(expectedTtl);
    }

    private static void AssertStringGetInvocation(Mock<IDatabase> database, string expectedKey)
    {
        var invocation = database.Invocations.Single(invocation => invocation.Method.Name == nameof(IDatabase.StringGetAsync));

        invocation.Arguments[0].ToString().Should().Be(expectedKey);
    }

    private static void AssertKeyDeleteInvocation(Mock<IDatabase> database, string expectedKey)
    {
        var invocation = database.Invocations.Single(invocation => invocation.Method.Name == nameof(IDatabase.KeyDeleteAsync));

        invocation.Arguments[0].ToString().Should().Be(expectedKey);
    }

    [Fact]
    public async Task GetMenuAsync_WhenInstanceNameIsNull_UsesDefaultKey()
    {
        var (sut, database, _) = CreateSut(null);
        const string expectedKey = "menu:1";

        database
            .Setup(db => db.StringGetAsync(expectedKey, It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await sut.GetMenuAsync(1);

        result.Should().BeNull();
        AssertStringGetInvocation(database, expectedKey);
    }

    [Fact]
    public async Task GetMenuAsync_ReturnsDeserializedMenu_WhenCacheExists()
    {
        var (sut, database, _) = CreateSut("S2O:");
        const string expectedKey = "S2O:menu:1";
        var menuData = new List<MenuCategoryDto>
        {
            new() { CategoryId = 1, CategoryName = "Pizza" }
        };

        database
            .Setup(db => db.StringGetAsync(expectedKey, It.IsAny<CommandFlags>()))
            .ReturnsAsync(JsonSerializer.Serialize(menuData));

        var result = await sut.GetMenuAsync(1);

        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].CategoryName.Should().Be("Pizza");
        AssertStringGetInvocation(database, expectedKey);
    }

    [Fact]
    public async Task GetMenuAsync_WhenRedisThrows_ReturnsNullAndLogsWarning()
    {
        var (sut, database, logger) = CreateSut("S2O:");
        const string expectedKey = "S2O:menu:1";

        database
            .Setup(db => db.StringGetAsync(expectedKey, It.IsAny<CommandFlags>()))
            .ThrowsAsync(new Exception("Fail"));

        var result = await sut.GetMenuAsync(1);

        result.Should().BeNull();
        VerifyLog(logger, LogLevel.Warning, "Redis GetMenuAsync failed", Times.Once());
    }

    [Fact]
    public async Task SetMenuAsync_UsesProvidedExpiry_WhenNotNull()
    {
        var (sut, database, _) = CreateSut("S2O:");
        var expiry = TimeSpan.FromHours(1);
        var menu = new List<MenuCategoryDto>
        {
            new() { CategoryId = 1, CategoryName = "Pizza" }
        };
        var expectedKey = "S2O:menu:1";
        var expectedJson = JsonSerializer.Serialize(menu);

        database
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await sut.SetMenuAsync(1, menu, expiry);

        AssertStringSetInvocation(database, expectedKey, expectedJson, "EX 3600");
    }

    [Fact]
    public async Task SetMenuAsync_WhenExpiryIsNull_UsesDefaultTtl()
    {
        var (sut, database, _) = CreateSut();
        var menu = new List<MenuCategoryDto>();
        const string expectedKey = "menu:1";

        database
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await sut.SetMenuAsync(1, menu, null);

        AssertStringSetInvocation(database, expectedKey, JsonSerializer.Serialize(menu), "EX 300");
    }

    [Fact]
    public async Task SetMenuAsync_WhenRedisThrows_LogsWarning()
    {
        var (sut, database, logger) = CreateSut();

        database
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<Expiration>(),
                It.IsAny<ValueCondition>(),
                It.IsAny<CommandFlags>()))
            .Callback(() => throw new Exception("Redis connection failed"));

        await sut.SetMenuAsync(1, new List<MenuCategoryDto>());

        VerifyLog(logger, LogLevel.Warning, "Cache write skipped", Times.Once());
    }

    [Fact]
    public async Task InvalidateMenuAsync_DeletesKeyAndLogsInformation()
    {
        var (sut, database, logger) = CreateSut("S2O:");
        const string expectedKey = "S2O:menu:1";

        database
            .Setup(db => db.KeyDeleteAsync(expectedKey, It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await sut.InvalidateMenuAsync(1);

        AssertKeyDeleteInvocation(database, expectedKey);
        VerifyLog(logger, LogLevel.Information, "Menu cache invalidated", Times.Once());
    }

    [Fact]
    public async Task InvalidateMenuAsync_WhenRedisThrows_LogsWarning()
    {
        var (sut, database, logger) = CreateSut();

        database
            .Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new Exception("Delete Fail"));

        await sut.InvalidateMenuAsync(1);

        VerifyLog(logger, LogLevel.Warning, "Redis InvalidateMenuAsync failed", Times.Once());
    }
}