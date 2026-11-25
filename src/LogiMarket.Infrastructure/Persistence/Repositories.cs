namespace LogiMarket.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;

public class ProductoRepository : IProductoRepository
{
    private readonly LogiMarketDbContext _context;

    public ProductoRepository(LogiMarketDbContext context) => _context = context;

    public Task<Producto?> ObtenerPorSkuAsync(string sku, CancellationToken ct = default)
    {
        return _context.Productos.FirstOrDefaultAsync(p => p.Sku == sku.Trim().ToUpperInvariant(), ct);
    }

    public async Task<IReadOnlyList<Producto>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        return await _context.Productos.AsNoTracking().OrderBy(p => p.Sku).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Producto>> BuscarAsync(string termino, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(termino))
            return await ObtenerTodosAsync(ct);

        var t = termino.Trim().ToLower();
        return await _context.Productos.AsNoTracking()
            .Where(p => p.Sku.ToLower().Contains(t) || p.Nombre.ToLower().Contains(t) || p.Categoria.ToLower().Contains(t))
            .OrderBy(p => p.Sku)
            .ToListAsync(ct);
    }

    public async Task AgregarAsync(Producto producto, CancellationToken ct = default)
    {
        await _context.Productos.AddAsync(producto, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task ActualizarAsync(Producto producto, CancellationToken ct = default)
    {
        _context.Productos.Update(producto);
        await _context.SaveChangesAsync(ct);
    }
    public async Task<bool> EliminarSinReferenciasAsync(string sku, CancellationToken ct = default)
    {
        var eliminados = await _context.Productos
            .Where(producto => producto.Sku == sku
                && !_context.KitComponentes.Any(componente => !componente.EsHijoKit && componente.HijoCodigo == sku)
                && !_context.PedidoDetalleComponentes.Any(componente => componente.Sku == sku)
                && !_context.MovimientosInventario.Any(movimiento => movimiento.Sku == sku))
            .ExecuteDeleteAsync(ct);
        if (eliminados == 1 && _context.Productos.Local.FirstOrDefault(producto => producto.Sku == sku) is { } local)
            _context.Entry(local).State = EntityState.Detached;

        return eliminados == 1;
    }
}

public class KitRepository : IKitRepository
{
    private readonly LogiMarketDbContext _context;

    public KitRepository(LogiMarketDbContext context) => _context = context;

    public Task<Kit?> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        return _context.Kits.Include(k => k.Componentes)
            .FirstOrDefaultAsync(k => k.Codigo == codigo.Trim().ToUpperInvariant(), ct);
    }

    public async Task<IReadOnlyList<Kit>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        return await _context.Kits.Include(k => k.Componentes).AsNoTracking().ToListAsync(ct);
    }

    public async Task AgregarAsync(Kit kit, CancellationToken ct = default)
    {
        await _context.Kits.AddAsync(kit, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task GuardarComposicionAsync(string kitCodigo, IEnumerable<KitComponente> componentes, CancellationToken ct = default)
    {
        var codigo = kitCodigo.Trim().ToUpperInvariant();
        var componentesExistentes = await _context.KitComponentes.Where(c => c.KitCodigo == codigo).ToListAsync(ct);
        var solicitados = componentes.ToDictionary(componente => componente.HijoCodigo);
        foreach (var actual in componentesExistentes)
        {
            if (!solicitados.Remove(actual.HijoCodigo, out var nuevo))
            {
                _context.KitComponentes.Remove(actual);
                continue;
            }

            actual.Cantidad = nuevo.Cantidad;
            actual.EsHijoKit = nuevo.EsHijoKit;
        }

        foreach (var nuevo in solicitados.Values)
            _context.KitComponentes.Add(new KitComponente(codigo, nuevo.HijoCodigo, nuevo.EsHijoKit, nuevo.Cantidad));
        await _context.SaveChangesAsync(ct);
    }
}

public class ClienteRepository : IClienteRepository
{
    private readonly LogiMarketDbContext _context;

    public ClienteRepository(LogiMarketDbContext context) => _context = context;

    public Task<Cliente?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        return _context.Clientes.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public Task<Cliente?> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        return _context.Clientes.FirstOrDefaultAsync(c => c.Codigo == codigo.Trim().ToUpperInvariant(), ct);
    }

    public async Task<IReadOnlyList<Cliente>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        return await _context.Clientes.AsNoTracking().OrderBy(c => c.Codigo).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Cliente>> BuscarAsync(string termino, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(termino))
            return await ObtenerTodosAsync(ct);

        var t = termino.Trim().ToLower();
        return await _context.Clientes.AsNoTracking()
            .Where(c => c.Codigo.ToLower().Contains(t) || c.Nombre.ToLower().Contains(t))
            .OrderBy(c => c.Codigo)
            .ToListAsync(ct);
    }

    public async Task AgregarAsync(Cliente cliente, CancellationToken ct = default)
    {
        await _context.Clientes.AddAsync(cliente, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task ActualizarAsync(Cliente cliente, CancellationToken ct = default)
    {
        _context.Clientes.Update(cliente);
        await _context.SaveChangesAsync(ct);
    }
}

public class PedidoRepository : IPedidoRepository
{
    private readonly LogiMarketDbContext _context;

    public PedidoRepository(LogiMarketDbContext context) => _context = context;

    public Task<Pedido?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        return _context.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Lineas)
            .Include(p => p.ComponentesFisicosSnapshot)
            .Include(p => p.Pago)
            .Include(p => p.Despacho)
            .Include(p => p.Devolucion)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public Task<Pedido?> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        return _context.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Lineas)
            .Include(p => p.ComponentesFisicosSnapshot)
            .Include(p => p.Pago)
            .Include(p => p.Despacho)
            .Include(p => p.Devolucion)
            .FirstOrDefaultAsync(p => p.Codigo == codigo.Trim().ToUpperInvariant(), ct);
    }

    public Task<Pedido?> ObtenerPorIdempotencyKeyAsync(string key, CancellationToken ct = default)
    {
        return _context.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Lineas)
            .Include(p => p.ComponentesFisicosSnapshot)
            .Include(p => p.Pago)
            .Include(p => p.Despacho)
            .Include(p => p.Devolucion)
            .FirstOrDefaultAsync(p => p.IdempotencyKey == key.Trim(), ct);
    }

    public async Task<IReadOnlyList<Pedido>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        return await _context.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Lineas)
            .Include(p => p.ComponentesFisicosSnapshot)
            .Include(p => p.Pago)
            .Include(p => p.Despacho)
            .Include(p => p.Devolucion)
            .OrderByDescending(p => p.FechaCreacion)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Pedido>> ObtenerPorClienteIdAsync(int clienteId, CancellationToken ct = default)
    {
        return await _context.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Lineas)
            .Where(p => p.ClienteId == clienteId)
            .OrderByDescending(p => p.FechaCreacion)
            .ToListAsync(ct);
    }

    public async Task AgregarAsync(Pedido pedido, CancellationToken ct = default)
    {
        await _context.Pedidos.AddAsync(pedido, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task ActualizarAsync(Pedido pedido, CancellationToken ct = default)
    {
        _context.Pedidos.Update(pedido);
        await _context.SaveChangesAsync(ct);
    }
}

public class UsuarioRepository : IUsuarioRepository
{
    private readonly LogiMarketDbContext _context;

    public UsuarioRepository(LogiMarketDbContext context) => _context = context;

    public Task<Usuario?> ObtenerPorUsernameAsync(string username, CancellationToken ct = default)
    {
        return _context.Usuarios.FirstOrDefaultAsync(u => u.Username == username.Trim().ToLowerInvariant(), ct);
    }

    public async Task<IReadOnlyList<Usuario>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        return await _context.Usuarios.AsNoTracking().ToListAsync(ct);
    }

    public async Task AgregarAsync(Usuario usuario, CancellationToken ct = default)
    {
        await _context.Usuarios.AddAsync(usuario, ct);
        await _context.SaveChangesAsync(ct);
    }
}

public class PagoRepository : IPagoRepository
{
    private readonly LogiMarketDbContext _context;

    public PagoRepository(LogiMarketDbContext context) => _context = context;

    public Task<Pago?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        return _context.Pagos.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public Task<Pago?> ObtenerPorClaveIdempotenciaAsync(string clave, CancellationToken ct = default)
    {
        return _context.Pagos.FirstOrDefaultAsync(p => p.ClaveIdempotencia == clave.Trim(), ct);
    }

    public async Task AgregarAsync(Pago pago, CancellationToken ct = default)
    {
        await _context.Pagos.AddAsync(pago, ct);
        await _context.SaveChangesAsync(ct);
    }
}

public class DespachoRepository : IDespachoRepository
{
    private readonly LogiMarketDbContext _context;

    public DespachoRepository(LogiMarketDbContext context) => _context = context;

    public Task<Despacho?> ObtenerPorPedidoIdAsync(int pedidoId, CancellationToken ct = default)
    {
        return _context.Despachos.FirstOrDefaultAsync(d => d.PedidoId == pedidoId, ct);
    }

    public async Task AgregarAsync(Despacho despacho, CancellationToken ct = default)
    {
        await _context.Despachos.AddAsync(despacho, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task ActualizarAsync(Despacho despacho, CancellationToken ct = default)
    {
        _context.Despachos.Update(despacho);
        await _context.SaveChangesAsync(ct);
    }
}

public class DevolucionRepository : IDevolucionRepository
{
    private readonly LogiMarketDbContext _context;

    public DevolucionRepository(LogiMarketDbContext context) => _context = context;

    public Task<Devolucion?> ObtenerPorPedidoIdAsync(int pedidoId, CancellationToken ct = default)
    {
        return _context.Devoluciones.FirstOrDefaultAsync(d => d.PedidoId == pedidoId, ct);
    }

    public async Task<IReadOnlyList<Devolucion>> ObtenerTodasAsync(CancellationToken ct = default)
    {
        return await _context.Devoluciones.OrderByDescending(d => d.FechaSolicitud).ToListAsync(ct);
    }

    public async Task AgregarAsync(Devolucion devolucion, CancellationToken ct = default)
    {
        await _context.Devoluciones.AddAsync(devolucion, ct);
        await _context.SaveChangesAsync(ct);
    }
}

public class MovimientoInventarioRepository : IMovimientoInventarioRepository
{
    private readonly LogiMarketDbContext _context;

    public MovimientoInventarioRepository(LogiMarketDbContext context) => _context = context;

    public async Task<IReadOnlyList<MovimientoInventario>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        return await _context.MovimientosInventario.OrderByDescending(m => m.Fecha).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MovimientoInventario>> ObtenerPorSkuAsync(string sku, CancellationToken ct = default)
    {
        return await _context.MovimientosInventario
            .Where(m => m.Sku == sku.Trim().ToUpperInvariant())
            .OrderByDescending(m => m.Fecha)
            .ToListAsync(ct);
    }

    public async Task AgregarAsync(MovimientoInventario movimiento, CancellationToken ct = default)
    {
        await _context.MovimientosInventario.AddAsync(movimiento, ct);
        await _context.SaveChangesAsync(ct);
    }
}

public class EventoOutboxRepository : IEventoOutboxRepository
{
    private readonly LogiMarketDbContext _context;

    public EventoOutboxRepository(LogiMarketDbContext context) => _context = context;

    public async Task<IReadOnlyList<EventoOutbox>> ObtenerPendientesAsync(int limite = 20, CancellationToken ct = default)
    {
        return await _context.EventosOutbox
            .Where(e => e.Estado == LogiMarket.Domain.Enums.EstadoOutbox.PENDIENTE)
            .OrderBy(e => e.FechaOcurrencia)
            .Take(limite)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EventoOutbox>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        return await _context.EventosOutbox.OrderByDescending(e => e.FechaOcurrencia).ToListAsync(ct);
    }

    public async Task AgregarAsync(EventoOutbox evento, CancellationToken ct = default)
    {
        await _context.EventosOutbox.AddAsync(evento, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task ActualizarAsync(EventoOutbox evento, CancellationToken ct = default)
    {
        _context.EventosOutbox.Update(evento);
        await _context.SaveChangesAsync(ct);
    }
}

public class AuditoriaRepository : IAuditoriaRepository
{
    private readonly LogiMarketDbContext _context;

    public AuditoriaRepository(LogiMarketDbContext context) => _context = context;

    public async Task<IReadOnlyList<Auditoria>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        return await _context.Auditorias.OrderByDescending(a => a.Fecha).ToListAsync(ct);
    }

    public async Task AgregarAsync(Auditoria auditoria, CancellationToken ct = default)
    {
        await _context.Auditorias.AddAsync(auditoria, ct);
        await _context.SaveChangesAsync(ct);
    }
}
