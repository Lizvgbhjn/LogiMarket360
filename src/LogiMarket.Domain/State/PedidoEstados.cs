namespace LogiMarket.Domain.State;

using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;
using LogiMarket.Domain.Exceptions;

public interface IPedidoEstado
{
    EstadoPedido Nombre { get; }
    bool PuedeEditar { get; }
    void Validar(Pedido pedido);
    void Pagar(Pedido pedido, string refPago);
    void Despachar(Pedido pedido, string guia);
    void Entregar(Pedido pedido);
    void Cancelar(Pedido pedido, string motivo);
}

public class BorradorEstado : IPedidoEstado
{
    public EstadoPedido Nombre => EstadoPedido.BORRADOR;
    public bool PuedeEditar => true;

    public void Validar(Pedido pedido)
    {
        if (!pedido.Lineas.Any())
            throw new DomainException("No se puede validar un pedido sin líneas de detalle.");
        pedido.CambiarEstado(new ValidadoEstado());
    }

    public void Pagar(Pedido pedido, string refPago)
    {
        throw new TransicionEstadoInvalidaException("No se puede pagar un pedido en estado BORRADOR. Debe ser VALIDADO primero.");
    }

    public void Despachar(Pedido pedido, string guia)
    {
        throw new TransicionEstadoInvalidaException("No se puede despachar un pedido en estado BORRADOR.");
    }

    public void Entregar(Pedido pedido)
    {
        throw new TransicionEstadoInvalidaException("No se puede entregar un pedido en estado BORRADOR.");
    }

    public void Cancelar(Pedido pedido, string motivo)
    {
        pedido.RegistrarCancelacion(motivo);
        pedido.CambiarEstado(new CanceladoEstado());
    }
}

public class ValidadoEstado : IPedidoEstado
{
    public EstadoPedido Nombre => EstadoPedido.VALIDADO;
    public bool PuedeEditar => false;

    public void Validar(Pedido pedido)
    {
        throw new TransicionEstadoInvalidaException("El pedido ya se encuentra VALIDADO.");
    }

    public void Pagar(Pedido pedido, string refPago)
    {
        if (string.IsNullOrWhiteSpace(refPago))
            throw new DomainException("Se requiere una referencia de pago válida para registrar el pago.");
        pedido.RegistrarReferenciaPago(refPago);
        pedido.CambiarEstado(new PagadoEstado());
    }

    public void Despachar(Pedido pedido, string guia)
    {
        throw new TransicionEstadoInvalidaException("No se puede despachar un pedido sin haber sido PAGADO previamente.");
    }

    public void Entregar(Pedido pedido)
    {
        throw new TransicionEstadoInvalidaException("No se puede entregar un pedido no pagado.");
    }

    public void Cancelar(Pedido pedido, string motivo)
    {
        pedido.RegistrarCancelacion(motivo);
        pedido.CambiarEstado(new CanceladoEstado());
    }
}

public class PagadoEstado : IPedidoEstado
{
    public EstadoPedido Nombre => EstadoPedido.PAGADO;
    public bool PuedeEditar => false;

    public void Validar(Pedido pedido)
    {
        throw new TransicionEstadoInvalidaException("El pedido ya está PAGADO; no se puede re-validar.");
    }

    public void Pagar(Pedido pedido, string refPago)
    {
        throw new TransicionEstadoInvalidaException("El pedido ya fue PAGADO. No se permite duplicar el cobro.");
    }

    public void Despachar(Pedido pedido, string guia)
    {
        if (string.IsNullOrWhiteSpace(guia))
            throw new DomainException("Se requiere un número o código de guía para despachar.");
        pedido.RegistrarDespacho(guia);
        pedido.CambiarEstado(new DespachadoEstado());
    }

    public void Entregar(Pedido pedido)
    {
        throw new TransicionEstadoInvalidaException("No se puede entregar directamente un pedido PAGADO sin antes haber sido DESPACHADO.");
    }

    public void Cancelar(Pedido pedido, string motivo)
    {
        throw new TransicionEstadoInvalidaException("No se permite cancelar un pedido PAGADO.");
    }
}

public class DespachadoEstado : IPedidoEstado
{
    public EstadoPedido Nombre => EstadoPedido.DESPACHADO;
    public bool PuedeEditar => false;

    public void Validar(Pedido pedido) => throw new TransicionEstadoInvalidaException("El pedido ya se encuentra DESPACHADO.");
    public void Pagar(Pedido pedido, string refPago) => throw new TransicionEstadoInvalidaException("El pedido ya fue pagado y despachado.");
    public void Despachar(Pedido pedido, string guia) => throw new TransicionEstadoInvalidaException("El pedido ya está en estado DESPACHADO.");

    public void Entregar(Pedido pedido)
    {
        pedido.RegistrarEntrega();
        pedido.CambiarEstado(new EntregadoEstado());
    }

    public void Cancelar(Pedido pedido, string motivo)
    {
        throw new TransicionEstadoInvalidaException("No se puede cancelar un pedido que ya está DESPACHADO.");
    }
}

public class EntregadoEstado : IPedidoEstado
{
    public EstadoPedido Nombre => EstadoPedido.ENTREGADO;
    public bool PuedeEditar => false;

    public void Validar(Pedido pedido) => throw new TransicionEstadoInvalidaException("El pedido ya fue ENTREGADO.");
    public void Pagar(Pedido pedido, string refPago) => throw new TransicionEstadoInvalidaException("El pedido ya fue entregado y pagado.");
    public void Despachar(Pedido pedido, string guia) => throw new TransicionEstadoInvalidaException("El pedido ya fue entregado.");
    public void Entregar(Pedido pedido) => throw new TransicionEstadoInvalidaException("El pedido ya se encuentra ENTREGADO.");
    public void Cancelar(Pedido pedido, string motivo) => throw new TransicionEstadoInvalidaException("No se puede cancelar un pedido ENTREGADO. Aplica política de Devolución.");
}

public class CanceladoEstado : IPedidoEstado
{
    public EstadoPedido Nombre => EstadoPedido.CANCELADO;
    public bool PuedeEditar => false;

    public void Validar(Pedido pedido) => throw new TransicionEstadoInvalidaException("El pedido está CANCELADO.");
    public void Pagar(Pedido pedido, string refPago) => throw new TransicionEstadoInvalidaException("No se puede pagar un pedido CANCELADO.");
    public void Despachar(Pedido pedido, string guia) => throw new TransicionEstadoInvalidaException("No se puede despachar un pedido CANCELADO.");
    public void Entregar(Pedido pedido) => throw new TransicionEstadoInvalidaException("No se puede entregar un pedido CANCELADO.");
    public void Cancelar(Pedido pedido, string motivo) => throw new TransicionEstadoInvalidaException("El pedido ya está CANCELADO.");
}

public static class PedidoEstadoFactory
{
    public static IPedidoEstado Crear(EstadoPedido estado) => estado switch
    {
        EstadoPedido.BORRADOR => new BorradorEstado(),
        EstadoPedido.VALIDADO => new ValidadoEstado(),
        EstadoPedido.PAGADO => new PagadoEstado(),
        EstadoPedido.DESPACHADO => new DespachadoEstado(),
        EstadoPedido.ENTREGADO => new EntregadoEstado(),
        EstadoPedido.CANCELADO => new CanceladoEstado(),
        _ => throw new ArgumentOutOfRangeException(nameof(estado), $"Estado desconocido: {estado}")
    };
}
