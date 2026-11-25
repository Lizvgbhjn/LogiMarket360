namespace LogiMarket.Domain.Visitor;

using LogiMarket.Domain.Composite;

public interface IElementoCatalogoVisitor
{
    void Visitar(ProductoSimple producto, int cantidad);
    void Visitar(KitCompuesto kit, int cantidad);
}

public class DesgloseComponentesVisitor : IElementoCatalogoVisitor
{
    private readonly Dictionary<string, (string Nombre, decimal PrecioUnitario, decimal PesoUnitario, int CantidadTotal)> _desglose = new();

    public IReadOnlyDictionary<string, (string Nombre, decimal PrecioUnitario, decimal PesoUnitario, int CantidadTotal)> Desglose => _desglose;

    public void Visitar(ProductoSimple producto, int cantidad)
    {
        if (_desglose.TryGetValue(producto.Sku, out var actual))
        {
            _desglose[producto.Sku] = (producto.Nombre, producto.Precio, producto.PesoKg, actual.CantidadTotal + cantidad);
        }
        else
        {
            _desglose[producto.Sku] = (producto.Nombre, producto.Precio, producto.PesoKg, cantidad);
        }
    }

    public void Visitar(KitCompuesto kit, int cantidad)
    {
        // El visitor puede registrar el nodo del kit si fuera necesario
    }
}

public class EstimacionPesoVisitor : IElementoCatalogoVisitor
{
    public decimal PesoTotalKg { get; private set; } = 0m;

    public void Visitar(ProductoSimple producto, int cantidad)
    {
        PesoTotalKg += producto.PesoKg * cantidad;
    }

    public void Visitar(KitCompuesto kit, int cantidad)
    {
        // El peso de los kits se suma a través de sus componentes físicos terminales
    }
}
