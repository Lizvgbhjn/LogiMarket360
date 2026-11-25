namespace LogiMarket.Application.Catalogo;

using LogiMarket.Domain.Composite;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Exceptions;

public sealed class ComposicionCatalogo(IEnumerable<Producto> productos, IEnumerable<Kit> kits)
{
    private readonly Dictionary<string, Producto> _productos = productos.ToDictionary(p => p.Sku);
    private readonly Dictionary<string, Kit> _kits = kits.Where(k => k.Activo).ToDictionary(k => k.Codigo);

    public IElementoCatalogo Obtener(string codigo, bool esKit) => Construir(codigo, esKit, new HashSet<string>());

    private IElementoCatalogo Construir(string codigo, bool esKit, HashSet<string> ruta)
    {
        if (!esKit)
        {
            if (!_productos.TryGetValue(codigo, out var producto))
                throw new DomainException($"El producto {codigo} ya no está disponible.");

            return new ProductoSimple(producto.Sku, producto.Nombre, producto.Categoria,
                producto.Descripcion, producto.Precio, producto.PesoKg, producto.Stock);
        }

        if (!_kits.TryGetValue(codigo, out var kit) || kit.Componentes.Count == 0)
            throw new DomainException($"El kit {codigo} no está disponible o no tiene componentes.");
        if (!ruta.Add(codigo))
            throw new CicloDetectadoException($"El kit {codigo} tiene una referencia circular.");

        var compuesto = new KitCompuesto(kit.Codigo, kit.Nombre, kit.Descripcion);
        foreach (var hijo in kit.Componentes)
            compuesto.AgregarComponente(Construir(hijo.HijoCodigo, hijo.EsHijoKit, ruta), hijo.Cantidad);

        ruta.Remove(codigo);
        return compuesto;
    }
}
