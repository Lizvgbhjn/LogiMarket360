namespace LogiMarket.Application.Ports;

public interface IReembolsadorSimulado
{
    Task<string> ReembolsarAsync(int pedidoId, decimal monto, CancellationToken ct = default);
}
