namespace LogiMarket.Domain.Enums;

public enum RolUsuario
{
    ADMIN,
    VENTAS,
    BODEGA,
    CONSULTA
}

public enum CategoriaCliente
{
    NORMAL,
    VIP,
    MAYORISTA
}

public enum EstadoPedido
{
    BORRADOR,
    VALIDADO,
    PAGADO,
    DESPACHADO,
    ENTREGADO,
    CANCELADO
}

public enum TipoEnvio
{
    ESTANDAR,
    EXPRES
}

public enum MotivoDevolucion
{
    DEFECTO,
    ARREPENTIMIENTO
}

public enum TipoMovimientoInventario
{
    VENTA,
    DEVOLUCION,
    AJUSTE
}

public enum EstadoOutbox
{
    PENDIENTE,
    PROCESADO,
    FALLIDO
}
