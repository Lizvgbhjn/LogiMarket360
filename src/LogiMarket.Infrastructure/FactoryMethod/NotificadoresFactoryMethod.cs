namespace LogiMarket.Infrastructure.FactoryMethod;

using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;

public record ResultadoNotificacion(bool Exitoso, string Canal, string Destinatario, string Mensaje, DateTime Fecha);

// Interfaz del producto que entrega el Factory Method
public interface INotificador
{
    string Canal { get; }
    Task<ResultadoNotificacion> EnviarAsync(string destinatario, string mensaje, CancellationToken ct = default);
}

// Producto Concreto 1: EMAIL
public class EmailNotificador : INotificador
{
    public string Canal => "EMAIL";

    public Task<ResultadoNotificacion> EnviarAsync(string destinatario, string mensaje, CancellationToken ct = default)
    {
        // Envío simulado verificable
        var resultado = new ResultadoNotificacion(true, Canal, destinatario, mensaje, DateTime.UtcNow);
        return Task.FromResult(resultado);
    }
}

// Producto Concreto 2: SMS
public class SmsNotificador : INotificador
{
    public string Canal => "SMS";

    public Task<ResultadoNotificacion> EnviarAsync(string destinatario, string mensaje, CancellationToken ct = default)
    {
        var resultado = new ResultadoNotificacion(true, Canal, destinatario, mensaje, DateTime.UtcNow);
        return Task.FromResult(resultado);
    }
}

// Creador Abstracto (Factory Method)
public abstract class NotificadorCreator
{
    protected readonly IAuditoriaRepository? _auditoriaRepo;

    protected NotificadorCreator(IAuditoriaRepository? auditoriaRepo = null)
    {
        _auditoriaRepo = auditoriaRepo;
    }

    // El Factory Method obligatorio
    public abstract INotificador CrearNotificador();

    public async Task<ResultadoNotificacion> EnviarAvisoAsync(string destinatario, string mensaje, CancellationToken ct = default)
    {
        var notificador = CrearNotificador();
        var resultado = await notificador.EnviarAsync(destinatario, mensaje, ct);

        if (_auditoriaRepo != null)
        {
            await _auditoriaRepo.AgregarAsync(new Auditoria
            {
                Usuario = "SISTEMA_NOTIFICACIONES",
                Accion = $"AVISO_{resultado.Canal}",
                Entidad = "Notificacion",
                Detalle = $"Destinatario: {destinatario} | Msg: {mensaje}",
                Fecha = DateTime.UtcNow,
                Exitoso = resultado.Exitoso
            }, ct);
        }

        return resultado;
    }
}

// Creador Concreto 1: EMAIL
public class EmailNotificadorCreator : NotificadorCreator
{
    public EmailNotificadorCreator(IAuditoriaRepository? auditoriaRepo = null) : base(auditoriaRepo) { }

    public override INotificador CrearNotificador()
    {
        return new EmailNotificador();
    }
}

// Creador Concreto 2: SMS
public class SmsNotificadorCreator : NotificadorCreator
{
    public SmsNotificadorCreator(IAuditoriaRepository? auditoriaRepo = null) : base(auditoriaRepo) { }

    public override INotificador CrearNotificador()
    {
        return new SmsNotificador();
    }
}
