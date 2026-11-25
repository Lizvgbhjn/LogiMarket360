namespace LogiMarket.Application.Command;

using LogiMarket.Application.DTOs;
using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;
using LogiMarket.Domain.Exceptions;

public record RegistroInvocacionComando(
    string NombreComando,
    DateTime Fecha,
    bool Exitoso,
    string Mensaje,
    string DetalleEntrada);

public interface IComandoOperacion
{
    string Nombre { get; }
    string DetalleEntrada { get; }
    Task<ResultadoOperacionDto> EjecutarAsync(CancellationToken ct = default);
}

public class CommandInvoker
{
    private readonly List<RegistroInvocacionComando> _historial = new();

    public IReadOnlyList<RegistroInvocacionComando> Historial => _historial.AsReadOnly();

    public async Task<ResultadoOperacionDto> EjecutarAsync(IComandoOperacion comando, CancellationToken ct = default)
    {
        if (comando == null) throw new ArgumentNullException(nameof(comando));

        try
        {
            var resultado = await comando.EjecutarAsync(ct);
            _historial.Add(new RegistroInvocacionComando(
                comando.Nombre,
                DateTime.UtcNow,
                resultado.Exitoso,
                resultado.Mensaje,
                comando.DetalleEntrada));
            return resultado;
        }
        catch (Exception ex)
        {
            _historial.Add(new RegistroInvocacionComando(
                comando.Nombre,
                DateTime.UtcNow,
                false,
                ex.Message,
                comando.DetalleEntrada));
            throw;
        }
    }
}
