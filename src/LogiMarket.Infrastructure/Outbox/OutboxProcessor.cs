namespace LogiMarket.Infrastructure.Outbox;

using LogiMarket.Application.Ports;
using LogiMarket.Application.Singleton;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public sealed class OutboxProcessor(
    IEventoOutboxRepository eventos, IEnvioOutbox notificador,
    IAuditoriaRepository auditoria, IMetricasSesion? metricas = null) : IOutboxProcessor
{
    public Task<int> ProcesarPendientesAsync(CancellationToken ct = default) => ProcesarAsync(false, ct);

    public Task<int> ReprocesarFallidosAsync(CancellationToken ct = default) => ProcesarAsync(true, ct);

    private async Task<int> ProcesarAsync(bool recuperarFallidos, CancellationToken ct)
    {
        var candidatos = await eventos.ObtenerParaProcesarAsync(50, recuperarFallidos, ct);
        var procesados = 0;
        foreach (var evento in candidatos)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await notificador.EnviarAsync(evento, ct);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                evento.Reintentos++;
                evento.Estado = EstadoOutbox.FALLIDO;
                evento.UltimoError = error.Message;
                await eventos.ActualizarAsync(evento, ct);
                await RegistrarAsync(evento, false, ct);
                continue;
            }

            evento.Estado = EstadoOutbox.PROCESADO;
            evento.FechaProcesamiento = DateTime.UtcNow;
            evento.UltimoError = null;
            await eventos.ActualizarAsync(evento, ct);
            await RegistrarAsync(evento, true, ct);
            metricas?.RegistrarEventoOutboxProcesado();
            procesados++;
        }
        return procesados;
    }

    private Task RegistrarAsync(EventoOutbox evento, bool exitoso, CancellationToken ct) =>
        auditoria.AgregarAsync(new Auditoria
        {
            Usuario = "OUTBOX_PROCESSOR",
            Accion = exitoso ? "OUTBOX_PROCESADO" : "OUTBOX_FALLIDO",
            Entidad = "EventoOutbox",
            Detalle = $"Evento {evento.Id} ({evento.TipoEvento}) · {evento.UltimoError ?? "Aviso procesado"}",
            Fecha = DateTime.UtcNow,
            Exitoso = exitoso
        }, ct);
}
