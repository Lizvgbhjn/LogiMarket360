namespace LogiMarket.Infrastructure.Flyweight;

using System.Collections.Concurrent;
using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;

// Estado intrínseco inmutable (Flyweight compartido)
public class ProductoFlyweight : IMetadatosProducto
{
    public string Sku { get; }
    public string Nombre { get; }
    public string Categoria { get; }
    public string Descripcion { get; }

    public ProductoFlyweight(string sku, string nombre, string categoria, string descripcion)
    {
        Sku = sku;
        Nombre = nombre;
        Categoria = categoria;
        Descripcion = descripcion;
    }
}

// Estado extrínseco por fila / renglón de vista
public class RenglonVisualCatalogo
{
    public ProductoFlyweight MetadatosInmutables { get; }
    public int FilaId { get; set; }
    public int StockActual { get; set; }
    public int CantidadDeseada { get; set; }
    public decimal PrecioVentaFila { get; set; }
    public bool Seleccionado { get; set; }

    public string Sku => MetadatosInmutables.Sku;
    public string Nombre => MetadatosInmutables.Nombre;
    public string Categoria => MetadatosInmutables.Categoria;
    public string Descripcion => MetadatosInmutables.Descripcion;

    public RenglonVisualCatalogo(ProductoFlyweight metadatos, int filaId, int stockActual, decimal precioVenta)
    {
        MetadatosInmutables = metadatos ?? throw new ArgumentNullException(nameof(metadatos));
        FilaId = filaId;
        StockActual = stockActual;
        PrecioVentaFila = precioVenta;
        CantidadDeseada = 1;
        Seleccionado = false;
    }
}

// Fábrica Flyweight
public class CatalogoVisualFlyweightFactory : IMetadatosCatalogo
{
    private readonly ConcurrentDictionary<string, ProductoFlyweight> _flyweights = new();

    public int TotalFlyweightsInstanciados => _flyweights.Count;

    public ProductoFlyweight ObtenerFlyweight(string sku, string nombre, string categoria, string descripcion)
    {
        var clave = sku.Trim().ToUpperInvariant();
        if (_flyweights.TryGetValue(clave, out var existente)
            && existente.Nombre == nombre && existente.Categoria == categoria && existente.Descripcion == descripcion)
            return existente;

        return _flyweights.AddOrUpdate(clave,
            static (codigo, datos) => new ProductoFlyweight(codigo, datos.nombre, datos.categoria, datos.descripcion),
            static (codigo, actual, datos) =>
                actual.Nombre == datos.nombre && actual.Categoria == datos.categoria && actual.Descripcion == datos.descripcion
                    ? actual
                    : new ProductoFlyweight(codigo, datos.nombre, datos.categoria, datos.descripcion),
            (nombre, categoria, descripcion));
    }

    public IMetadatosProducto Obtener(Producto producto) =>
        ObtenerFlyweight(producto.Sku, producto.Nombre, producto.Categoria, producto.Descripcion);

    public List<RenglonVisualCatalogo> GenerarRenglonesSimulados(int cantidadFilas = 1000)
    {
        var skus = new[]
        {
            ("P01", "Mouse USB", "Periféricos", "Mouse óptico USB 1600 DPI", 20.00m, 8),
            ("P02", "Teclado", "Periféricos", "Teclado estándar USB en español", 30.00m, 5),
            ("P03", "Memoria USB", "Almacenamiento", "Pendrive USB 3.0 de 64 GB", 10.00m, 20),
            ("P04", "Webcam", "Video", "Cámara web Full HD 1080p con micrófono", 40.00m, 4),
            ("P05", "Alfombrilla", "Accesorios", "Alfombrilla antideslizante para ratón", 5.00m, 30)
        };

        var renglones = new List<RenglonVisualCatalogo>(cantidadFilas);
        for (int i = 0; i < cantidadFilas; i++)
        {
            var baseData = skus[i % skus.Length];
            var flyweight = ObtenerFlyweight(baseData.Item1, baseData.Item2, baseData.Item3, baseData.Item4);
            var renglon = new RenglonVisualCatalogo(flyweight, i + 1, baseData.Item6, baseData.Item5);
            renglones.Add(renglon);
        }

        return renglones;
    }
}
