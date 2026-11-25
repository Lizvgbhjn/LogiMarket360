namespace LogiMarket.Domain.Composite;

using LogiMarket.Domain.Visitor;

public class ProductoSimple : IElementoCatalogo
{
    public string Sku { get; }
    public string Nombre { get; }
    public string Categoria { get; }
    public string Descripcion { get; }
    public decimal Precio { get; }
    public decimal PesoKg { get; }
    public int Stock { get; private set; }

    public string Codigo => Sku;

    public ProductoSimple(string sku, string nombre, string categoria, string descripcion, decimal precio, decimal pesoKg, int stock)
    {
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("El SKU no puede estar vacío.", nameof(sku));
        if (precio <= 0) throw new ArgumentException("El precio debe ser un valor monetario positivo.", nameof(precio));
        if (pesoKg < 0) throw new ArgumentException("El peso no puede ser negativo.", nameof(pesoKg));
        if (stock < 0) throw new ArgumentException("El stock no puede ser negativo.", nameof(stock));

        Sku = sku.Trim().ToUpperInvariant();
        Nombre = nombre.Trim();
        Categoria = categoria.Trim();
        Descripcion = descripcion.Trim();
        Precio = precio;
        PesoKg = pesoKg;
        Stock = stock;
    }

    public decimal ObtenerPrecio() => Precio;
    public decimal ObtenerPeso() => PesoKg;

    public IReadOnlyList<ComponenteFisico> ObtenerDesgloseFisico()
    {
        return new List<ComponenteFisico>
        {
            new ComponenteFisico(Sku, Nombre, Precio, PesoKg, 1)
        };
    }

    public void Aceptar(IElementoCatalogoVisitor visitor, int cantidadAcumulada = 1)
    {
        visitor.Visitar(this, cantidadAcumulada);
    }
}
