namespace LogiMarket.Application.Ports;

using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public interface IUnitOfWork
{
    Task<IDisposable> BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

public interface IProductoRepository
{
    Task<Producto?> ObtenerPorSkuAsync(string sku, CancellationToken ct = default);
    Task<IReadOnlyList<Producto>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Producto>> BuscarAsync(string termino, CancellationToken ct = default);
    Task AgregarAsync(Producto producto, CancellationToken ct = default);
    Task ActualizarAsync(Producto producto, CancellationToken ct = default);
    Task<bool> EliminarSinReferenciasAsync(string sku, CancellationToken ct = default);
}

public interface IKitRepository
{
    Task<Kit?> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default);
    Task<IReadOnlyList<Kit>> ObtenerTodosAsync(CancellationToken ct = default);
    Task AgregarAsync(Kit kit, CancellationToken ct = default);
    Task GuardarComposicionAsync(string kitCodigo, IEnumerable<KitComponente> componentes, CancellationToken ct = default);
}

public interface IClienteRepository
{
    Task<Cliente?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Cliente?> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default);
    Task<IReadOnlyList<Cliente>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Cliente>> BuscarAsync(string termino, CancellationToken ct = default);
    Task AgregarAsync(Cliente cliente, CancellationToken ct = default);
    Task ActualizarAsync(Cliente cliente, CancellationToken ct = default);
}

public interface IPedidoRepository
{
    Task<Pedido?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Pedido?> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default);
    Task<Pedido?> ObtenerPorIdempotencyKeyAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyList<Pedido>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Pedido>> ObtenerPorClienteIdAsync(int clienteId, CancellationToken ct = default);
    Task AgregarAsync(Pedido pedido, CancellationToken ct = default);
    Task ActualizarAsync(Pedido pedido, CancellationToken ct = default);
}

public interface IUsuarioRepository
{
    Task<Usuario?> ObtenerPorUsernameAsync(string username, CancellationToken ct = default);
    Task<IReadOnlyList<Usuario>> ObtenerTodosAsync(CancellationToken ct = default);
    Task AgregarAsync(Usuario usuario, CancellationToken ct = default);
}

public interface IPagoRepository
{
    Task<Pago?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Pago?> ObtenerPorClaveIdempotenciaAsync(string clave, CancellationToken ct = default);
    Task AgregarAsync(Pago pago, CancellationToken ct = default);
}

public interface IDespachoRepository
{
    Task<Despacho?> ObtenerPorPedidoIdAsync(int pedidoId, CancellationToken ct = default);
    Task AgregarAsync(Despacho despacho, CancellationToken ct = default);
    Task ActualizarAsync(Despacho despacho, CancellationToken ct = default);
}

public interface IDevolucionRepository
{
    Task<Devolucion?> ObtenerPorPedidoIdAsync(int pedidoId, CancellationToken ct = default);
    Task<IReadOnlyList<Devolucion>> ObtenerTodasAsync(CancellationToken ct = default);
    Task AgregarAsync(Devolucion devolucion, CancellationToken ct = default);
}

public interface IMovimientoInventarioRepository
{
    Task<IReadOnlyList<MovimientoInventario>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MovimientoInventario>> ObtenerPorSkuAsync(string sku, CancellationToken ct = default);
    Task AgregarAsync(MovimientoInventario movimiento, CancellationToken ct = default);
}

public interface IEventoOutboxRepository
{
    Task<IReadOnlyList<EventoOutbox>> ObtenerParaProcesarAsync(int limite, bool recuperarFallidos, CancellationToken ct = default);
    Task<IReadOnlyList<EventoOutbox>> ObtenerPendientesAsync(int limite = 20, CancellationToken ct = default);
    Task<IReadOnlyList<EventoOutbox>> ObtenerTodosAsync(CancellationToken ct = default);
    Task AgregarAsync(EventoOutbox evento, CancellationToken ct = default);
    Task ActualizarAsync(EventoOutbox evento, CancellationToken ct = default);
}

public interface IAuditoriaRepository
{
    Task<IReadOnlyList<Auditoria>> ObtenerTodosAsync(CancellationToken ct = default);
    Task AgregarAsync(Auditoria auditoria, CancellationToken ct = default);
}
