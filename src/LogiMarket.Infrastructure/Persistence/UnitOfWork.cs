namespace LogiMarket.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using LogiMarket.Domain.Entities;
using LogiMarket.Application.Ports;

public class UnitOfWork : IUnitOfWork
{
    private readonly LogiMarketDbContext _context;
    private IDbContextTransaction? _transaction;
    private readonly List<Auditoria> _auditoriaCobro = new();

    public UnitOfWork(LogiMarketDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IDisposable> BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            return _transaction;
        }

        _transaction = await _context.Database.BeginTransactionAsync(ct);
        _context.ChangeTracker.Tracked += RegistrarAuditoriaCobro;
        return _transaction;
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
        if (_transaction != null)
        {
            await _transaction.CommitAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
            _context.ChangeTracker.Tracked -= RegistrarAuditoriaCobro;
            _auditoriaCobro.Clear();
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            try
            {
                await _transaction.RollbackAsync(ct);
            }
            finally
            {
                await _transaction.DisposeAsync();
                _transaction = null;
                _context.ChangeTracker.Tracked -= RegistrarAuditoriaCobro;
                _context.ChangeTracker.Clear();
            }

            if (_auditoriaCobro.Count > 0)
            {
                var intentos = _auditoriaCobro.ToArray();
                _auditoriaCobro.Clear();
                _context.Auditorias.AddRange(intentos);
                await _context.SaveChangesAsync(ct);
            }
        }
    }

    private void RegistrarAuditoriaCobro(object? sender, EntityTrackedEventArgs args)
    {
        if (args.FromQuery || args.Entry.Entity is not Auditoria { Entidad: "PasarelaPago" } log)
            return;

        _auditoriaCobro.Add(new Auditoria
        {
            Usuario = log.Usuario,
            Accion = log.Accion == "COBRO_EXITOSO" ? "COBRO_AUTORIZACION_REVERTIDA" : log.Accion,
            Entidad = log.Entidad,
            Detalle = log.Detalle,
            Fecha = log.Fecha,
            Exitoso = log.Accion == "COBRO_INICIO"
        });
    }
}
