namespace LogiMarket.Infrastructure.Demostracion;

using LogiMarket.Application.Ports;
using LogiMarket.Domain.Enums;
using LogiMarket.Infrastructure.Adapter;
using LogiMarket.Infrastructure.Flyweight;
using LogiMarket.Infrastructure.Outbox;

public sealed class DemostracionOperativa(
    IServicioSesion sesion,
    PasarelaModernaSimulada moderna,
    PasarelaLegadaSimulada legada,
    OutboxAutomatico outbox, ConfiguracionEnvioOutbox configuracion) : IDemostracionOperativa
{
    public bool Configurar(bool fallarCobro, bool fallarOutbox, bool usarSms)
    {
        if (sesion.RolActual != RolUsuario.ADMIN)
            return false;

        moderna.InducirError = fallarCobro;
        legada.InducirError = fallarCobro;
        configuracion.Fallar = fallarOutbox;
        configuracion.PreferirSms = usarSms;
        return true;
    }

    public Task<int> ProcesarEventosAsync(bool recuperarFallidos)
    {
        if (sesion.RolActual != RolUsuario.ADMIN)
            return Task.FromResult(-1);

        return outbox.EjecutarCicloAsync(recuperarFallidos);
    }

    public ResultadoDemoCatalogo MedirCatalogo()
    {
        var factory = new CatalogoVisualFlyweightFactory();
        var filas = factory.GenerarRenglonesSimulados(1000);
        return new ResultadoDemoCatalogo(
            filas.Count, factory.TotalFlyweightsInstanciados, filas.Count,
            ReferenceEquals(filas[0].MetadatosInmutables, filas[5].MetadatosInmutables));
    }
}
