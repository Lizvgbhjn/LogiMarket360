namespace LogiMarket.Infrastructure.Persistence;

using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

public sealed class AuditoriaCobroTransaccional(
    IAuditoriaRepository repositorio, IServiceScopeFactory scopes) : IAuditoriaRepository
{
    private List<Auditoria>? _intentos;

    internal void Iniciar() => _intentos = new List<Auditoria>();

    public Task<IReadOnlyList<Auditoria>> ObtenerTodosAsync(CancellationToken ct = default) =>
        repositorio.ObtenerTodosAsync(ct);

    public Task AgregarAsync(Auditoria auditoria, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (_intentos == null)
            return repositorio.AgregarAsync(auditoria, ct);

        _intentos.Add(auditoria);
        return Task.CompletedTask;
    }

    internal async Task GuardarEnTransaccionAsync(CancellationToken ct)
    {
        if (_intentos == null) return;
        foreach (var intento in _intentos)
            await repositorio.AgregarAsync(intento, ct);
    }

    internal void Completar() => _intentos = null;

    internal async Task GuardarTrasRollbackAsync(CancellationToken ct)
    {
        var intentos = _intentos;
        _intentos = null;
        if (intentos is not { Count: > 0 }) return;

        using var scope = scopes.CreateScope();
        var destino = scope.ServiceProvider.GetRequiredService<IAuditoriaRepository>();
        foreach (var intento in intentos)
        {
            var revertido = intento.Accion == "COBRO_EXITOSO";
            await destino.AgregarAsync(new Auditoria
            {
                Usuario = intento.Usuario,
                Accion = revertido ? "COBRO_AUTORIZACION_REVERTIDA" : intento.Accion,
                Entidad = intento.Entidad,
                Detalle = intento.Detalle,
                Fecha = intento.Fecha,
                Exitoso = !revertido && intento.Exitoso
            }, ct);
        }
    }
}
