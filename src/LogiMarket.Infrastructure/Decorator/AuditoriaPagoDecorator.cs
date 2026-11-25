namespace LogiMarket.Infrastructure.Decorator;

using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;

public class AuditoriaPagoDecorator : IPasarelaPago
{
    private readonly IPasarelaPago _inner;
    private readonly IAuditoriaRepository _auditoriaRepo;
    private readonly IServicioSesion _servicioSesion;

    public string NombreProveedor => _inner.NombreProveedor;

    public AuditoriaPagoDecorator(IPasarelaPago inner, IAuditoriaRepository auditoriaRepo, IServicioSesion servicioSesion)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _auditoriaRepo = auditoriaRepo ?? throw new ArgumentNullException(nameof(auditoriaRepo));
        _servicioSesion = servicioSesion ?? throw new ArgumentNullException(nameof(servicioSesion));
    }

    public async Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, string concepto, CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var usuario = _servicioSesion.UsuarioActual?.Username ?? "SISTEMA";

        // 1. Log INICIO
        await _auditoriaRepo.AgregarAsync(new Auditoria
        {
            Usuario = usuario,
            Accion = "COBRO_INICIO",
            Entidad = "PasarelaPago",
            Detalle = $"[INICIO] Cobro {correlationId} | Prov: {NombreProveedor} | Monto: {monto:F2} USD | RefIdem: {claveIdempotencia}",
            Fecha = DateTime.UtcNow,
            Exitoso = true
        }, ct);

        try
        {
            // 2. Ejecutar cobro real
            var resultado = await _inner.CobrarAsync(monto, claveIdempotencia, concepto, ct);

            // 3. Log FIN
            await _auditoriaRepo.AgregarAsync(new Auditoria
            {
                Usuario = usuario,
                Accion = resultado.Exitoso ? "COBRO_EXITOSO" : "COBRO_RECHAZADO",
                Entidad = "PasarelaPago",
                Detalle = $"[FIN] Cobro {correlationId} | Estado: {(resultado.Exitoso ? "APROBADO" : "RECHAZADO")} | Ref: {resultado.ReferenciaTransaccion} | Msg: {resultado.Mensaje}",
                Fecha = DateTime.UtcNow,
                Exitoso = resultado.Exitoso
            }, ct);

            return resultado;
        }
        catch (Exception ex)
        {
            // Log ERROR
            await _auditoriaRepo.AgregarAsync(new Auditoria
            {
                Usuario = usuario,
                Accion = "COBRO_ERROR",
                Entidad = "PasarelaPago",
                Detalle = $"[ERROR] Cobro {correlationId} | Excepción: {ex.Message}",
                Fecha = DateTime.UtcNow,
                Exitoso = false
            }, ct);
            throw;
        }
    }
}
