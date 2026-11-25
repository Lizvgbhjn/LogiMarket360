namespace LogiMarket.Application.Facade;

using System.Text.Json;
using LogiMarket.Application.Chain;
using LogiMarket.Application.DTOs;
using LogiMarket.Application.Observer;
using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;
using LogiMarket.Domain.Exceptions;

public class ConfirmacionPedidoFacade
{
    private readonly IPedidoRepository _pedidoRepo;
    private readonly IProductoRepository _productoRepo;
    private readonly IPagoRepository _pagoRepo;
    private readonly IMovimientoInventarioRepository _movimientoRepo;
    private readonly IEventoOutboxRepository _outboxRepo;
    private readonly IAuditoriaRepository _auditoriaRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasarelaPago _pasarelaPago;
    private readonly ISelectorPasarelaPago? _seleccionarPasarela;
    private readonly IValidadorPedido _pipelineValidador;
    private readonly IServicioSesion _servicioSesion;
    private readonly IPublicadorEventos? _publicador;

    public ConfirmacionPedidoFacade(
        IPedidoRepository pedidoRepo,
        IProductoRepository productoRepo,
        IPagoRepository pagoRepo,
        IMovimientoInventarioRepository movimientoRepo,
        IEventoOutboxRepository outboxRepo,
        IAuditoriaRepository auditoriaRepo,
        IUnitOfWork unitOfWork,
        IPasarelaPago pasarelaPago,
        IValidadorPedido pipelineValidador,
        IServicioSesion servicioSesion,
        IPublicadorEventos? publicador = null,
        ISelectorPasarelaPago? seleccionarPasarela = null)
    {
        _pedidoRepo = pedidoRepo;
        _productoRepo = productoRepo;
        _pagoRepo = pagoRepo;
        _movimientoRepo = movimientoRepo;
        _outboxRepo = outboxRepo;
        _auditoriaRepo = auditoriaRepo;
        _unitOfWork = unitOfWork;
        _pasarelaPago = pasarelaPago;
        _seleccionarPasarela = seleccionarPasarela;
        _pipelineValidador = pipelineValidador;
        _servicioSesion = servicioSesion;
        _publicador = publicador;
    }

    public async Task<ResultadoOperacionDto> ConfirmarPedidoAsync(ConfirmarPedidoDto dto, CancellationToken ct = default)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        var pedido = await _pedidoRepo.ObtenerPorIdAsync(dto.PedidoId, ct);
        if (pedido == null)
        {
            return new ResultadoOperacionDto(false, $"No se encontró el pedido con ID {dto.PedidoId}.");
        }

        // 1. Verificación de Idempotencia: Si ya está pagado con la misma clave, retornar éxito sin re-cobrar
        if (pedido.Estado == EstadoPedido.PAGADO)
        {
            var pagoExistente = await _pagoRepo.ObtenerPorClaveIdempotenciaAsync(dto.ClaveIdempotencia, ct);
            if (pagoExistente != null && pagoExistente.PedidoId == pedido.Id)
            {
                return new ResultadoOperacionDto(true, "Operación idempotente: El pedido ya fue confirmado y pagado previamente.", new
                {
                    PedidoId = pedido.Id,
                    Codigo = pedido.Codigo,
                    Total = pedido.Total,
                    ReferenciaPago = pagoExistente.ReferenciaTransaccion,
                    Repetido = true
                });
            }

            return new ResultadoOperacionDto(false, "El pedido ya se encuentra PAGADO.");
        }

        // 2. Ejecución del pipeline de validación (Chain of Responsibility)
        var resultadoValidacion = await _pipelineValidador.ValidarAsync(pedido, ct);
        if (!resultadoValidacion.EsValido)
        {
            return new ResultadoOperacionDto(false, $"Validación fallida en '{resultadoValidacion.PasoFallido}': {resultadoValidacion.MensajeError}");
        }

        var pasarela = _seleccionarPasarela?.Obtener(dto.ProveedorSimulado) ?? _pasarelaPago;
        ResultadoCobro cobro;
        try
        {
            await _unitOfWork.BeginTransactionAsync(ct);
            if (pedido.Estado == EstadoPedido.BORRADOR)
                pedido.Validar();

            // Descuento de existencias y registro de movimientos de inventario por SKU terminal
            foreach (var comp in pedido.ComponentesFisicosSnapshot)
            {
                var producto = await _productoRepo.ObtenerPorSkuAsync(comp.Sku, ct);
                if (producto == null)
                    throw new StockInsuficienteException($"Producto con SKU '{comp.Sku}' no encontrado al persistir stock.");

                var stockAnterior = producto.Stock;
                producto.DescontarStock(comp.CantidadTotalFisica);
                await _productoRepo.ActualizarAsync(producto, ct);

                var mov = new MovimientoInventario
                {
                    Sku = comp.Sku,
                    TipoMovimiento = TipoMovimientoInventario.VENTA,
                    Cantidad = -comp.CantidadTotalFisica,
                    StockAnterior = stockAnterior,
                    StockPosterior = producto.Stock,
                    Referencia = $"VENTA-{pedido.Codigo}",
                    Fecha = DateTime.UtcNow
                };
                await _movimientoRepo.AgregarAsync(mov, ct);
            }

            // La autorización simulada no mueve fondos ni crea un asiento en un sistema externo.
            // Solo se solicita después de que el inventario pudo reservarse en la transacción local.
            cobro = await pasarela.CobrarAsync(
                pedido.Total, dto.ClaveIdempotencia, $"Cobro de pedido {pedido.Codigo}", ct);
            if (!cobro.Exitoso)
            {
                await _unitOfWork.RollbackAsync(CancellationToken.None);
                return new ResultadoOperacionDto(false, $"Cobro rechazado: {cobro.Mensaje}");
            }

            // Transición a PAGADO
            pedido.Pagar(cobro.ReferenciaTransaccion);
            await _pedidoRepo.ActualizarAsync(pedido, ct);

            // Registro de Pago
            var pago = new Pago(pedido.Id, pedido.Total, cobro.ReferenciaTransaccion,
                dto.ClaveIdempotencia, cobro.Proveedor, cobro.Mensaje);
            await _pagoRepo.AgregarAsync(pago, ct);

            // Registro de Evento Outbox para garantizar publicación durable post-commit
            var eventoPayload = JsonSerializer.Serialize(new
            {
                PedidoId = pedido.Id,
                Codigo = pedido.Codigo,
                Cliente = pedido.Cliente?.Nombre,
                Total = pedido.Total,
                ReferenciaPago = cobro.ReferenciaTransaccion,
                Fecha = DateTime.UtcNow
            });

            var eventoOutbox = new EventoOutbox
            {
                TipoEvento = "PedidoPagado",
                ContenidoJson = eventoPayload,
                FechaOcurrencia = DateTime.UtcNow,
                Estado = EstadoOutbox.PENDIENTE,
                Reintentos = 0
            };
            await _outboxRepo.AgregarAsync(eventoOutbox, ct);

            // Registro de Auditoría
            var auditoria = new Auditoria
            {
                Usuario = _servicioSesion.UsuarioActual?.Username ?? "SISTEMA",
                Accion = "CONFIRMAR_PEDIDO",
                Entidad = "Pedido",
                Detalle = $"Pedido {pedido.Codigo} confirmado con total {pedido.Total:F2} USD. Ref: {cobro.ReferenciaTransaccion}.",
                Fecha = DateTime.UtcNow,
                Exitoso = true
            };
            await _auditoriaRepo.AgregarAsync(auditoria, ct);

            // Commit transaccional
            await _unitOfWork.CommitAsync(ct);
        }
        catch (StockInsuficienteException sex)
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            return new ResultadoOperacionDto(false, $"Stock insuficiente: {sex.Message}");
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            return new ResultadoOperacionDto(false, $"Error al confirmar pedido por concurrencia o integridad: {ex.Message}");
        }

        // 5. Post-confirmación: Métricas y Observadores
        if (_publicador != null)
        {
            await _publicador.PublicarAsync(new EventoNegocio(
                "PedidoPagado",
                pedido.Id,
                pedido.Codigo,
                pedido.Total,
                $"Confirmación exitosa de {pedido.Codigo}",
                DateTime.UtcNow
            ), ct);
        }

        return new ResultadoOperacionDto(true, $"Pedido {pedido.Codigo} confirmado y pagado exitosamente.", new
        {
            PedidoId = pedido.Id,
            Codigo = pedido.Codigo,
            Total = pedido.Total,
            ReferenciaPago = cobro.ReferenciaTransaccion
        });
    }
}
