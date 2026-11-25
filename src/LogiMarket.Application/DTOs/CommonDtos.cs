namespace LogiMarket.Application.DTOs;

using LogiMarket.Domain.Enums;

public record UsuarioLoginDto(string Username, string Password);
public record UsuarioSesionDto(int Id, string Username, RolUsuario Rol);

public record LineaItemDto(
    string Codigo,
    string Nombre,
    bool EsKit,
    int Cantidad,
    decimal PrecioUnitario);

public record CrearPedidoDraftDto(
    int ClienteId,
    TipoEnvio TipoEnvio,
    List<LineaItemDto> Lineas);

public record ConfirmarPedidoDto(
    int PedidoId,
    string ClaveIdempotencia,
    string ProveedorSimulado = "PasarelaModerna");

public record DespacharPedidoDto(
    int PedidoId,
    string GuiaDespacho);

public record SolicitarDevolucionDto(
    int PedidoId,
    MotivoDevolucion Motivo,
    bool ProductoSinUso,
    string Observaciones);

public record ResultadoOperacionDto(
    bool Exitoso,
    string Mensaje,
    object? Datos = null);
