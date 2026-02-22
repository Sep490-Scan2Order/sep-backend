using Microsoft.EntityFrameworkCore.Storage;
using IDbTransaction = ScanToOrder.Domain.Interfaces.IDbTransaction;

namespace ScanToOrder.Infrastructure.Repositories;

public class EfDbTransaction : IDbTransaction
{
    private readonly IDbContextTransaction _efTransaction;

    public EfDbTransaction(IDbContextTransaction efTransaction)
    {
        _efTransaction = efTransaction;
    }

    public Task CommitAsync(CancellationToken ct = default) => _efTransaction.CommitAsync(ct);

    public Task RollbackAsync(CancellationToken ct = default) => _efTransaction.RollbackAsync(ct);

    public ValueTask DisposeAsync() => _efTransaction.DisposeAsync();
}