namespace LogiMarket.Domain.Iterator;

using System.Collections;
using LogiMarket.Domain.Composite;

public class ComponenteFisicoIterator : IEnumerator<ComponenteFisico>
{
    private readonly List<ComponenteFisico> _elementos;
    private int _posicion = -1;

    public ComponenteFisicoIterator(KitCompuesto kit)
    {
        if (kit == null) throw new ArgumentNullException(nameof(kit));
        // Agrupa los SKU terminales de la estructura conservando el contrato del iterador.
        _elementos = Recorrer(kit, 1)
            .GroupBy(componente => componente.Sku)
            .Select(grupo => grupo.First() with { Cantidad = grupo.Sum(componente => componente.Cantidad) })
            .OrderBy(componente => componente.Sku)
            .ToList();
    }

    private static IEnumerable<ComponenteFisico> Recorrer(IElementoCatalogo elemento, int cantidad)
    {
        if (elemento is ProductoSimple producto)
        {
            yield return new ComponenteFisico(producto.Sku, producto.Nombre, producto.Precio, producto.PesoKg, cantidad);
            yield break;
        }

        var kit = (KitCompuesto)elemento;
        foreach (var item in kit.Componentes)
        foreach (var componente in Recorrer(item.Elemento, checked(cantidad * item.Cantidad)))
            yield return componente;
    }

    public ComponenteFisico Current
    {
        get
        {
            if (_posicion < 0 || _posicion >= _elementos.Count)
                throw new InvalidOperationException("Posición del iterador fuera de rango.");
            return _elementos[_posicion];
        }
    }

    object IEnumerator.Current => Current;

    public bool MoveNext()
    {
        _posicion++;
        return _posicion < _elementos.Count;
    }

    public void Reset()
    {
        _posicion = -1;
    }

    public void Dispose()
    {
        // Limpieza si correspondiera
    }
}
