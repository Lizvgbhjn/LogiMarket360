namespace LogiMarket.Infrastructure.Observer;

using LogiMarket.Application.Observer;
using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;

public sealed class EventosOutboxObserver(IOutboxProcessor procesador) : IEventoNegocioObserver
{
    public string Nombre => "Notificaciones pendientes";

    public async Task ActualizarAsync(EventoNegocio evento, CancellationToken ct = default)
    {
        await procesador.ProcesarPendientesAsync(ct);
    }
}

public sealed class AuditoriaEventoObserver(IAuditoriaRepository auditoria) : IEventoNegocioObserver
{
    public string Nombre => "Auditoría de eventos";

    public Task ActualizarAsync(EventoNegocio evento, CancellationToken ct = default)
    {
        return auditoria.AgregarAsync(new Auditoria
        {
            Usuario = "SISTEMA_EVENTOS",
            Accion = evento.Tipo,
            Entidad = "Pedido",
            Detalle = evento.Detalle,
            Fecha = evento.Timestamp,
            Exitoso = true
        }, ct);
    }
}
