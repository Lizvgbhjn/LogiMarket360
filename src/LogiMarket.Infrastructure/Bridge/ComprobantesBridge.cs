namespace LogiMarket.Infrastructure.Bridge;

using LogiMarket.Domain.Entities;
using System.Globalization;
using System.Text;

public record ResultadoSalida(
    string TipoSalida,
    string Titulo,
    string Contenido,
    string? RutaArchivo = null);

// Implementor
public interface ISalidaDocumento
{
    string TipoSalida { get; }
    ResultadoSalida Imprimir(string titulo, string contenido, string metadata);
}

// Concrete Implementor 1: Vista Previa
public class VistaPreviaSalida : ISalidaDocumento
{
    public string TipoSalida => "VISTA_PREVIA";

    public ResultadoSalida Imprimir(string titulo, string contenido, string metadata)
    {
        return new ResultadoSalida(TipoSalida, titulo, contenido, null);
    }
}

// Concrete Implementor 2: Archivo TXT Real
public class ArchivoTxtSalida : ISalidaDocumento
{
    private readonly string _directorioSalida;

    public string TipoSalida => "ARCHIVO_TXT";

    public ArchivoTxtSalida(string? directorioSalida = null)
    {
        _directorioSalida = directorioSalida ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "comprobantes_txt");
        if (!Directory.Exists(_directorioSalida))
        {
            Directory.CreateDirectory(_directorioSalida);
        }
    }

    public ResultadoSalida Imprimir(string titulo, string contenido, string metadata)
    {
        var sanitized = string.Join("_", titulo.Split(Path.GetInvalidFileNameChars()));
        var nombreArchivo = $"{sanitized}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Random.Shared.Next(100, 999)}.txt";
        var rutaCompleta = Path.Combine(_directorioSalida, nombreArchivo);

        File.WriteAllText(rutaCompleta, contenido, Encoding.UTF8);

        return new ResultadoSalida(TipoSalida, titulo, contenido, rutaCompleta);
    }
}

// Abstraction
public abstract class Comprobante
{
    protected readonly ISalidaDocumento _salida;

    protected Comprobante(ISalidaDocumento salida)
    {
        _salida = salida ?? throw new ArgumentNullException(nameof(salida));
    }

    public abstract ResultadoSalida Emitir();
}

// Refined Abstraction 1: Comprobante de Venta
public class ComprobanteVenta : Comprobante
{
    private readonly Pedido _pedido;

    public ComprobanteVenta(ISalidaDocumento salida, Pedido pedido) : base(salida)
    {
        _pedido = pedido ?? throw new ArgumentNullException(nameof(pedido));
    }

    public override ResultadoSalida Emitir()
    {
        var sb = new StringBuilder();
        sb.AppendLine("==================================================");
        sb.AppendLine("           LOGIMARKET 360 - COMPROBANTE DE VENTA");
        sb.AppendLine("==================================================");
        sb.AppendLine($"Código Pedido:    {_pedido.Codigo}");
        sb.AppendLine($"Fecha Emisión:    {_pedido.FechaCreacion:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Cliente:          {_pedido.Cliente?.Nombre ?? "N/A"} ({_pedido.Cliente?.Categoria})");
        sb.AppendLine($"Contacto:         {_pedido.Cliente?.Contacto ?? "N/A"}");
        sb.AppendLine($"Estado:           {_pedido.Estado}");
        sb.AppendLine($"Ref. Pago:        {_pedido.ReferenciaPago ?? "N/A"}");
        sb.AppendLine($"Tipo Entrega:     {_pedido.TipoEnvio}");
        sb.AppendLine("--------------------------------------------------");
        sb.AppendLine("CANT | DESCRIPCIÓN               | P.UNIT  | TOTAL");
        sb.AppendLine("--------------------------------------------------");

        foreach (var linea in _pedido.Lineas)
        {
            var desc = linea.NombreArticulo.Length > 25 ? linea.NombreArticulo[..25] : linea.NombreArticulo.PadRight(25);
            sb.AppendLine($"{linea.Cantidad,4} | {desc} | {linea.PrecioUnitario,7:F2} | {linea.Subtotal,7:F2}");
        }

        sb.AppendLine("--------------------------------------------------");
        sb.AppendLine($"Subtotal:         USD {_pedido.Subtotal.ToString("F2", CultureInfo.InvariantCulture),10}");
        sb.AppendLine($"Descuento:      - USD {_pedido.Descuento.ToString("F2", CultureInfo.InvariantCulture),10}");
        sb.AppendLine($"Tarifa Envío:   + USD {_pedido.TarifaEnvio.ToString("F2", CultureInfo.InvariantCulture),10}");
        sb.AppendLine("--------------------------------------------------");
        sb.AppendLine($"TOTAL PAGADO:     USD {_pedido.Total.ToString("F2", CultureInfo.InvariantCulture),10}");
        sb.AppendLine("==================================================");

        return _salida.Imprimir($"Venta_{_pedido.Codigo}", sb.ToString(), _pedido.Codigo);
    }
}

// Refined Abstraction 2: Comprobante de Devolución
public class ComprobanteDevolucion : Comprobante
{
    private readonly Pedido _pedido;
    private readonly Devolucion _devolucion;

    public ComprobanteDevolucion(ISalidaDocumento salida, Pedido pedido, Devolucion devolucion) : base(salida)
    {
        _pedido = pedido ?? throw new ArgumentNullException(nameof(pedido));
        _devolucion = devolucion ?? throw new ArgumentNullException(nameof(devolucion));
    }

    public override ResultadoSalida Emitir()
    {
        var sb = new StringBuilder();
        sb.AppendLine("==================================================");
        sb.AppendLine("       LOGIMARKET 360 - COMPROBANTE DE DEVOLUCIÓN");
        sb.AppendLine("==================================================");
        sb.AppendLine($"Pedido Original:  {_pedido.Codigo}");
        sb.AppendLine($"Fecha Devolución: {_devolucion.FechaSolicitud:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Cliente:          {_pedido.Cliente?.Nombre ?? "N/A"}");
        sb.AppendLine($"Motivo:           {_devolucion.Motivo}");
        sb.AppendLine($"Estado Devolución:{_devolucion.Estado}");
        sb.AppendLine($"Observaciones:    {_devolucion.Observaciones}");
        sb.AppendLine("--------------------------------------------------");
        sb.AppendLine("Regla: Reembolso de productos sin tarifa de envío.");
        sb.AppendLine($"Subtotal Pedido:  USD {_pedido.Subtotal.ToString("F2", CultureInfo.InvariantCulture),10}");
        sb.AppendLine($"Descuento Pedido:-USD {_pedido.Descuento.ToString("F2", CultureInfo.InvariantCulture),10}");
        sb.AppendLine($"Tarifa Envío:     USD {_pedido.TarifaEnvio.ToString("F2", CultureInfo.InvariantCulture),10} (No reembolsable)");
        sb.AppendLine("--------------------------------------------------");
        sb.AppendLine($"TOTAL REEMBOLSO:  USD {_devolucion.MontoReembolsado.ToString("F2", CultureInfo.InvariantCulture),10}");
        sb.AppendLine("==================================================");

        return _salida.Imprimir($"Devolucion_{_pedido.Codigo}", sb.ToString(), _pedido.Codigo);
    }
}
