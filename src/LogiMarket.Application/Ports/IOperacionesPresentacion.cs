namespace LogiMarket.Application.Ports;

using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public interface IGuiaEntrega
{
    string Generar(TipoEnvio tipo, string codigoPedido);
    decimal Tarifa(TipoEnvio tipo);
    int PlazoDias(TipoEnvio tipo);
}

public record ComprobanteEmitido(string Contenido, string? RutaArchivo);

public interface IEmisorComprobantes
{
    ComprobanteEmitido Emitir(Pedido pedido, Devolucion? devolucion, bool exportarTxt);
}

public record ResultadoDemoCatalogo(int Filas, int InstanciasCompartidas, int InstanciasSinCompartir, bool MismaReferencia);

public interface IDemostracionOperativa
{
    bool Configurar(bool fallarCobro, bool fallarOutbox, bool usarSms);
    Task<int> ProcesarEventosAsync(bool recuperarFallidos);
    ResultadoDemoCatalogo MedirCatalogo();
}

public interface IMetadatosProducto
{
    string Sku { get; }
    string Nombre { get; }
}

public interface IMetadatosCatalogo
{
    IMetadatosProducto Obtener(Producto producto);
}