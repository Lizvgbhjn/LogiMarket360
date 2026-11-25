namespace LogiMarket.Infrastructure.Outbox;

using System.Diagnostics;
using LogiMarket.Application.Ports;
using Microsoft.Extensions.DependencyInjection;

public sealed class OutboxAutomatico(IServiceScopeFactory scopes) : IOutboxProcessor, IDisposable
{
    private readonly SemaphoreSlim _exclusion = new(1, 1);

    public Task<int> ProcesarPendientesAsync(CancellationToken ct = default) => EjecutarCicloAsync(false, ct);

    public async Task<int> EjecutarCicloAsync(bool recuperarFallidos, CancellationToken ct = default)
    {
        await _exclusion.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var scope = scopes.CreateScope();
            var procesador = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
            return recuperarFallidos
                ? await procesador.ReprocesarFallidosAsync(ct).ConfigureAwait(false)
                : await procesador.ProcesarPendientesAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _exclusion.Release();
        }
    }

    public async Task EjecutarAsync(TimeSpan intervalo, CancellationToken ct)
    {
        using var temporizador = new PeriodicTimer(intervalo);
        try
        {
            do
            {
                try
                {
                    await EjecutarCicloAsync(true, ct).ConfigureAwait(false);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    Trace.TraceError("Outbox no pudo completar el ciclo: {0}", error);
                }
            }
            while (await temporizador.WaitForNextTickAsync(ct).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // El formulario está cerrando.
        }
    }

    public void Dispose() => _exclusion.Dispose();
}
