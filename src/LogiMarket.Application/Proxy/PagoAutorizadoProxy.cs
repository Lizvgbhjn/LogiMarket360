namespace LogiMarket.Application.Proxy;

using LogiMarket.Application.Ports;
using LogiMarket.Application.Singleton;
using LogiMarket.Domain.Enums;
using LogiMarket.Domain.Exceptions;

public class PagoAutorizadoProxy : IPasarelaPago
{
    private readonly IPasarelaPago _pasarelaInterna;
    private readonly IServicioSesion _servicioSesion;
    private readonly IMetricasSesion? _metricas;

    public string NombreProveedor => _pasarelaInterna.NombreProveedor;

    public PagoAutorizadoProxy(IPasarelaPago pasarelaInterna, IServicioSesion servicioSesion, IMetricasSesion? metricas = null)
    {
        _pasarelaInterna = pasarelaInterna ?? throw new ArgumentNullException(nameof(pasarelaInterna));
        _servicioSesion = servicioSesion ?? throw new ArgumentNullException(nameof(servicioSesion));
        _metricas = metricas;
    }

    public async Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, string concepto, CancellationToken ct = default)
    {
        // El Proxy verifica rol VENTAS/ADMIN antes de delegar en Decorator y Adapter.
        // Un rol CONSULTA o BODEGA no debe alcanzar al cobro ni producir LOG de cobro.
        if (!_servicioSesion.EstaAutenticado)
        {
            _metricas?.RegistrarCobroDenegado();
            return new ResultadoCobro(false, string.Empty, 0m, "Cobro rechazado por Proxy: No hay sesión autenticada.", NombreProveedor);
        }

        var rol = _servicioSesion.RolActual;
        if (rol != RolUsuario.ADMIN && rol != RolUsuario.VENTAS)
        {
            _metricas?.RegistrarCobroDenegado();
            return new ResultadoCobro(
                false,
                string.Empty,
                0m,
                $"Cobro rechazado por Proxy: El rol '{rol}' no cuenta con permisos de cobro financiero.",
                NombreProveedor);
        }

        // Usuario autorizado: delega a la pasarela interna (Decorator -> Adapter)
        return await _pasarelaInterna.CobrarAsync(monto, claveIdempotencia, concepto, ct);
    }
}
