namespace LogiMarket.Application.Consultas;

using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;

public sealed class ConsultasOperativas(
    IPedidoRepository pedidos, IProductoRepository productos, IKitRepository kits,
    IClienteRepository clientes, IAuditoriaRepository auditoria, IEventoOutboxRepository outbox,
    IDevolucionRepository devoluciones, IMovimientoInventarioRepository movimientos)
{
    public Task<IReadOnlyList<Pedido>> PedidosAsync() => pedidos.ObtenerTodosAsync();
    public Task<Pedido?> PedidoPorIdAsync(int id) => pedidos.ObtenerPorIdAsync(id);
    public Task<Pedido?> PedidoPorCodigoAsync(string codigo) => pedidos.ObtenerPorCodigoAsync(codigo);
    public Task<IReadOnlyList<Producto>> ProductosAsync() => productos.ObtenerTodosAsync();
    public Task<Producto?> ProductoPorSkuAsync(string sku) => productos.ObtenerPorSkuAsync(sku);
    public Task<IReadOnlyList<Producto>> BuscarProductosAsync(string termino) => productos.BuscarAsync(termino);
    public Task<IReadOnlyList<Kit>> KitsAsync() => kits.ObtenerTodosAsync();
    public Task<Kit?> KitPorCodigoAsync(string codigo) => kits.ObtenerPorCodigoAsync(codigo);
    public Task<IReadOnlyList<Cliente>> ClientesAsync() => clientes.ObtenerTodosAsync();
    public Task<Cliente?> ClientePorCodigoAsync(string codigo) => clientes.ObtenerPorCodigoAsync(codigo);
    public Task<IReadOnlyList<Cliente>> BuscarClientesAsync(string termino) => clientes.BuscarAsync(termino);
    public Task<IReadOnlyList<Auditoria>> AuditoriaAsync() => auditoria.ObtenerTodosAsync();
    public Task<IReadOnlyList<EventoOutbox>> EventosAsync() => outbox.ObtenerTodosAsync();
    public Task<IReadOnlyList<EventoOutbox>> EventosPendientesAsync(int limite) => outbox.ObtenerPendientesAsync(limite);
    public Task<IReadOnlyList<Devolucion>> DevolucionesAsync() => devoluciones.ObtenerTodasAsync();
    public Task<Devolucion?> DevolucionPorPedidoAsync(int id) => devoluciones.ObtenerPorPedidoIdAsync(id);
    public Task<IReadOnlyList<MovimientoInventario>> MovimientosAsync() => movimientos.ObtenerTodosAsync();
}
