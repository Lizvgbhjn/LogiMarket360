namespace LogiMarket.Domain.Entities;

using LogiMarket.Domain.Enums;
using LogiMarket.Domain.Exceptions;
using LogiMarket.Domain.State;
using LogiMarket.Domain.Strategy;

public class Pedido
{
    public int Id { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public int ClienteId { get; private set; }
    public Cliente? Cliente { get; set; }
    public DateTime FechaCreacion { get; private set; } = DateTime.UtcNow;
    public DateTime? FechaPago { get; private set; }
    public DateTime? FechaDespacho { get; private set; }
    public DateTime? FechaEntrega { get; private set; }

    public EstadoPedido Estado { get; private set; } = EstadoPedido.BORRADOR;

    public decimal Subtotal { get; private set; }
    public decimal Descuento { get; private set; }
    public decimal TarifaEnvio { get; private set; }
    public decimal Total { get; private set; }

    public TipoEnvio TipoEnvio { get; private set; } = TipoEnvio.ESTANDAR;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? ReferenciaPago { get; private set; }
    public string? GuiaDespacho { get; private set; }
    public string? MotivoCancelacion { get; private set; }

    private readonly List<PedidoDetalle> _lineas = new();
    private readonly List<PedidoDetalleComponente> _componentesFisicosSnapshot = new();

    public IReadOnlyList<PedidoDetalle> Lineas => _lineas.AsReadOnly();
    public IReadOnlyList<PedidoDetalleComponente> ComponentesFisicosSnapshot => _componentesFisicosSnapshot.AsReadOnly();

    public Pago? Pago { get; private set; }
    public Despacho? Despacho { get; private set; }
    public Devolucion? Devolucion { get; private set; }

    // Objeto Estado del patrón State
    private IPedidoEstado? _estadoInterno;
    public IPedidoEstado EstadoInterno => _estadoInterno ??= PedidoEstadoFactory.Crear(Estado);

    public decimal ImporteReembolsable => Subtotal - Descuento;

    public int TotalUnidadesFisicas => ComponentesFisicosSnapshot.Sum(c => c.CantidadTotalFisica);

    public Pedido() { }

    public Pedido(string codigo, int clienteId, TipoEnvio tipoEnvio = TipoEnvio.ESTANDAR, string? clave = null)
    {
        Codigo = codigo.Trim().ToUpperInvariant();
        ClienteId = clienteId;
        TipoEnvio = tipoEnvio;
        IdempotencyKey = string.IsNullOrWhiteSpace(clave) ? Guid.NewGuid().ToString("N") : clave.Trim();
        Estado = EstadoPedido.BORRADOR;
        _estadoInterno = new BorradorEstado();
        FechaCreacion = DateTime.UtcNow;
    }

    internal void CambiarEstado(IPedidoEstado nuevoEstado)
    {
        _estadoInterno = nuevoEstado;
        Estado = nuevoEstado.Nombre;
    }

    public void Validar()
    {
        EstadoInterno.Validar(this);
    }

    internal void RegistrarReferenciaPago(string referencia)
    {
        if (Estado != EstadoPedido.VALIDADO)
            throw new InvalidOperationException("El pedido debe estar validado para registrar el pago.");

        ReferenciaPago = referencia;
        FechaPago = DateTime.UtcNow;
    }

    public void Pagar(string refPago)
    {
        EstadoInterno.Pagar(this, refPago);
    }

    public void Despachar(string guia)
    {
        EstadoInterno.Despachar(this, guia);
    }

    public void Entregar()
    {
        EstadoInterno.Entregar(this);
    }

    public void Cancelar(string motivo)
    {
        EstadoInterno.Cancelar(this, motivo);
    }

    public void ConfigurarBorrador(int clienteId, TipoEnvio tipoEnvio)
    {
        ExigirBorrador();
        ClienteId = clienteId;
        TipoEnvio = tipoEnvio;
    }

    internal void RegistrarDespacho(string guia)
    {
        if (Estado != EstadoPedido.PAGADO)
            throw new InvalidOperationException("Solo un pedido pagado se puede despachar.");
        GuiaDespacho = guia;
        FechaDespacho = DateTime.UtcNow;
    }

    internal void RegistrarEntrega()
    {
        if (Estado != EstadoPedido.DESPACHADO)
            throw new InvalidOperationException("Solo un pedido despachado se puede entregar.");
        FechaEntrega = DateTime.UtcNow;
    }

    internal void RegistrarCancelacion(string motivo)
    {
        if (Estado is not (EstadoPedido.BORRADOR or EstadoPedido.VALIDADO))
            throw new InvalidOperationException("El pedido ya no se puede cancelar.");
        MotivoCancelacion = motivo;
    }

    public void RegistrarDevolucion(Devolucion devolucion)
    {
        if (Estado != EstadoPedido.ENTREGADO || Devolucion != null || devolucion.PedidoId != Id)
            throw new InvalidOperationException("La devolución debe corresponder a un pedido entregado, sin devolución previa.");

        Devolucion = devolucion;
    }

    private void ExigirBorrador()
    {
        if (Estado != EstadoPedido.BORRADOR)
            throw new InvalidOperationException("Solo se puede modificar un borrador.");
    }

    public void AgregarLinea(PedidoDetalle linea)
    {
        ExigirBorrador();
        _lineas.Add(linea);
    }

    public void QuitarLinea(PedidoDetalle linea)
    {
        ExigirBorrador();
        _lineas.Remove(linea);
    }

    public void CambiarCantidad(PedidoDetalle linea, int cantidad)
    {
        ExigirBorrador();
        if (!_lineas.Contains(linea))
            throw new ArgumentException("La línea no pertenece al pedido.", nameof(linea));

        linea.CambiarCantidad(cantidad);
    }

    public void AgregarComponenteFisico(PedidoDetalleComponente componente)
    {
        ExigirBorrador();
        _componentesFisicosSnapshot.Add(componente);
    }

    public void LimpiarContenido()
    {
        ExigirBorrador();
        _lineas.Clear();
        _componentesFisicosSnapshot.Clear();
    }

    public void CalcularTotales(decimal tarifaEnvio, CalculadoraDescuentoContext? estrategias = null)
    {
        AplicarImportes(0m, tarifaEnvio);
        var descuento = (estrategias ?? new CalculadoraDescuentoContext()).DeterminarDescuento(this);
        AplicarImportes(descuento.MontoDescuento, tarifaEnvio);
    }

    public sealed class ImportesBorrador
    {
        internal ImportesBorrador(Pedido pedido)
        {
            Subtotal = pedido.Subtotal;
            Descuento = pedido.Descuento;
            Tarifa = pedido.TarifaEnvio;
        }

        internal decimal Subtotal { get; }
        internal decimal Descuento { get; }
        internal decimal Tarifa { get; }
    }

    public ImportesBorrador CapturarImportes() => new(this);

    public void RestaurarImportes(ImportesBorrador importes)
    {
        if (Math.Round(Lineas.Sum(l => l.Subtotal), 2, MidpointRounding.AwayFromZero) != importes.Subtotal)
        {
            CalcularTotales(importes.Tarifa);
            return;
        }

        AplicarImportes(importes.Descuento, importes.Tarifa);
    }

    private void AplicarImportes(decimal descuento, decimal tarifaEnvio)
    {
        if (Estado != EstadoPedido.BORRADOR)
            throw new InvalidOperationException("Solo se pueden editar los importes de un borrador.");

        var subtotal = Math.Round(Lineas.Sum(l => l.Subtotal), 2, MidpointRounding.AwayFromZero);
        if (descuento < 0m || descuento > subtotal || tarifaEnvio < 0m)
            throw new ArgumentException("Los importes del pedido no son válidos.");

        Subtotal = subtotal;
        Descuento = Math.Round(descuento, 2, MidpointRounding.AwayFromZero);
        TarifaEnvio = Math.Round(tarifaEnvio, 2, MidpointRounding.AwayFromZero);
        Total = Math.Round(Subtotal - Descuento + TarifaEnvio, 2, MidpointRounding.AwayFromZero);
    }
}

public class PedidoDetalle
{
    public int Id { get; private set; }
    public int PedidoId { get; private set; }
    public string ArticuloCodigo { get; private set; } = string.Empty;
    public string NombreArticulo { get; private set; } = string.Empty;
    public bool EsKit { get; private set; }
    public int Cantidad { get; private set; }
    public decimal PrecioUnitario { get; private set; }
    public decimal Subtotal { get; private set; }

    public PedidoDetalle() { }

    public PedidoDetalle(string articuloCodigo, string nombreArticulo, bool esKit, int cantidad, decimal precioUnitario)
    {
        ArticuloCodigo = articuloCodigo.Trim().ToUpperInvariant();
        NombreArticulo = nombreArticulo.Trim();
        EsKit = esKit;
        PrecioUnitario = precioUnitario;
        CambiarCantidad(cantidad);
    }

    internal void CambiarCantidad(int cantidad)
    {
        if (cantidad <= 0)
            throw new ArgumentOutOfRangeException(nameof(cantidad));

        Cantidad = cantidad;
        Subtotal = Math.Round(cantidad * PrecioUnitario, 2, MidpointRounding.AwayFromZero);
    }
}

public class PedidoDetalleComponente
{
    public int Id { get; private set; }
    public int PedidoId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string NombreProducto { get; private set; } = string.Empty;
    public int CantidadTotalFisica { get; private set; }
    public decimal PrecioUnitarioSnapshot { get; private set; }
    public decimal PesoKgSnapshot { get; private set; }

    public PedidoDetalleComponente() { }

    public PedidoDetalleComponente(string sku, string nombreProducto, int cantidadTotalFisica, decimal precioUnitarioSnapshot, decimal pesoKgSnapshot)
    {
        Sku = sku.Trim().ToUpperInvariant();
        NombreProducto = nombreProducto.Trim();
        CantidadTotalFisica = cantidadTotalFisica;
        PrecioUnitarioSnapshot = precioUnitarioSnapshot;
        PesoKgSnapshot = pesoKgSnapshot;
    }
}

public class Pago
{
    private Pago() { }

    public Pago(int pedidoId, decimal monto, string referencia, string clave, string proveedor, string detalle)
    {
        if (pedidoId <= 0 || monto <= 0 || string.IsNullOrWhiteSpace(referencia) || string.IsNullOrWhiteSpace(clave))
            throw new ArgumentException("El pago requiere pedido, monto positivo y referencias válidas.");

        PedidoId = pedidoId;
        Monto = monto;
        ReferenciaTransaccion = referencia;
        ClaveIdempotencia = clave;
        Proveedor = proveedor;
        Detalle = detalle;
    }

    public int Id { get; private set; }
    public int PedidoId { get; private set; }
    public decimal Monto { get; private set; }
    public string ReferenciaTransaccion { get; private set; } = string.Empty;
    public string ClaveIdempotencia { get; private set; } = string.Empty;
    public string Proveedor { get; private set; } = string.Empty;
    public DateTime Fecha { get; private set; } = DateTime.UtcNow;
    public string EstadoPago { get; private set; } = "APROBADO"; // APROBADO, RECHAZADO
    public string Detalle { get; private set; } = string.Empty;
}

public class Despacho
{
    private Despacho() { }

    public Despacho(int pedidoId, string guia, TipoEnvio tipo, int plazoDias, DateTime fechaDespacho)
    {
        if (pedidoId <= 0 || string.IsNullOrWhiteSpace(guia) || plazoDias < 0)
            throw new ArgumentException("El despacho requiere pedido, guía y plazo válido.");

        PedidoId = pedidoId;
        GuiaSeguimiento = guia;
        TipoEntrega = tipo;
        PlazoDias = plazoDias;
        FechaDespacho = fechaDespacho;
        FechaEntregaEstimada = fechaDespacho.AddDays(plazoDias);
    }

    public void RegistrarEntrega(DateTime fecha)
    {
        if (EstadoDespacho != "EN_TRANSITO" || fecha < FechaDespacho)
            throw new InvalidOperationException("La entrega debe corresponder a un despacho en tránsito.");
        FechaEntregaReal = fecha;
        EstadoDespacho = "ENTREGADO";
    }

    public int Id { get; private set; }
    public int PedidoId { get; private set; }
    public string GuiaSeguimiento { get; private set; } = string.Empty;
    public TipoEnvio TipoEntrega { get; private set; }
    public int PlazoDias { get; private set; }
    public DateTime FechaDespacho { get; private set; } = DateTime.UtcNow;
    public DateTime FechaEntregaEstimada { get; private set; }
    public DateTime? FechaEntregaReal { get; private set; }
    public string EstadoDespacho { get; private set; } = "EN_TRANSITO";
}

public class Devolucion
{
    private Devolucion() { }

    public Devolucion(int pedidoId, MotivoDevolucion motivo, decimal monto, DateTime fecha, string observaciones)
    {
        if (pedidoId < 0 || monto < 0 || !Enum.IsDefined(motivo))
            throw new ArgumentException("La devolución requiere pedido, motivo e importe válidos.");

        PedidoId = pedidoId;
        Motivo = motivo;
        MontoReembolsado = monto;
        FechaSolicitud = fecha;
        Observaciones = observaciones;
    }

    public int Id { get; private set; }
    public int PedidoId { get; private set; }
    public MotivoDevolucion Motivo { get; private set; }
    public decimal MontoReembolsado { get; private set; }
    public DateTime FechaSolicitud { get; private set; } = DateTime.UtcNow;
    public string Estado { get; private set; } = "APROBADA"; // APROBADA, RECHAZADA
    public string Observaciones { get; private set; } = string.Empty;
}

public class MovimientoInventario
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public TipoMovimientoInventario TipoMovimiento { get; set; }
    public int Cantidad { get; set; } // Negativo para venta, positivo para reintegro/ajuste
    public int StockAnterior { get; set; }
    public int StockPosterior { get; set; }
    public string Referencia { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}

public class EventoOutbox
{
    public int Id { get; set; }
    public string TipoEvento { get; set; } = string.Empty;
    public string ContenidoJson { get; set; } = string.Empty;
    public DateTime FechaOcurrencia { get; set; } = DateTime.UtcNow;
    public EstadoOutbox Estado { get; set; } = EstadoOutbox.PENDIENTE;
    public int Reintentos { get; set; } = 0;
    public string? UltimoError { get; set; }
    public DateTime? FechaProcesamiento { get; set; }
}

public class Auditoria
{
    public int Id { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public string Entidad { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public bool Exitoso { get; set; } = true;
    public string IpHost { get; set; } = "127.0.0.1";
}
