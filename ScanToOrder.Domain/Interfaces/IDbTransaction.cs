namespace ScanToOrder.Domain.Interfaces;

public interface IDbTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}