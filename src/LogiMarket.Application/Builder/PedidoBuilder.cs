namespace LogiMarket.Application.Builder;

using LogiMarket.Domain.Composite;
using LogiMarket.Domain.Logistica;
using LogiMarket.Domain.Visitor;
using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;
using LogiMarket.Domain.Exceptions;
using LogiMarket.Domain.Strategy;

public class PedidoBuilder(IGuiaEntrega? entrega = null)
{
    private Cliente? _cliente;
    private TipoEnvio _tipoEnvio = TipoEnvio.ESTANDAR;
    private readonly List<(IElementoCatalogo Elemento, int Cantidad)> _items = new();
    private string _idempotencyKey = Guid.NewGuid().ToString("N");
    private string _codigo = string.Empty;

    public PedidoBuilder ConCliente(Cliente cliente)
    {
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
        return this;
    }

    public PedidoBuilder ConTipoEnvio(TipoEnvio tipoEnvio)
    {
        _tipoEnvio = tipoEnvio;
        return this;
    }

    public PedidoBuilder ConCodigo(string codigo)
    {
        _codigo = codigo;
        return this;
    }

    public PedidoBuilder ConIdempotencyKey(string idempotencyKey)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            _idempotencyKey = idempotencyKey.Trim();
        return this;
    }

    public PedidoBuilder AgregarItem(IElementoCatalogo elemento, int cantidad = 1)
    {
        if (elemento == null) throw new ArgumentNullException(nameof(elemento));
        if (cantidad <= 0) throw new ArgumentException("La cantidad debe ser mayor a cero.", nameof(cantidad));

        _items.Add((elemento, cantidad));
        return this;
    }

    public Pedido Construir(CalculadoraDescuentoContext? calculadoraDescuento = null)
    {
        if (_cliente == null)
            throw new DomainException("No se puede construir un pedido sin cliente asignado.");

        if (!_items.Any())
            throw new DomainException("No se puede construir un pedido sin al menos una línea de producto o kit.");

        var codigoPedido = string.IsNullOrWhiteSpace(_codigo)
            ? $"PED-{Guid.NewGuid():N}"
            : _codigo;

        var pedido = new Pedido(codigoPedido, _cliente.Id, _tipoEnvio, _idempotencyKey) { Cliente = _cliente };

        // 1. Crear líneas con snapshot de precios
        foreach (var (elemento, cantidad) in _items)
        {
            var precioUnitario = elemento.ObtenerPrecio();
            var esKit = elemento is KitCompuesto;
            var detalle = new PedidoDetalle(elemento.Codigo, elemento.Nombre, esKit, cantidad, precioUnitario);
            pedido.AgregarLinea(detalle);
        }

        var desglose = new DesgloseComponentesVisitor();
        foreach (var (elemento, cantidad) in _items)
            elemento.Aceptar(desglose, cantidad);

        foreach (var (sku, info) in desglose.Desglose)
        {
            pedido.AgregarComponenteFisico(new PedidoDetalleComponente(
                sku, info.Nombre, info.CantidadTotal, info.PrecioUnitario, info.PesoUnitario));
        }

        var tarifa = entrega?.Tarifa(_tipoEnvio) ?? CondicionesEntrega.Para(_tipoEnvio).Tarifa;
        pedido.CalcularTotales(tarifa, calculadoraDescuento);

        return pedido;
    }
}
