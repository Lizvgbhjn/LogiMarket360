namespace LogiMarket.Application.Prototype;

using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;
using LogiMarket.Domain.Exceptions;

public class PedidoBorradorPrototype
{
    public static Pedido Clonar(Pedido original)
    {
        if (original == null) throw new ArgumentNullException(nameof(original));

        // Clona un pedido BORRADOR con sus líneas y estructura anidada,
        // sin copiar identificador de persistencia ni estado de pago.
        // La copia es profunda para sus colecciones mutables.
        var nuevoCodigo = $"CLON-{DateTime.UtcNow:HHmmss}-{Random.Shared.Next(100, 999)}";
        var copia = new Pedido(nuevoCodigo, original.ClienteId, original.TipoEnvio)
        {
            Cliente = original.Cliente
        };

        // Copia profunda de líneas
        foreach (var linea in original.Lineas)
        {
            copia.AgregarLinea(new PedidoDetalle(
                linea.ArticuloCodigo,
                linea.NombreArticulo,
                linea.EsKit,
                linea.Cantidad,
                linea.PrecioUnitario
            ));
        }

        // Copia profunda de componentes físicos snapshot
        foreach (var comp in original.ComponentesFisicosSnapshot)
        {
            copia.AgregarComponenteFisico(new PedidoDetalleComponente(
                comp.Sku,
                comp.NombreProducto,
                comp.CantidadTotalFisica,
                comp.PrecioUnitarioSnapshot,
                comp.PesoKgSnapshot
            ));
        }

        copia.RestaurarImportes(original.CapturarImportes());
        return copia;
    }
}
