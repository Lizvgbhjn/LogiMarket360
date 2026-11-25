namespace LogiMarket.Infrastructure.Persistence;

using LogiMarket.Application.Consultas;
using Microsoft.Extensions.DependencyInjection;

public sealed class ConsultaAislada(IServiceScopeFactory scopes) : IConsultaAislada
{
    public async Task<TResult> EjecutarAsync<TRepositorio, TResult>(Func<TRepositorio, Task<TResult>> operacion)
        where TRepositorio : class
    {
        using var scope = scopes.CreateScope();
        return await operacion(scope.ServiceProvider.GetRequiredService<TRepositorio>()).ConfigureAwait(false);
    }
}
