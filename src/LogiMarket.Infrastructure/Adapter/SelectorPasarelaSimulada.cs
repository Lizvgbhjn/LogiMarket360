namespace LogiMarket.Infrastructure.Adapter;

using LogiMarket.Application.Ports;

public sealed class SelectorPasarelaSimulada(IPasarelaPago moderna, IPasarelaPago legada) : ISelectorPasarelaPago
{
    public IPasarelaPago Obtener(string proveedor) => proveedor switch
    {
        "PasarelaLegada" => legada,
        "PasarelaModerna" => moderna,
        _ => throw new ArgumentException("Proveedor simulado desconocido.", nameof(proveedor))
    };
}
