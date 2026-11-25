namespace LogiMarket.Infrastructure.AbstractFactory;

using LogiMarket.Application.Ports;
using LogiMarket.Domain.Enums;

public sealed class GuiaEntrega : IGuiaEntrega
{
    public string Generar(TipoEnvio tipo, string codigoPedido) =>
        ServicioEntregaFactoryProvider.ObtenerFabrica(tipo).CrearGeneradorGuia().GenerarGuia(codigoPedido);
    public decimal Tarifa(TipoEnvio tipo) =>
        ServicioEntregaFactoryProvider.ObtenerFabrica(tipo).CrearCalculadorTarifa().ObtenerTarifa();

    public int PlazoDias(TipoEnvio tipo) =>
        ServicioEntregaFactoryProvider.ObtenerFabrica(tipo).CrearCalculadorPlazo().ObtenerPlazoDias();
}
