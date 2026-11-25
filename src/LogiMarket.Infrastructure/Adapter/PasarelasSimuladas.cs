namespace LogiMarket.Infrastructure.Adapter;

using LogiMarket.Application.Ports;

// Clase legada simulada con interfaz incompatible
public class PasarelaLegadaSimulada
{
    public bool InducirError { get; set; } = false;

    // Operación incompatible requerida por la guía: CobrarCentavos(long, string)
    public string CobrarCentavos(long centavos, string refId)
    {
        if (centavos <= 0)
            throw new ArgumentException("Los centavos deben ser mayores a cero.", nameof(centavos));

        if (string.IsNullOrWhiteSpace(refId))
            throw new ArgumentException("La referencia no puede estar vacía.", nameof(refId));

        if (InducirError)
            throw new InvalidOperationException("Rechazo inducido en Pasarela Legada simulada.");

        return $"LEGACY-AUTH-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }
}

// Adaptador que implementa IPasarelaPago sin modificar el código legado
public class PasarelaLegadaAdapter : IPasarelaPago
{
    private readonly PasarelaLegadaSimulada _legada;

    public string NombreProveedor => "PasarelaLegadaAdapter";

    public PasarelaLegadaAdapter(PasarelaLegadaSimulada legada)
    {
        _legada = legada ?? throw new ArgumentNullException(nameof(legada));
    }

    public Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, string concepto, CancellationToken ct = default)
    {
        try
        {
            // Convierte decimal a centavos enteros (long)
            long centavos = (long)Math.Round(monto * 100m, MidpointRounding.AwayFromZero);
            var authCode = _legada.CobrarCentavos(centavos, claveIdempotencia);

            return Task.FromResult(new ResultadoCobro(
                true,
                authCode,
                monto,
                "Cobro completado exitosamente a través de Pasarela Legada Adaptada.",
                NombreProveedor));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ResultadoCobro(
                false,
                string.Empty,
                monto,
                $"Fallo en pasarela legada: {ex.Message}",
                NombreProveedor));
        }
    }
}

// Segundo proveedor moderno simulado sustituible mediante DI / modo demo
public class PasarelaModernaSimulada : IPasarelaPago
{
    public string NombreProveedor => "PasarelaModernaSimulada";
    public bool InducirError { get; set; } = false;

    public Task<ResultadoCobro> CobrarAsync(decimal monto, string claveIdempotencia, string concepto, CancellationToken ct = default)
    {
        if (InducirError)
        {
            return Task.FromResult(new ResultadoCobro(
                false,
                string.Empty,
                monto,
                "Rechazo inducido en Pasarela Moderna (Fondos insuficientes simulados).",
                NombreProveedor));
        }

        var authCode = $"MOD-AUTH-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        return Task.FromResult(new ResultadoCobro(
            true,
            authCode,
            monto,
            "Transacción aprobada satisfactoriamente por Pasarela Moderna.",
            NombreProveedor));
    }
}
