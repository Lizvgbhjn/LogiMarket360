namespace LogiMarket.Domain.Composite;

using LogiMarket.Domain.Exceptions;
using LogiMarket.Domain.Visitor;
using LogiMarket.Domain.Iterator;

public record ItemKit(IElementoCatalogo Elemento, int Cantidad);

public class KitCompuesto : IElementoCatalogo, IEnumerable<ComponenteFisico>
{
    private readonly List<ItemKit> _componentes = new();

    public string Codigo { get; }
    public string Nombre { get; }
    public string Descripcion { get; }

    public IReadOnlyList<ItemKit> Componentes => _componentes.AsReadOnly();

    public KitCompuesto(string codigo, string nombre, string descripcion = "")
    {
        if (string.IsNullOrWhiteSpace(codigo)) throw new ArgumentException("El código del kit no puede estar vacío.", nameof(codigo));
        Codigo = codigo.Trim().ToUpperInvariant();
        Nombre = nombre.Trim();
        Descripcion = descripcion.Trim();
    }

    public void AgregarComponente(IElementoCatalogo elemento, int cantidad = 1)
    {
        if (elemento == null) throw new ArgumentNullException(nameof(elemento));
        if (cantidad <= 0) throw new ArgumentException("La cantidad debe ser mayor a cero.", nameof(cantidad));

        // Validación anti-ciclos: no permitir que un kit se contenga directa o indirectamente
        if (elemento.Codigo == Codigo || ContieneKitRecursivo(elemento, Codigo))
        {
            throw new CicloDetectadoException($"No se permite referencia circular: El kit '{Codigo}' no puede contenerse a sí mismo.");
        }

        _componentes.Add(new ItemKit(elemento, cantidad));
    }

    private static bool ContieneKitRecursivo(IElementoCatalogo elemento, string codigoBuscado)
    {
        if (elemento is KitCompuesto kitHijo)
        {
            if (kitHijo.Codigo == codigoBuscado) return true;
            foreach (var item in kitHijo.Componentes)
            {
                if (ContieneKitRecursivo(item.Elemento, codigoBuscado))
                    return true;
            }
        }
        return false;
    }

    public decimal ObtenerPrecio()
    {
        return _componentes.Sum(item => item.Elemento.ObtenerPrecio() * item.Cantidad);
    }

    public decimal ObtenerPeso()
    {
        return _componentes.Sum(item => item.Elemento.ObtenerPeso() * item.Cantidad);
    }

    public IReadOnlyList<ComponenteFisico> ObtenerDesgloseFisico() => this.ToList();

    public void Aceptar(IElementoCatalogoVisitor visitor, int cantidadAcumulada = 1)
    {
        visitor.Visitar(this, cantidadAcumulada);
        foreach (var item in _componentes)
        {
            item.Elemento.Aceptar(visitor, cantidadAcumulada * item.Cantidad);
        }
    }

    public IEnumerator<ComponenteFisico> GetEnumerator()
    {
        return new ComponenteFisicoIterator(this);
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
