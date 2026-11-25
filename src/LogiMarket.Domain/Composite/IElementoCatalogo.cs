namespace LogiMarket.Domain.Composite;

using LogiMarket.Domain.Visitor;

public record ComponenteFisico(string Sku, string Nombre, decimal PrecioUnitario, decimal PesoKg, int Cantidad);

public interface IElementoCatalogo
{
    string Codigo { get; }
    string Nombre { get; }
    decimal ObtenerPrecio();
    decimal ObtenerPeso();
    IReadOnlyList<ComponenteFisico> ObtenerDesgloseFisico();
    void Aceptar(IElementoCatalogoVisitor visitor, int cantidadAcumulada = 1);
}
