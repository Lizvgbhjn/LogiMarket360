namespace LogiMarket.Application.Catalogo;

using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public sealed class GestionCatalogo(IProductoRepository productos, IServicioSesion sesion, IKitRepository? kits = null)
{
    public async Task GuardarAsync(Producto datos, bool editar)
    {
        ExigirAdministrador();
        var actual = await productos.ObtenerPorSkuAsync(datos.Sku);

        if (!editar)
        {
            if (actual != null)
                throw new InvalidOperationException("Este SKU ya existe.");

            await productos.AgregarAsync(datos);
            return;
        }

        if (actual == null)
            throw new InvalidOperationException("El producto ya no existe.");

        actual.ActualizarFicha(datos.Nombre, datos.Categoria, datos.Descripcion, datos.Precio, datos.PesoKg);
        await productos.ActualizarAsync(actual);
    }

    public async Task EliminarAsync(string sku)
    {
        ExigirAdministrador();
        if (!await productos.EliminarSinReferenciasAsync(sku))
            throw new InvalidOperationException("El producto no existe o está referenciado por un kit, pedido o movimiento.");
    }

    public async Task CrearKitAsync(string codigo, string nombre)
    {
        ExigirAdministrador();
        await (kits ?? throw new InvalidOperationException("Repositorio de kits no configurado."))
            .AgregarAsync(new Kit(codigo, nombre, "Kit especial compuesto"));
    }

    public async Task AgregarComponenteAsync(string kitCodigo, string hijoCodigo, bool esKit, int cantidad)
    {
        ExigirAdministrador();
        var repositorio = kits ?? throw new InvalidOperationException("Repositorio de kits no configurado.");
        var lista = (await repositorio.ObtenerTodosAsync()).ToList();
        var kit = lista.SingleOrDefault(k => k.Codigo == kitCodigo)
            ?? throw new InvalidOperationException("Seleccione un kit existente.");
        var existente = kit.Componentes.SingleOrDefault(c => c.HijoCodigo == hijoCodigo);
        if (existente != null)
            throw new InvalidOperationException("El componente ya está incluido en el kit.");

        kit.AgregarComponente(new KitComponente(kitCodigo, hijoCodigo, esKit, cantidad));
        var composicion = new ComposicionCatalogo(await productos.ObtenerTodosAsync(), lista);
        _ = composicion.Obtener(kitCodigo, true);
        await repositorio.GuardarComposicionAsync(kitCodigo, kit.Componentes);
    }

    public void ExigirAdministrador()
    {
        if (sesion.RolActual != RolUsuario.ADMIN)
            throw new UnauthorizedAccessException("Solo ADMIN puede modificar el catálogo.");
    }
}
