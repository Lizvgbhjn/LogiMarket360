namespace LogiMarket.Application.Chain;

using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public record ResultadoValidacion(bool EsValido, string MensajeError, string PasoFallido)
{
    public static ResultadoValidacion Exitoso() => new(true, string.Empty, string.Empty);
    public static ResultadoValidacion Fallido(string paso, string mensaje) => new(false, mensaje, paso);
}

public interface IValidadorPedido
{
    IValidadorPedido SetSiguiente(IValidadorPedido siguiente);
    Task<ResultadoValidacion> ValidarAsync(Pedido pedido, CancellationToken ct = default);
}

public abstract class ValidadorPedidoBase : IValidadorPedido
{
    protected IValidadorPedido? _siguiente;

    public IValidadorPedido SetSiguiente(IValidadorPedido siguiente)
    {
        _siguiente = siguiente;
        return siguiente;
    }

    public abstract Task<ResultadoValidacion> ValidarAsync(Pedido pedido, CancellationToken ct = default);

    protected async Task<ResultadoValidacion> ContinuarAsync(Pedido pedido, CancellationToken ct)
    {
        if (_siguiente != null)
        {
            return await _siguiente.ValidarAsync(pedido, ct);
        }
        return ResultadoValidacion.Exitoso();
    }
}

public class ClienteActivoValidador : ValidadorPedidoBase
{
    private readonly IClienteRepository _clienteRepo;

    public ClienteActivoValidador(IClienteRepository clienteRepo)
    {
        _clienteRepo = clienteRepo;
    }

    public override async Task<ResultadoValidacion> ValidarAsync(Pedido pedido, CancellationToken ct = default)
    {
        if (pedido.ClienteId <= 0)
        {
            return ResultadoValidacion.Fallido(nameof(ClienteActivoValidador), "El pedido no tiene un cliente asignado.");
        }

        var cliente = await _clienteRepo.ObtenerPorIdAsync(pedido.ClienteId, ct);
        if (cliente == null || !cliente.Activo)
        {
            return ResultadoValidacion.Fallido(nameof(ClienteActivoValidador), $"El cliente con ID {pedido.ClienteId} no existe o se encuentra inactivo.");
        }

        pedido.Cliente = cliente;
        return await ContinuarAsync(pedido, ct);
    }
}

public class LineasPedidoValidador : ValidadorPedidoBase
{
    public override async Task<ResultadoValidacion> ValidarAsync(Pedido pedido, CancellationToken ct = default)
    {
        if (pedido.Lineas == null || !pedido.Lineas.Any())
        {
            return ResultadoValidacion.Fallido(nameof(LineasPedidoValidador), "El pedido no contiene ninguna línea de producto o kit.");
        }

        foreach (var linea in pedido.Lineas)
        {
            if (linea.Cantidad <= 0)
            {
                return ResultadoValidacion.Fallido(nameof(LineasPedidoValidador), $"La cantidad para el artículo '{linea.ArticuloCodigo}' debe ser mayor a cero.");
            }
        }

        return await ContinuarAsync(pedido, ct);
    }
}

public class StockDisponibleValidador : ValidadorPedidoBase
{
    private readonly IProductoRepository _productoRepo;

    public StockDisponibleValidador(IProductoRepository productoRepo)
    {
        _productoRepo = productoRepo;
    }

    public override async Task<ResultadoValidacion> ValidarAsync(Pedido pedido, CancellationToken ct = default)
    {
        if (!pedido.ComponentesFisicosSnapshot.Any())
        {
            return ResultadoValidacion.Fallido(nameof(StockDisponibleValidador), "No se han desglosado los componentes físicos del pedido.");
        }

        foreach (var comp in pedido.ComponentesFisicosSnapshot)
        {
            var producto = await _productoRepo.ObtenerPorSkuAsync(comp.Sku, ct);
            if (producto == null)
            {
                return ResultadoValidacion.Fallido(nameof(StockDisponibleValidador), $"El producto con SKU '{comp.Sku}' no existe en el catálogo.");
            }

            if (producto.Stock < comp.CantidadTotalFisica)
            {
                return ResultadoValidacion.Fallido(nameof(StockDisponibleValidador),
                    $"Stock insuficiente para '{producto.Nombre}' ({comp.Sku}): disponible {producto.Stock}, requerido {comp.CantidadTotalFisica}.");
            }
        }

        return await ContinuarAsync(pedido, ct);
    }
}

public class PermisoConfirmacionValidador : ValidadorPedidoBase
{
    private readonly IServicioSesion _servicioSesion;

    public PermisoConfirmacionValidador(IServicioSesion servicioSesion)
    {
        _servicioSesion = servicioSesion;
    }

    public override async Task<ResultadoValidacion> ValidarAsync(Pedido pedido, CancellationToken ct = default)
    {
        if (!_servicioSesion.EstaAutenticado)
        {
            return ResultadoValidacion.Fallido(nameof(PermisoConfirmacionValidador), "Operación no autorizada: No hay sesión activa.");
        }

        var rol = _servicioSesion.RolActual;
        if (rol != RolUsuario.ADMIN && rol != RolUsuario.VENTAS)
        {
            return ResultadoValidacion.Fallido(nameof(PermisoConfirmacionValidador),
                $"Operación no autorizada: El rol '{rol}' no tiene permisos para confirmar pedidos.");
        }

        if (pedido.Estado != EstadoPedido.BORRADOR && pedido.Estado != EstadoPedido.VALIDADO)
        {
            return ResultadoValidacion.Fallido(nameof(PermisoConfirmacionValidador),
                $"No se puede confirmar un pedido en estado '{pedido.Estado}'.");
        }

        return await ContinuarAsync(pedido, ct);
    }
}
