namespace LogiMarket.Application.Observer;

public record EventoNegocio(
    string Tipo,
    int EntidadId,
    string CodigoEntidad,
    decimal Monto,
    string Detalle,
    DateTime Timestamp);

public interface IEventoNegocioObserver
{
    string Nombre { get; }
    Task ActualizarAsync(EventoNegocio evento, CancellationToken ct = default);
}

public interface IPublicadorEventos
{
    void Suscribir(IEventoNegocioObserver observador);
    void Desuscribir(IEventoNegocioObserver observador);
    Task PublicarAsync(EventoNegocio evento, CancellationToken ct = default);
}

public class PublicadorEventos : IPublicadorEventos
{
    private readonly List<IEventoNegocioObserver> _observadores = new();

    public PublicadorEventos(IEnumerable<IEventoNegocioObserver>? observadoresIniciales = null)
    {
        if (observadoresIniciales != null)
        {
            _observadores.AddRange(observadoresIniciales);
        }
    }

    public void Suscribir(IEventoNegocioObserver observador)
    {
        if (observador != null && !_observadores.Contains(observador))
        {
            _observadores.Add(observador);
        }
    }

    public void Desuscribir(IEventoNegocioObserver observador)
    {
        _observadores.Remove(observador);
    }

    public async Task PublicarAsync(EventoNegocio evento, CancellationToken ct = default)
    {
        foreach (var observador in _observadores.ToList())
        {
            await observador.ActualizarAsync(evento, ct);
        }
    }
}
