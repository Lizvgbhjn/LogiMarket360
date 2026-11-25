namespace LogiMarket.Domain.Logistica;

using LogiMarket.Domain.Enums;

public readonly record struct CondicionesEntrega(decimal Tarifa, int PlazoDias)
{
    public static CondicionesEntrega Para(TipoEnvio tipo) => tipo switch
    {
        TipoEnvio.ESTANDAR => new(5m, 3),
        TipoEnvio.EXPRES => new(12m, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(tipo))
    };
}
