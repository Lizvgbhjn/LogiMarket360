namespace LogiMarket.Application.Ventas;

using LogiMarket.Application.Ports;
using LogiMarket.Application.Builder;
using LogiMarket.Application.Catalogo;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public sealed class GestionVentas(IClienteRepository clientes, IPedidoRepository pedidos, IServicioSesion sesion, IGuiaEntrega? entrega = null)
{
    public bool PuedeModificar => sesion.RolActual is RolUsuario.ADMIN or RolUsuario.VENTAS;

    public Task AgregarClienteAsync(Cliente cliente)
    {
        ExigirPermiso();
        return clientes.AgregarAsync(cliente);
    }

    public async Task ActualizarClienteAsync(int id, string nombre, CategoriaCliente categoria, string contacto)
    {
        ExigirPermiso();
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del cliente es obligatorio.", nameof(nombre));

        var cliente = await clientes.ObtenerPorIdAsync(id)
            ?? throw new InvalidOperationException("El cliente ya no existe.");
        cliente.ActualizarFicha(nombre, categoria, contacto);
        await clientes.ActualizarAsync(cliente);
    }

    public Pedido Cotizar(Pedido borrador, Cliente cliente, TipoEnvio envio, ComposicionCatalogo catalogo)
    {
        if (!cliente.Activo)
            throw new InvalidOperationException("El cliente no está activo.");
        if (borrador.Estado != EstadoPedido.BORRADOR)
            throw new InvalidOperationException("Solo se puede cotizar un borrador.");

        var builder = new PedidoBuilder(entrega)
            .ConCliente(cliente)
            .ConTipoEnvio(envio)
            .ConCodigo(borrador.Codigo)
            .ConIdempotencyKey(borrador.IdempotencyKey);
        foreach (var linea in borrador.Lineas)
            builder.AgregarItem(catalogo.Obtener(linea.ArticuloCodigo, linea.EsKit), linea.Cantidad);

        return builder.Construir();
    }

    public async Task GuardarPedidoValidadoAsync(Pedido pedido, int clienteId)
    {
        ExigirPermiso();
        if (pedido.Id != 0 || pedido.Estado != EstadoPedido.BORRADOR)
            throw new InvalidOperationException("Solo se puede registrar un borrador nuevo.");

        var cliente = await clientes.ObtenerPorIdAsync(clienteId)
            ?? throw new InvalidOperationException("El cliente no es válido.");
        if (!cliente.Activo)
            throw new InvalidOperationException("El cliente no está activo.");

        pedido.Cliente = cliente;
        pedido.ConfigurarBorrador(cliente.Id, pedido.TipoEnvio);
        pedido.Validar();
        await pedidos.AgregarAsync(pedido);
    }

    private void ExigirPermiso()
    {
        if (!PuedeModificar)
            throw new UnauthorizedAccessException("Solo ADMIN y VENTAS pueden modificar clientes y pedidos.");
    }
}
