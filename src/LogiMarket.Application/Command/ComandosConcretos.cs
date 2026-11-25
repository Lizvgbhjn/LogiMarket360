namespace LogiMarket.Application.Command;

using System.Text.Json;
using LogiMarket.Application.DTOs;
using LogiMarket.Application.Facade;
using LogiMarket.Application.Observer;
using LogiMarket.Application.Ports;
using LogiMarket.Application.Singleton;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;
using LogiMarket.Domain.Exceptions;
using LogiMarket.Domain.TemplateMethod;

public class ConfirmarPedidoCommand : IComandoOperacion
{
    private readonly ConfirmacionPedidoFacade _facade;
    private readonly ConfirmarPedidoDto _dto;

    public string Nombre => "ConfirmarPedidoCommand";
    public string DetalleEntrada => $"PedidoId: {_dto.PedidoId}, ClaveIdempotencia: {_dto.ClaveIdempotencia}";

    public ConfirmarPedidoCommand(ConfirmacionPedidoFacade facade, ConfirmarPedidoDto dto)
    {
        _facade = facade ?? throw new ArgumentNullException(nameof(facade));
        _dto = dto ?? throw new ArgumentNullException(nameof(dto));
    }

    public Task<ResultadoOperacionDto> EjecutarAsync(CancellationToken ct = default)
    {
        return _facade.ConfirmarPedidoAsync(_dto, ct);
    }
}

public class CancelarPedidoCommand : IComandoOperacion
{
    private readonly IPedidoRepository _pedidoRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditoriaRepository _auditoriaRepo;
    private readonly IServicioSesion _servicioSesion;
    private readonly int _pedidoId;
    private readonly string _motivo;

    public string Nombre => "CancelarPedidoCommand";
    public string DetalleEntrada => $"PedidoId: {_pedidoId}, Motivo: {_motivo}";

    public CancelarPedidoCommand(
        IPedidoRepository pedidoRepo,
        IUnitOfWork unitOfWork,
        IAuditoriaRepository auditoriaRepo,
        IServicioSesion servicioSesion,
        int pedidoId,
        string motivo)
    {
        _pedidoRepo = pedidoRepo;
        _unitOfWork = unitOfWork;
        _auditoriaRepo = auditoriaRepo;
        _servicioSesion = servicioSesion;
        _pedidoId = pedidoId;
        _motivo = motivo;
    }

    public async Task<ResultadoOperacionDto> EjecutarAsync(CancellationToken ct = default)
    {
        if (_servicioSesion.RolActual is not (RolUsuario.ADMIN or RolUsuario.VENTAS))
            return new ResultadoOperacionDto(false, "Solo ADMIN y VENTAS pueden cancelar pedidos.");

        var pedido = await _pedidoRepo.ObtenerPorIdAsync(_pedidoId, ct);
        if (pedido == null)
            return new ResultadoOperacionDto(false, $"No se encontró el pedido {_pedidoId}.");

        using var tx = await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            pedido.Cancelar(_motivo);
            await _pedidoRepo.ActualizarAsync(pedido, ct);

            await _auditoriaRepo.AgregarAsync(new Auditoria
            {
                Usuario = _servicioSesion.UsuarioActual?.Username ?? "SISTEMA",
                Accion = "CANCELAR_PEDIDO",
                Entidad = "Pedido",
                Detalle = $"Pedido {pedido.Codigo} cancelado. Motivo: {_motivo}",
                Fecha = DateTime.UtcNow,
                Exitoso = true
            }, ct);

            await _unitOfWork.CommitAsync(ct);
            return new ResultadoOperacionDto(true, $"Pedido {pedido.Codigo} cancelado exitosamente.");
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }
    }
}

public class DespacharPedidoCommand : IComandoOperacion
{
    private readonly IPedidoRepository _pedidoRepo;
    private readonly IDespachoRepository _despachoRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditoriaRepository _auditoriaRepo;
    private readonly IServicioSesion _servicioSesion;
    private readonly IPublicadorEventos? _publicador;
    private readonly DespacharPedidoDto _dto;
    private readonly IEventoOutboxRepository? _outboxDespacho;
    private readonly IGuiaEntrega? _entrega;

    public string Nombre => "DespacharPedidoCommand";
    public string DetalleEntrada => $"PedidoId: {_dto.PedidoId}, Guia: {_dto.GuiaDespacho}";

    public DespacharPedidoCommand(
        IPedidoRepository pedidoRepo,
        IDespachoRepository despachoRepo,
        IUnitOfWork unitOfWork,
        IAuditoriaRepository auditoriaRepo,
        IServicioSesion servicioSesion,
        DespacharPedidoDto dto,
        IPublicadorEventos? publicador = null,
        IEventoOutboxRepository? outboxDespacho = null,
        IGuiaEntrega? entrega = null)
    {
        _pedidoRepo = pedidoRepo;
        _despachoRepo = despachoRepo;
        _unitOfWork = unitOfWork;
        _auditoriaRepo = auditoriaRepo;
        _servicioSesion = servicioSesion;
        _dto = dto;
        _publicador = publicador;
        _outboxDespacho = outboxDespacho;
        _entrega = entrega;
    }

    public async Task<ResultadoOperacionDto> EjecutarAsync(CancellationToken ct = default)
    {
        var rol = _servicioSesion.RolActual;
        if (rol != RolUsuario.ADMIN && rol != RolUsuario.BODEGA)
        {
            return new ResultadoOperacionDto(false, $"El rol '{rol}' no tiene permisos para despachar pedidos.");
        }

        var pedido = await _pedidoRepo.ObtenerPorIdAsync(_dto.PedidoId, ct);
        if (pedido == null)
            return new ResultadoOperacionDto(false, $"Pedido {_dto.PedidoId} no encontrado.");

        using var tx = await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            pedido.Despachar(_dto.GuiaDespacho);
            await _pedidoRepo.ActualizarAsync(pedido, ct);

            var plazoDias = _entrega?.PlazoDias(pedido.TipoEnvio) ?? (pedido.TipoEnvio == TipoEnvio.EXPRES ? 1 : 3);
            var despacho = new Despacho(pedido.Id, _dto.GuiaDespacho, pedido.TipoEnvio,
                plazoDias, pedido.FechaDespacho!.Value);
            await _despachoRepo.AgregarAsync(despacho, ct);
            if (_outboxDespacho != null)
            {
                await _outboxDespacho.AgregarAsync(new EventoOutbox
                {
                    TipoEvento = "DespachoRegistrado",
                    ContenidoJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        PedidoId = pedido.Id,
                        pedido.Codigo,
                        Guia = _dto.GuiaDespacho
                    }),
                    FechaOcurrencia = DateTime.UtcNow,
                    Estado = EstadoOutbox.PENDIENTE
                }, ct);
            }

            await _auditoriaRepo.AgregarAsync(new Auditoria
            {
                Usuario = _servicioSesion.UsuarioActual?.Username ?? "SISTEMA",
                Accion = "DESPACHAR_PEDIDO",
                Entidad = "Pedido",
                Detalle = $"Pedido {pedido.Codigo} despachado con guía {_dto.GuiaDespacho}.",
                Fecha = DateTime.UtcNow,
                Exitoso = true
            }, ct);

            await _unitOfWork.CommitAsync(ct);

            if (_publicador != null)
            {
                await _publicador.PublicarAsync(new EventoNegocio(
                    "DespachoRegistrado",
                    pedido.Id,
                    pedido.Codigo,
                    0m,
                    $"Despacho registrado con guía {_dto.GuiaDespacho}",
                    DateTime.UtcNow
                ), ct);
            }

            return new ResultadoOperacionDto(true, $"Pedido {pedido.Codigo} despachado exitosamente con guía {_dto.GuiaDespacho}.");
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }
    }
}

public class ProcesarDevolucionCommand : IComandoOperacion
{
    private readonly IPedidoRepository _pedidoRepo;
    private readonly IProductoRepository _productoRepo;
    private readonly IDevolucionRepository _devolucionRepo;
    private readonly IMovimientoInventarioRepository _movimientoRepo;
    private readonly IEventoOutboxRepository _outboxRepo;
    private readonly IAuditoriaRepository _auditoriaRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServicioSesion _servicioSesion;
    private readonly IMetricasSesion? _metricas;
    private readonly IPublicadorEventos? _publicador;
    private readonly SolicitarDevolucionDto _dto;

    public string Nombre => "ProcesarDevolucionCommand";
    public string DetalleEntrada => $"PedidoId: {_dto.PedidoId}, Motivo: {_dto.Motivo}, SinUso: {_dto.ProductoSinUso}";

    public ProcesarDevolucionCommand(
        IPedidoRepository pedidoRepo,
        IProductoRepository productoRepo,
        IDevolucionRepository devolucionRepo,
        IMovimientoInventarioRepository movimientoRepo,
        IEventoOutboxRepository outboxRepo,
        IAuditoriaRepository auditoriaRepo,
        IUnitOfWork unitOfWork,
        IServicioSesion servicioSesion,
        SolicitarDevolucionDto dto,
        IMetricasSesion? metricas = null,
        IPublicadorEventos? publicador = null)
    {
        _pedidoRepo = pedidoRepo;
        _productoRepo = productoRepo;
        _devolucionRepo = devolucionRepo;
        _movimientoRepo = movimientoRepo;
        _outboxRepo = outboxRepo;
        _auditoriaRepo = auditoriaRepo;
        _unitOfWork = unitOfWork;
        _servicioSesion = servicioSesion;
        _dto = dto;
        _metricas = metricas;
        _publicador = publicador;
    }

    public async Task<ResultadoOperacionDto> EjecutarAsync(CancellationToken ct = default)
    {
        var rol = _servicioSesion.RolActual;
        if (rol != RolUsuario.ADMIN && rol != RolUsuario.VENTAS)
        {
            return new ResultadoOperacionDto(false, $"El rol '{rol}' no está autorizado para procesar devoluciones.");
        }

        var pedido = await _pedidoRepo.ObtenerPorIdAsync(_dto.PedidoId, ct);
        if (pedido == null)
            return new ResultadoOperacionDto(false, $"Pedido {_dto.PedidoId} no encontrado.");

        // Seleccionar política según el Template Method
        PoliticaDevolucionTemplate template = _dto.Motivo switch
        {
            MotivoDevolucion.DEFECTO => new DevolucionPorDefecto(),
            MotivoDevolucion.ARREPENTIMIENTO => new DevolucionPorArrepentimiento(),
            _ => throw new DomainException($"Motivo de devolución no soportado: {_dto.Motivo}")
        };

        var solicitud = new SolicitudDevolucion(
            pedido,
            _dto.Motivo,
            DateTime.UtcNow,
            _dto.ProductoSinUso,
            _dto.Observaciones);

        try
        {
            var resultadoEvaluacion = await template.ProcesarDevolucionAsync(
                solicitud,
                async (resultado, token) =>
                {
                    await _unitOfWork.BeginTransactionAsync(token);
                    // Registrar Devolución
                    var devolucion = new Devolucion(pedido.Id, _dto.Motivo, resultado.MontoReembolso,
                        solicitud.FechaSolicitud, _dto.Observaciones);
                    pedido.RegistrarDevolucion(devolucion);
                    await _devolucionRepo.AgregarAsync(devolucion, token);

                    // Reintegrar stock de cada SKU terminal
                    foreach (var comp in pedido.ComponentesFisicosSnapshot)
                    {
                        var producto = await _productoRepo.ObtenerPorSkuAsync(comp.Sku, token)
                            ?? throw new DomainException($"Producto {comp.Sku} no encontrado para reintegro.");
                        if (producto != null)
                        {
                            var stockAnterior = producto.Stock;
                            producto.ReintegrarStock(comp.CantidadTotalFisica);
                            await _productoRepo.ActualizarAsync(producto, token);

                            await _movimientoRepo.AgregarAsync(new MovimientoInventario
                            {
                                Sku = comp.Sku,
                                TipoMovimiento = TipoMovimientoInventario.DEVOLUCION,
                                Cantidad = comp.CantidadTotalFisica,
                                StockAnterior = stockAnterior,
                                StockPosterior = producto.Stock,
                                Referencia = $"DEV-{pedido.Codigo}",
                                Fecha = DateTime.UtcNow
                            }, ct);
                        }
                    }
                },
                async (resultado, token) =>
                {
                    // Registrar evento Outbox
                    var jsonOutbox = JsonSerializer.Serialize(new
                    {
                        PedidoId = pedido.Id,
                        Codigo = pedido.Codigo,
                        Motivo = _dto.Motivo.ToString(),
                        MontoReembolso = resultado.MontoReembolso,
                        Fecha = DateTime.UtcNow
                    });
                    await _outboxRepo.AgregarAsync(new EventoOutbox
                    {
                        TipoEvento = "DevolucionAceptada",
                        ContenidoJson = jsonOutbox,
                        FechaOcurrencia = DateTime.UtcNow,
                        Estado = EstadoOutbox.PENDIENTE
                    }, ct);
                }, ct);
            if (!resultadoEvaluacion.Aprobada)
                return new ResultadoOperacionDto(false, $"Devolución rechazada: {resultadoEvaluacion.MotivoRechazo}");

            await _auditoriaRepo.AgregarAsync(new Auditoria
            {
                Usuario = _servicioSesion.UsuarioActual?.Username ?? "SISTEMA",
                Accion = "PROCESAR_DEVOLUCION",
                Entidad = "Devolucion",
                Detalle = $"Devolución aprobada para pedido {pedido.Codigo}. Reembolso: {resultadoEvaluacion.MontoReembolso:F2} USD (sin flete). Motivo: {_dto.Motivo}.",
                Fecha = DateTime.UtcNow,
                Exitoso = true
            }, ct);

            await _unitOfWork.CommitAsync(ct);

            _metricas?.RegistrarDevolucion(resultadoEvaluacion.MontoReembolso);

            if (_publicador != null)
            {
                await _publicador.PublicarAsync(new EventoNegocio(
                    "DevolucionAceptada",
                    pedido.Id,
                    pedido.Codigo,
                    resultadoEvaluacion.MontoReembolso,
                    $"Devolución aceptada para pedido {pedido.Codigo}",
                    DateTime.UtcNow
                ), ct);
            }

            return new ResultadoOperacionDto(true, $"Devolución aprobada por USD {resultadoEvaluacion.MontoReembolso:F2}.", new
            {
                PedidoId = pedido.Id,
                MontoReembolso = resultadoEvaluacion.MontoReembolso,
                Motivo = _dto.Motivo.ToString()
            });
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }
    }
}
