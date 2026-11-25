namespace LogiMarket.Infrastructure.Outbox;

using System.Text.Json;
using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;
using LogiMarket.Infrastructure.FactoryMethod;

public interface IEnvioOutbox
{
    Task EnviarAsync(EventoOutbox evento, CancellationToken ct);
}

public sealed class ConfiguracionEnvioOutbox
{
    public volatile bool Fallar;
    public volatile bool PreferirSms;
}

public sealed class EnvioOutboxSimulado(
    IPedidoRepository pedidos, NotificadorCreator correo, SmsNotificadorCreator sms,
    ConfiguracionEnvioOutbox configuracion) : IEnvioOutbox
{
    public async Task EnviarAsync(EventoOutbox evento, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (configuracion.Fallar)
            throw new InvalidOperationException("El notificador simulado rechazó el aviso.");

        using var json = JsonDocument.Parse(evento.ContenidoJson);
        var datos = json.RootElement;
        if ((!datos.TryGetProperty("PedidoId", out var id) && !datos.TryGetProperty("Id", out id))
            || !id.TryGetInt32(out var idPedido))
            throw new InvalidOperationException("El evento no contiene un pedido válido.");

        var pedido = await pedidos.ObtenerPorIdAsync(idPedido, ct);
        var contacto = pedido?.Cliente?.Contacto.Trim();
        if (string.IsNullOrWhiteSpace(contacto))
            throw new InvalidOperationException("El pedido no tiene un contacto de notificación.");

        var esCorreo = System.Net.Mail.MailAddress.TryCreate(contacto, out _);
        var esTelefono = contacto.Any(char.IsDigit) && contacto.All(c => char.IsDigit(c) || " +-().".Contains(c));
        if (!esCorreo && !esTelefono)
            throw new InvalidOperationException("El contacto del pedido no es un correo ni un teléfono válido.");

        var creador = esTelefono && (configuracion.PreferirSms || !esCorreo) ? sms : correo;
        var resultado = await creador.EnviarAvisoAsync(contacto, $"Evento {evento.TipoEvento} · Pedido {pedido!.Codigo}", ct);
        if (!resultado.Exitoso)
            throw new InvalidOperationException(resultado.Mensaje);
    }
}
