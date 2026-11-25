namespace LogiMarket.Application.Singleton;

public interface IMetricasSesion
{
    int TotalPedidosConfirmados { get; }
    decimal TotalVentasMonto { get; }
    int TotalDevoluciones { get; }
    decimal TotalReembolsadoMonto { get; }
    int TotalIntentosCobroDenegados { get; }
    int TotalEventosOutboxProcesados { get; }

    void RegistrarVentaConfirmada(decimal monto);
    void RegistrarDevolucion(decimal montoReembolsado);
    void RegistrarCobroDenegado();
    void RegistrarEventoOutboxProcesado();
    void ReiniciarMetricas();
}

public class MetricasSesion : IMetricasSesion
{
    private static readonly object _lock = new();

    private int _totalPedidosConfirmados;
    private decimal _totalVentasMonto;
    private int _totalDevoluciones;
    private decimal _totalReembolsadoMonto;
    private int _totalIntentosCobroDenegados;
    private int _totalEventosOutboxProcesados;

    public int TotalPedidosConfirmados { get { lock (_lock) return _totalPedidosConfirmados; } }
    public decimal TotalVentasMonto { get { lock (_lock) return _totalVentasMonto; } }
    public int TotalDevoluciones { get { lock (_lock) return _totalDevoluciones; } }
    public decimal TotalReembolsadoMonto { get { lock (_lock) return _totalReembolsadoMonto; } }
    public int TotalIntentosCobroDenegados { get { lock (_lock) return _totalIntentosCobroDenegados; } }
    public int TotalEventosOutboxProcesados { get { lock (_lock) return _totalEventosOutboxProcesados; } }

    public void RegistrarVentaConfirmada(decimal monto)
    {
        lock (_lock)
        {
            _totalPedidosConfirmados++;
            _totalVentasMonto += monto;
        }
    }

    public void RegistrarDevolucion(decimal montoReembolsado)
    {
        lock (_lock)
        {
            _totalDevoluciones++;
            _totalReembolsadoMonto += montoReembolsado;
        }
    }

    public void RegistrarCobroDenegado()
    {
        lock (_lock)
        {
            _totalIntentosCobroDenegados++;
        }
    }

    public void RegistrarEventoOutboxProcesado()
    {
        lock (_lock)
        {
            _totalEventosOutboxProcesados++;
        }
    }

    public void ReiniciarMetricas()
    {
        lock (_lock)
        {
            _totalPedidosConfirmados = 0;
            _totalVentasMonto = 0m;
            _totalDevoluciones = 0;
            _totalReembolsadoMonto = 0m;
            _totalIntentosCobroDenegados = 0;
            _totalEventosOutboxProcesados = 0;
        }
    }
}
