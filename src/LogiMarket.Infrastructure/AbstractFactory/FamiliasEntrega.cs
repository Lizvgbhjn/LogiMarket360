namespace LogiMarket.Infrastructure.AbstractFactory;

using LogiMarket.Domain.Enums;
using LogiMarket.Domain.Logistica;

// Interfaces de la familia de productos
public interface ICalculadorTarifa
{
    decimal ObtenerTarifa();
}

public interface ICalculadorPlazo
{
    int ObtenerPlazoDias();
    DateTime CalcularFechaEntregaEstimada(DateTime fechaDespacho);
}

public interface IGeneradorGuia
{
    string GenerarGuia(string codigoPedido);
}

// Interfaz de la Fábrica Abstracta
public interface IServicioEntregaFactory
{
    TipoEnvio Tipo { get; }
    ICalculadorTarifa CrearCalculadorTarifa();
    ICalculadorPlazo CrearCalculadorPlazo();
    IGeneradorGuia CrearGeneradorGuia();
}

// ----------------- Familia ESTÁNDAR -----------------
public class CalculadorTarifaEstandar : ICalculadorTarifa
{
    public decimal ObtenerTarifa() => CondicionesEntrega.Para(TipoEnvio.ESTANDAR).Tarifa;
}

public class CalculadorPlazoEstandar : ICalculadorPlazo
{
    public int ObtenerPlazoDias() => CondicionesEntrega.Para(TipoEnvio.ESTANDAR).PlazoDias;
    public DateTime CalcularFechaEntregaEstimada(DateTime fechaDespacho) => fechaDespacho.AddDays(ObtenerPlazoDias());
}

public class GeneradorGuiaEstandar : IGeneradorGuia
{
    public string GenerarGuia(string codigoPedido)
    {
        return $"GUIA-STD-{codigoPedido.Replace("PED-", "")}-{Random.Shared.Next(1000, 9999)}";
    }
}

public class EstandarEntregaFactory : IServicioEntregaFactory
{
    public TipoEnvio Tipo => TipoEnvio.ESTANDAR;
    public ICalculadorTarifa CrearCalculadorTarifa() => new CalculadorTarifaEstandar();
    public ICalculadorPlazo CrearCalculadorPlazo() => new CalculadorPlazoEstandar();
    public IGeneradorGuia CrearGeneradorGuia() => new GeneradorGuiaEstandar();
}

// ----------------- Familia EXPRÉS -----------------
public class CalculadorTarifaExpres : ICalculadorTarifa
{
    public decimal ObtenerTarifa() => CondicionesEntrega.Para(TipoEnvio.EXPRES).Tarifa;
}

public class CalculadorPlazoExpres : ICalculadorPlazo
{
    public int ObtenerPlazoDias() => CondicionesEntrega.Para(TipoEnvio.EXPRES).PlazoDias;
    public DateTime CalcularFechaEntregaEstimada(DateTime fechaDespacho) => fechaDespacho.AddDays(ObtenerPlazoDias());
}

public class GeneradorGuiaExpres : IGeneradorGuia
{
    public string GenerarGuia(string codigoPedido)
    {
        return $"GUIA-EXP-{codigoPedido.Replace("PED-", "")}-{Random.Shared.Next(1000, 9999)}";
    }
}

public class ExpresEntregaFactory : IServicioEntregaFactory
{
    public TipoEnvio Tipo => TipoEnvio.EXPRES;
    public ICalculadorTarifa CrearCalculadorTarifa() => new CalculadorTarifaExpres();
    public ICalculadorPlazo CrearCalculadorPlazo() => new CalculadorPlazoExpres();
    public IGeneradorGuia CrearGeneradorGuia() => new GeneradorGuiaExpres();
}

public static class ServicioEntregaFactoryProvider
{
    public static IServicioEntregaFactory ObtenerFabrica(TipoEnvio tipoEnvio) => tipoEnvio switch
    {
        TipoEnvio.ESTANDAR => new EstandarEntregaFactory(),
        TipoEnvio.EXPRES => new ExpresEntregaFactory(),
        _ => throw new ArgumentOutOfRangeException(nameof(tipoEnvio), $"Tipo de envío no soportado: {tipoEnvio}")
    };
}
