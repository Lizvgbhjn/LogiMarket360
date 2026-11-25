namespace LogiMarket.Infrastructure.Bridge;

using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;

public sealed class EmisorComprobantes : IEmisorComprobantes
{
    public ComprobanteEmitido Emitir(Pedido pedido, Devolucion? devolucion, bool exportarTxt)
    {
        ISalidaDocumento salida = exportarTxt ? new ArchivoTxtSalida() : new VistaPreviaSalida();
        Comprobante comprobante = devolucion == null
            ? new ComprobanteVenta(salida, pedido)
            : new ComprobanteDevolucion(salida, pedido, devolucion);
        var resultado = comprobante.Emitir();
        return new ComprobanteEmitido(resultado.Contenido, resultado.RutaArchivo);
    }
}
