namespace LogiMarket.Application.Catalogo;

using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public sealed class GestionInventario(
    IProductoRepository productos, IMovimientoInventarioRepository movimientos,
    IAuditoriaRepository auditoria, IServicioSesion sesion, IUnitOfWork unidad)
{
    public async Task AjustarAsync(string sku, int delta, string motivo)
    {
        if (sesion.RolActual is not (RolUsuario.ADMIN or RolUsuario.BODEGA))
            throw new UnauthorizedAccessException("Solo ADMIN y BODEGA pueden ajustar inventario.");
        if (delta == 0 || delta == int.MinValue || string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Introduzca un ajuste válido y un motivo.");

        try
        {
            await unidad.BeginTransactionAsync();
            var producto = await productos.ObtenerPorSkuAsync(sku)
                ?? throw new InvalidOperationException("Producto no encontrado.");
            var anterior = producto.Stock;
            if (delta > 0)
                producto.ReintegrarStock(delta);
            else
                producto.DescontarStock(-delta);

            await productos.ActualizarAsync(producto);
            await movimientos.AgregarAsync(new MovimientoInventario
            {
                Sku = producto.Sku,
                TipoMovimiento = TipoMovimientoInventario.AJUSTE,
                Cantidad = delta,
                StockAnterior = anterior,
                StockPosterior = producto.Stock,
                Referencia = motivo.Trim(),
                Fecha = DateTime.UtcNow
            });
            await auditoria.AgregarAsync(new Auditoria
            {
                Usuario = sesion.UsuarioActual!.Username,
                Accion = "AJUSTE_INVENTARIO",
                Entidad = "Producto",
                Detalle = $"SKU {sku}: {anterior} → {producto.Stock}. Motivo: {motivo.Trim()}",
                Fecha = DateTime.UtcNow,
                Exitoso = true
            });
            await unidad.CommitAsync();
        }
        catch
        {
            await unidad.RollbackAsync();
            throw;
        }
    }
}
