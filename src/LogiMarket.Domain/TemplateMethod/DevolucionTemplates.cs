namespace LogiMarket.Domain.TemplateMethod;

using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;
using LogiMarket.Domain.Exceptions;

public record SolicitudDevolucion(
    Pedido Pedido,
    MotivoDevolucion Motivo,
    DateTime FechaSolicitud,
    bool ProductoSinUso,
    string Observaciones);

public record ResultadoDevolucion(
    bool Aprobada,
    string MotivoRechazo,
    decimal MontoReembolso,
    MotivoDevolucion Motivo,
    int TotalItemsReintegrados);

public abstract class PoliticaDevolucionTemplate
{
    // Secuencia invariante (Template Method)
    public ResultadoDevolucion ProcesarDevolucion(SolicitudDevolucion solicitud)
    {
        // 1. Verificar elegibilidad básica
        var (elegible, mensajeElegibilidad) = VerificarElegibilidad(solicitud);
        if (!elegible)
        {
            return new ResultadoDevolucion(false, mensajeElegibilidad, 0m, solicitud.Motivo, 0);
        }

        // 2. Inspeccionar condición
        var (condicionValida, mensajeCondicion) = InspeccionarCondicion(solicitud);
        if (!condicionValida)
        {
            return new ResultadoDevolucion(false, mensajeCondicion, 0m, solicitud.Motivo, 0);
        }

        // 3. Calcular reembolso (importe de los productos tras descuento, SIN la tarifa de envío)
        var montoReembolso = CalcularReembolso(solicitud.Pedido);

        // 4. Determinar unidades de stock a reintegrar
        var unidadesReintegrar = solicitud.Pedido.TotalUnidadesFisicas;

        // 5. Devolución aceptada
        return new ResultadoDevolucion(true, "Aprobada", montoReembolso, solicitud.Motivo, unidadesReintegrar);
    }

    public async Task<ResultadoDevolucion> ProcesarDevolucionAsync(
        SolicitudDevolucion solicitud,
        Func<ResultadoDevolucion, CancellationToken, Task> registrarDevolucionYStock,
        Func<ResultadoDevolucion, CancellationToken, Task> registrarEvento,
        CancellationToken ct = default)
    {
        var resultado = ProcesarDevolucion(solicitud);
        if (!resultado.Aprobada)
            return resultado;

        await registrarDevolucionYStock(resultado, ct);
        await registrarEvento(resultado, ct);
        return resultado;
    }

    protected virtual (bool Elegible, string Mensaje) VerificarElegibilidad(SolicitudDevolucion solicitud)
    {
        var pedido = solicitud.Pedido;
        if (pedido.Estado != EstadoPedido.ENTREGADO)
        {
            return (false, "Solo se pueden devolver pedidos en estado ENTREGADO.");
        }

        if (pedido.Devolucion != null && pedido.Devolucion.Estado == "APROBADA")
        {
            return (false, "El pedido ya cuenta con una devolución aprobada previamente.");
        }

        if (!pedido.FechaEntrega.HasValue)
        {
            return (false, "El pedido no registra fecha de entrega.");
        }

        return (true, string.Empty);
    }

    protected abstract (bool Valida, string Mensaje) InspeccionarCondicion(SolicitudDevolucion solicitud);

    protected virtual decimal CalcularReembolso(Pedido pedido)
    {
        // El reembolso corresponde al importe de los productos después del descuento, sin devolver la tarifa de envío
        // subtotal − descuento
        var monto = pedido.ImporteReembolsable;
        return Math.Max(0, Math.Round(monto, 2, MidpointRounding.AwayFromZero));
    }
}

public class DevolucionPorDefecto : PoliticaDevolucionTemplate
{
    protected override (bool Valida, string Mensaje) InspeccionarCondicion(SolicitudDevolucion solicitud)
    {
        var diasDesdeEntrega = (solicitud.FechaSolicitud.Date - solicitud.Pedido.FechaEntrega!.Value.Date).TotalDays;
        if (diasDesdeEntrega > 30)
        {
            return (false, $"Plazo vencido para DEFECTO: han transcurrido {diasDesdeEntrega} días (máximo 30 días).");
        }

        return (true, string.Empty);
    }
}

public class DevolucionPorArrepentimiento : PoliticaDevolucionTemplate
{
    protected override (bool Valida, string Mensaje) InspeccionarCondicion(SolicitudDevolucion solicitud)
    {
        var diasDesdeEntrega = (solicitud.FechaSolicitud.Date - solicitud.Pedido.FechaEntrega!.Value.Date).TotalDays;
        if (diasDesdeEntrega > 7)
        {
            return (false, $"Plazo vencido para ARREPENTIMIENTO: han transcurrido {diasDesdeEntrega} días (máximo 7 días).");
        }

        if (!solicitud.ProductoSinUso)
        {
            return (false, "Devolución por ARREPENTIMIENTO rechazada: el producto debe estar sin uso.");
        }

        return (true, string.Empty);
    }
}
