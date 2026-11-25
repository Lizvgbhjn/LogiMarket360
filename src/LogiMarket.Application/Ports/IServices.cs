namespace LogiMarket.Application.Ports;

using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public record ResultadoCobro(
    bool Exitoso,
    string ReferenciaTransaccion,
    decimal MontoCobrado,
    string Mensaje,
    string Proveedor);

public interface IPasarelaPago
{
    string NombreProveedor { get; }
    Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, string concepto, CancellationToken ct = default);
}

public interface ISelectorPasarelaPago
{
    IPasarelaPago Obtener(string proveedor);
}

public interface IServicioSesion
{
    Usuario? UsuarioActual { get; }
    bool EstaAutenticado { get; }
    RolUsuario? RolActual => UsuarioActual?.Rol;
    void IniciarSesion(Usuario usuario);
    void CerrarSesion();
}

public interface IServicioNotificacion
{
    Task NotificarAsync(string tipoCanal, string destinatario, string mensaje, CancellationToken ct = default);
}

public interface IOutboxProcessor
{
    Task<int> ProcesarPendientesAsync(CancellationToken ct = default);
}
