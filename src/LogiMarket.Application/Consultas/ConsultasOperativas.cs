namespace LogiMarket.Application.Consultas;

using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;

public interface IConsultaAislada
{
    Task<TResult> EjecutarAsync<TRepositorio, TResult>(Func<TRepositorio, Task<TResult>> operacion)
        where TRepositorio : class;
}

public sealed class ConsultasOperativas(IConsultaAislada consultas)
{
    public Task<IReadOnlyList<Pedido>> PedidosAsync() =>
        consultas.EjecutarAsync<IPedidoRepository, IReadOnlyList<Pedido>>(repositorio => repositorio.ObtenerTodosAsync());

    public Task<Pedido?> PedidoPorIdAsync(int id) =>
        consultas.EjecutarAsync<IPedidoRepository, Pedido?>(repositorio => repositorio.ObtenerPorIdAsync(id));

    public Task<Pedido?> PedidoPorCodigoAsync(string codigo) =>
        consultas.EjecutarAsync<IPedidoRepository, Pedido?>(repositorio => repositorio.ObtenerPorCodigoAsync(codigo));

    public Task<IReadOnlyList<Producto>> ProductosAsync() =>
        consultas.EjecutarAsync<IProductoRepository, IReadOnlyList<Producto>>(repositorio => repositorio.ObtenerTodosAsync());

    public Task<Producto?> ProductoPorSkuAsync(string sku) =>
        consultas.EjecutarAsync<IProductoRepository, Producto?>(repositorio => repositorio.ObtenerPorSkuAsync(sku));

    public Task<IReadOnlyList<Producto>> BuscarProductosAsync(string termino) =>
        consultas.EjecutarAsync<IProductoRepository, IReadOnlyList<Producto>>(repositorio => repositorio.BuscarAsync(termino));

    public Task<IReadOnlyList<Kit>> KitsAsync() =>
        consultas.EjecutarAsync<IKitRepository, IReadOnlyList<Kit>>(repositorio => repositorio.ObtenerTodosAsync());

    public Task<Kit?> KitPorCodigoAsync(string codigo) =>
        consultas.EjecutarAsync<IKitRepository, Kit?>(repositorio => repositorio.ObtenerPorCodigoAsync(codigo));

    public Task<IReadOnlyList<Cliente>> ClientesAsync() =>
        consultas.EjecutarAsync<IClienteRepository, IReadOnlyList<Cliente>>(repositorio => repositorio.ObtenerTodosAsync());

    public Task<Cliente?> ClientePorCodigoAsync(string codigo) =>
        consultas.EjecutarAsync<IClienteRepository, Cliente?>(repositorio => repositorio.ObtenerPorCodigoAsync(codigo));

    public Task<IReadOnlyList<Cliente>> BuscarClientesAsync(string termino) =>
        consultas.EjecutarAsync<IClienteRepository, IReadOnlyList<Cliente>>(repositorio => repositorio.BuscarAsync(termino));

    public Task<IReadOnlyList<Auditoria>> AuditoriaAsync() =>
        consultas.EjecutarAsync<IAuditoriaRepository, IReadOnlyList<Auditoria>>(repositorio => repositorio.ObtenerTodosAsync());

    public Task<IReadOnlyList<EventoOutbox>> EventosAsync() =>
        consultas.EjecutarAsync<IEventoOutboxRepository, IReadOnlyList<EventoOutbox>>(repositorio => repositorio.ObtenerTodosAsync());

    public Task<IReadOnlyList<EventoOutbox>> EventosPendientesAsync(int limite) =>
        consultas.EjecutarAsync<IEventoOutboxRepository, IReadOnlyList<EventoOutbox>>(repositorio => repositorio.ObtenerPendientesAsync(limite));

    public Task<IReadOnlyList<Devolucion>> DevolucionesAsync() =>
        consultas.EjecutarAsync<IDevolucionRepository, IReadOnlyList<Devolucion>>(repositorio => repositorio.ObtenerTodasAsync());

    public Task<Devolucion?> DevolucionPorPedidoAsync(int id) =>
        consultas.EjecutarAsync<IDevolucionRepository, Devolucion?>(repositorio => repositorio.ObtenerPorPedidoIdAsync(id));

    public Task<IReadOnlyList<MovimientoInventario>> MovimientosAsync() =>
        consultas.EjecutarAsync<IMovimientoInventarioRepository, IReadOnlyList<MovimientoInventario>>(repositorio => repositorio.ObtenerTodosAsync());
}
