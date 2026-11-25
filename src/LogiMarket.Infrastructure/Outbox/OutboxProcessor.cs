namespace LogiMarket.Infrastructure.Outbox;

using LogiMarket.Application.Ports;
using LogiMarket.Application.Singleton;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;
using LogiMarket.Infrastructure.FactoryMethod;

public class OutboxProcessor : IOutboxProcessor
{
    private readonly IEventoOutboxRepository _outboxRepo;
    private readonly NotificadorCreator _notificadorCreator;
    private readonly IAuditoriaRepository _auditoriaRepo;
    private readonly SmsNotificadorCreator? _notificadorSms;
    private readonly IMetricasSesion? _metricas;

    public bool UsarSms { get; set; }
    public bool InducirFallo { get; set; } = false;

    public OutboxProcessor(
        IEventoOutboxRepository outboxRepo,
        NotificadorCreator notificadorCreator,
        IAuditoriaRepository auditoriaRepo,
        IMetricasSesion? metricas = null,
        SmsNotificadorCreator? notificadorSms = null)
    {
        _outboxRepo = outboxRepo;
        _notificadorCreator = notificadorCreator;
        _auditoriaRepo = auditoriaRepo;
        _metricas = metricas;
        _notificadorSms = notificadorSms;
    }

    public async Task<int> ProcesarPendientesAsync(CancellationToken ct = default)
    {
        var pendientes = await _outboxRepo.ObtenerPendientesAsync(50, ct);
        int procesados = 0;

        foreach (var evento in pendientes)
        {
            if (InducirFallo)
            {
                evento.Reintentos++;
                evento.Estado = EstadoOutbox.FALLIDO;
                evento.UltimoError = "Fallo simulado inducido en procesamiento de Outbox.";
                await _outboxRepo.ActualizarAsync(evento, ct);

                await _auditoriaRepo.AgregarAsync(new Auditoria
                {
                    Usuario = "OUTBOX_PROCESSOR",
                    Accion = "OUTBOX_FALLIDO",
                    Entidad = "EventoOutbox",
                    Detalle = $"Evento {evento.Id} ({evento.TipoEvento}) falló por simulación inducida.",
                    Fecha = DateTime.UtcNow,
                    Exitoso = false
                }, ct);

                continue;
            }

            try
            {
                // Despachar notificación mediante el notificador del Factory Method
                var creador = UsarSms && _notificadorSms != null ? _notificadorSms : _notificadorCreator;
                await creador.EnviarAvisoAsync(
                    creador is SmsNotificadorCreator ? "555-0100" : "cliente@ejemplo.test",
                    $"Evento {evento.TipoEvento}: {evento.ContenidoJson}",
                    ct);

                evento.Estado = EstadoOutbox.PROCESADO;
                evento.FechaProcesamiento = DateTime.UtcNow;
                evento.UltimoError = null;
                await _outboxRepo.ActualizarAsync(evento, ct);

                _metricas?.RegistrarEventoOutboxProcesado();

                await _auditoriaRepo.AgregarAsync(new Auditoria
                {
                    Usuario = "OUTBOX_PROCESSOR",
                    Accion = "OUTBOX_PROCESADO",
                    Entidad = "EventoOutbox",
                    Detalle = $"Evento {evento.Id} ({evento.TipoEvento}) procesado con éxito.",
                    Fecha = DateTime.UtcNow,
                    Exitoso = true
                }, ct);

                procesados++;
            }
            catch (Exception ex)
            {
                evento.Reintentos++;
                evento.Estado = EstadoOutbox.FALLIDO;
                evento.UltimoError = ex.Message;
                await _outboxRepo.ActualizarAsync(evento, ct);
            }
        }

        return procesados;
    }

    public async Task<int> ReprocesarFallidosAsync(CancellationToken ct = default)
    {
        // Restablece los eventos fallidos a PENDIENTE para recuperación (Case P37)
        var todos = await _outboxRepo.ObtenerTodosAsync(ct);
        var fallidos = todos.Where(e => e.Estado == EstadoOutbox.FALLIDO).ToList();

        foreach (var f in fallidos)
        {
            f.Estado = EstadoOutbox.PENDIENTE;
            await _outboxRepo.ActualizarAsync(f, ct);
        }

        // Si InducirFallo está desactivado, procesa los recuperados
        return await ProcesarPendientesAsync(ct);
    }
}
