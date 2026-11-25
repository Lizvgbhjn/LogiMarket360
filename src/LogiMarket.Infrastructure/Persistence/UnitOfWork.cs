namespace LogiMarket.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore.Storage;
using LogiMarket.Application.Ports;

public class UnitOfWork(LogiMarketDbContext context, AuditoriaCobroTransaccional auditoriaCobro) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    public async Task<IDisposable> BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
            return _transaction;

        _transaction = await context.Database.BeginTransactionAsync(ct);
        auditoriaCobro.Iniciar();
        return _transaction;
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction == null)
        {
            await context.SaveChangesAsync(ct);
            return;
        }

        await auditoriaCobro.GuardarEnTransaccionAsync(ct);
        await context.SaveChangesAsync(ct);
        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
        auditoriaCobro.Completar();
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction == null) return;

        try
        {
            await _transaction.RollbackAsync(ct);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
            context.ChangeTracker.Clear();
        }

        await auditoriaCobro.GuardarTrasRollbackAsync(ct);
    }
}
