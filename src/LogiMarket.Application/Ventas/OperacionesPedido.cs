namespace LogiMarket.Application.Ventas;

using System.Text.Json;
using LogiMarket.Application.Command;
using LogiMarket.Application.DTOs;
using LogiMarket.Application.Observer;
using LogiMarket.Application.Ports;
using LogiMarket.Application.Singleton;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public sealed class OperacionesPedido(
    IPedidoRepository pedidos, IDespachoRepository despachos, IProductoRepository productos,
    IDevolucionRepository devoluciones, IMovimientoInventarioRepository movimientos,
    IEventoOutboxRepository outbox, IAuditoriaRepository auditoria, IUnitOfWork unidad,
    IServicioSesion sesion, IGuiaEntrega entrega, IMetricasSesion metricas,
    IPublicadorEventos publicador, CommandInvoker invocador)
{
    public Task<ResultadoOperacionDto> DespacharAsync(DespacharPedidoDto datos) =>
        invocador.EjecutarAsync(new DespacharPedidoCommand(
            pedidos, despachos, unidad, auditoria, sesion, datos, publicador, outbox, entrega));

    public Task<ResultadoOperacionDto> CancelarAsync(int id, string motivo) =>
        invocador.EjecutarAsync(new CancelarPedidoCommand(pedidos, unidad, auditoria, sesion, id, motivo));

    public Task<ResultadoOperacionDto> DevolverAsync(SolicitarDevolucionDto datos) =>
        invocador.EjecutarAsync(new ProcesarDevolucionCommand(
            pedidos, productos, devoluciones, movimientos, outbox, auditoria, unidad, sesion,
            datos, metricas, publicador));

    public async Task<ResultadoOperacionDto> EntregarAsync(int id, CancellationToken ct = default)
    {
        if (sesion.RolActual is not (RolUsuario.ADMIN or RolUsuario.BODEGA))
            return new ResultadoOperacionDto(false, "Solo ADMIN y BODEGA pueden registrar entregas.");

        var pedido = await pedidos.ObtenerPorIdAsync(id, ct);
        var despacho = await despachos.ObtenerPorPedidoIdAsync(id, ct);
        if (pedido == null || despacho == null || pedido.Estado != EstadoPedido.DESPACHADO)
            return new ResultadoOperacionDto(false, "El pedido necesita un despacho pendiente de entrega.");

        try
        {
            await unidad.BeginTransactionAsync(ct);
            pedido.Entregar();
            despacho.RegistrarEntrega(pedido.FechaEntrega!.Value);
            await pedidos.ActualizarAsync(pedido, ct);
            await despachos.ActualizarAsync(despacho, ct);
            await outbox.AgregarAsync(new EventoOutbox
            {
                TipoEvento = "EntregaRegistrada",
                ContenidoJson = JsonSerializer.Serialize(new { pedido.Id, pedido.Codigo }),
                FechaOcurrencia = DateTime.UtcNow
            }, ct);
            await auditoria.AgregarAsync(new Auditoria
            {
                Usuario = sesion.UsuarioActual!.Username,
                Accion = "ENTREGA_PEDIDO",
                Entidad = "Pedido",
                Detalle = pedido.Codigo,
                Fecha = DateTime.UtcNow,
                Exitoso = true
            }, ct);
            await unidad.CommitAsync(ct);
        }
        catch
        {
            await unidad.RollbackAsync(CancellationToken.None);
            throw;
        }

        await publicador.PublicarAsync(new EventoNegocio(
            "EntregaRegistrada", pedido.Id, pedido.Codigo, 0m, "Entrega registrada", DateTime.UtcNow), ct);
        return new ResultadoOperacionDto(true, $"Pedido {pedido.Codigo} entregado.");
    }

    public decimal EstimarReembolso(Pedido pedido) => pedido.ImporteReembolsable;
}
