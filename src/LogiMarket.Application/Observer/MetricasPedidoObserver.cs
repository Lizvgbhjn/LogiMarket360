namespace LogiMarket.Application.Observer;

using LogiMarket.Application.Singleton;

public sealed class MetricasPedidoObserver(IMetricasSesion metricas) : IEventoNegocioObserver
{
    public string Nombre => "Métricas de pedidos";

    public Task ActualizarAsync(EventoNegocio evento, CancellationToken ct = default)
    {
        if (evento.Tipo == "PedidoPagado")
            metricas.RegistrarVentaConfirmada(evento.Monto);

        return Task.CompletedTask;
    }
}
