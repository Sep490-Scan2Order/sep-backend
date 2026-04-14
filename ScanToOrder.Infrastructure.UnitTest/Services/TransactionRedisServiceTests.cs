using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using ScanToOrder.Infrastructure.Services;
using StackExchange.Redis;

namespace ScanToOrder.Infrastructure.UnitTest.Services;

public class TransactionRedisServiceTests
{
    private static (TransactionRedisService sut, Mock<IDatabase> db, Mock<IConnectionMultiplexer> mux, Mock<IConfiguration> cfg)
        CreateSut(string? instanceName)
    {
        var db = new Mock<IDatabase>(MockBehavior.Strict);
        var mux = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        var cfg = new Mock<IConfiguration>(MockBehavior.Strict);

        mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);
        cfg.SetupGet(c => c["RedisSettings:InstanceName"]).Returns(instanceName);

        var sut = new TransactionRedisService(mux.Object, cfg.Object);
        return (sut, db, mux, cfg);
    }

    [Fact]
    public async Task Constructor_InstanceNameNull_UsesEmpty()
    {
        var (sut, db, _, _) = CreateSut(null);
        var tenantId = Guid.NewGuid();
        const string code = "CODE";

        db.Setup(d => d.StringSetAsync(
                "transaction:CODE",
                tenantId.ToString(),
                It.Is<Expiration>(e => e.ToString() == "EX 600"),
                It.IsAny<ValueCondition>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await sut.SaveTransactionCodeAsync(code, tenantId);
        db.VerifyAll();
    }

    [Fact]
    public async Task Constructor_InstanceNameSet_UsesPrefix()
    {
        var (sut, db, _, _) = CreateSut("myapp:");
        var tenantId = Guid.NewGuid();
        const string code = "CODE";

        db.Setup(d => d.StringSetAsync(
                "myapp:transaction:CODE",
                tenantId.ToString(),
                It.Is<Expiration>(e => e.ToString() == "EX 600"),
                It.IsAny<ValueCondition>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await sut.SaveTransactionCodeAsync(code, tenantId);
        db.VerifyAll();
    }

    [Fact]
    public async Task SaveTransactionCode_CallsStringSetAsync_WithCorrectKeyAndTTL()
    {
        var (sut, db, _, _) = CreateSut("prefix:");
        var tenantId = Guid.NewGuid();

        db.Setup(d => d.StringSetAsync(
                "prefix:transaction:T1",
                tenantId.ToString(),
                It.Is<Expiration>(e => e.ToString() == "EX 600"),
                It.IsAny<ValueCondition>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await sut.SaveTransactionCodeAsync("T1", tenantId);

        db.VerifyAll();
    }

    [Fact]
    public async Task GetTenantIdByTransactionCode_CallsStringGetAsync_WithCorrectKey()
    {
        var (sut, db, _, _) = CreateSut("prefix:");
        db.Setup(d => d.StringGetAsync("prefix:transaction:T1", It.IsAny<CommandFlags>()))
            .ReturnsAsync("tenant-1");

        var result = await sut.GetTenantIdByTransactionCodeAsync("T1");

        result.Should().Be("tenant-1");
        db.VerifyAll();
    }

    [Fact]
    public async Task GetTenantIdByTransactionCode_ReturnsNullWhenNotFound()
    {
        var (sut, db, _, _) = CreateSut("");
        db.Setup(d => d.StringGetAsync("transaction:T1", It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await sut.GetTenantIdByTransactionCodeAsync("T1");

        result.Should().BeNull();
        db.VerifyAll();
    }

    [Fact]
    public async Task DeleteTransactionCode_CallsKeyDeleteAsync_WithCorrectKey()
    {
        var (sut, db, _, _) = CreateSut("p:");
        db.Setup(d => d.KeyDeleteAsync("p:transaction:T1", It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await sut.DeleteTransactionCodeAsync("T1");

        db.VerifyAll();
    }

    [Fact]
    public async Task ExistsTransactionCode_ReturnsTrue_WhenKeyExists()
    {
        var (sut, db, _, _) = CreateSut("p:");
        db.Setup(d => d.KeyExistsAsync("p:transaction:T1", It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await sut.ExistsTransactionCodeAsync("T1");

        result.Should().BeTrue();
        db.VerifyAll();
    }

    [Fact]
    public async Task ExistsTransactionCode_ReturnsFalse_WhenKeyNotExists()
    {
        var (sut, db, _, _) = CreateSut("p:");
        db.Setup(d => d.KeyExistsAsync("p:transaction:T1", It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        var result = await sut.ExistsTransactionCodeAsync("T1");

        result.Should().BeFalse();
        db.VerifyAll();
    }

    [Fact]
    public async Task SaveOrderPaymentCode_DefaultExpiry_CallsStringSetAsync_15Min()
    {
        var (sut, db, _, _) = CreateSut("app:");
        db.Setup(d => d.StringSetAsync(
                "app:orderpayment:P1",
                "cart-1",
                It.Is<Expiration>(e => e.ToString() == "EX 900"),
                It.IsAny<ValueCondition>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await sut.SaveOrderPaymentCodeAsync("P1", "cart-1");

        db.VerifyAll();
    }

    [Fact]
    public async Task SaveOrderPaymentCode_CustomExpiry_CallsStringSetAsync_CustomTTL()
    {
        var (sut, db, _, _) = CreateSut("app:");
        var expiry = TimeSpan.FromMinutes(5);
        db.Setup(d => d.StringSetAsync(
                "app:orderpayment:P1",
                "cart-1",
                It.Is<Expiration>(e => e.ToString() == "EX 300"),
                It.IsAny<ValueCondition>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await sut.SaveOrderPaymentCodeAsync("P1", "cart-1", expiry);

        db.VerifyAll();
    }

    [Fact]
    public async Task GetCartIdByOrderPaymentCode_CallsStringGetAsync_WithCorrectKey()
    {
        var (sut, db, _, _) = CreateSut("app:");
        db.Setup(d => d.StringGetAsync("app:orderpayment:P1", It.IsAny<CommandFlags>()))
            .ReturnsAsync("cart-1");

        var result = await sut.GetCartIdByOrderPaymentCodeAsync("P1");

        result.Should().Be("cart-1");
        db.VerifyAll();
    }

    [Fact]
    public async Task GetCartIdByOrderPaymentCode_ReturnsNullWhenNotFound()
    {
        var (sut, db, _, _) = CreateSut("app:");
        db.Setup(d => d.StringGetAsync("app:orderpayment:P1", It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await sut.GetCartIdByOrderPaymentCodeAsync("P1");

        result.Should().BeNull();
        db.VerifyAll();
    }

    [Fact]
    public async Task DeleteOrderPaymentCode_CallsKeyDeleteAsync_WithCorrectKey()
    {
        var (sut, db, _, _) = CreateSut("app:");
        db.Setup(d => d.KeyDeleteAsync("app:orderpayment:P1", It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await sut.DeleteOrderPaymentCodeAsync("P1");

        db.VerifyAll();
    }

    [Fact]
    public async Task KeyPattern_WithPrefix_TransactionKey()
    {
        var (sut, db, _, _) = CreateSut("app:");
        var tenantId = Guid.NewGuid();
        db.Setup(d => d.StringSetAsync(
                "app:transaction:CODE",
                tenantId.ToString(),
                It.Is<Expiration>(e => e.ToString() == "EX 600"),
                It.IsAny<ValueCondition>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await sut.SaveTransactionCodeAsync("CODE", tenantId);

        db.VerifyAll();
    }

    [Fact]
    public async Task KeyPattern_WithPrefix_OrderPaymentKey()
    {
        var (sut, db, _, _) = CreateSut("app:");
        db.Setup(d => d.StringSetAsync(
                "app:orderpayment:CODE",
                "cart",
                It.Is<Expiration>(e => e.ToString() == "EX 900"),
                It.IsAny<ValueCondition>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await sut.SaveOrderPaymentCodeAsync("CODE", "cart");

        db.VerifyAll();
    }
}
