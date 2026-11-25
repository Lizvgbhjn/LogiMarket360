namespace LogiMarket.Infrastructure.Adapter;

using LogiMarket.Application.Ports;

public sealed class ReembolsadorSimulado : IReembolsadorSimulado
{
    public Task<string> ReembolsarAsync(int pedidoId, decimal monto, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (pedidoId <= 0 || monto < 0m || decimal.Round(monto, 2) != monto)
            throw new ArgumentException("La solicitud de reembolso simulado no es válida.");

        return Task.FromResult($"REEMBOLSO-SIM-{pedidoId}");
    }
}
