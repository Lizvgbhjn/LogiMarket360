namespace LogiMarket.Tests;

using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Ports;
using LogiMarket.Application.Ventas;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;
using LogiMarket.Infrastructure.AbstractFactory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class GestionOperativaTests
{
    [Fact]
    public async Task CatalogoPermiteCrudAdministrativoYProtegeLasReferencias()
    {
        using var fixture = new TestFixture();
        using var scope = fixture.ServiceProvider.CreateScope();
        var productos = scope.ServiceProvider.GetRequiredService<IProductoRepository>();
        var sesion = scope.ServiceProvider.GetRequiredService<IServicioSesion>();
        var kits = scope.ServiceProvider.GetRequiredService<IKitRepository>();
        var catalogo = new GestionCatalogo(productos, sesion, kits);
        var nuevo = new Producto("T01", "Producto de prueba", "Pruebas", "", 2m, 3, 1m);

        await catalogo.GuardarAsync(nuevo, false);
        await catalogo.GuardarAsync(new Producto("T01", "Editado", "Pruebas", "", 4m, 0, 2m), true);
        var editado = Assert.Single(await productos.BuscarAsync("Editado"));
        Assert.Equal(4m, editado.Precio);
        Assert.Equal(3, editado.Stock);
        await Assert.ThrowsAsync<InvalidOperationException>(() => catalogo.EliminarAsync("P01"));
        await catalogo.EliminarAsync("T01");
        Assert.Null(await productos.ObtenerPorSkuAsync("T01"));

        await catalogo.CrearKitAsync("KIT-COMPOSICION", "Kit de prueba");
        await catalogo.AgregarComponenteAsync("KIT-COMPOSICION", "P01", false, 1);
        await catalogo.AgregarComponenteAsync("KIT-COMPOSICION", "P02", false, 2);
        var actualizado = await kits.ObtenerPorCodigoAsync("KIT-COMPOSICION");
        Assert.NotNull(actualizado);
        Assert.Equal(2, actualizado.Componentes.Count);
        await Assert.ThrowsAsync<LogiMarket.Domain.Exceptions.CicloDetectadoException>(
            () => catalogo.AgregarComponenteAsync("KIT-COMPOSICION", "KIT-COMPOSICION", true, 1));
    }

    [Fact]
    public async Task ConsultaNoPuedeCambiarCatalogoClientesNiPedidos()
    {
        using var fixture = new TestFixture(RolUsuario.CONSULTA);
        using var scope = fixture.ServiceProvider.CreateScope();
        var servicios = scope.ServiceProvider;
        var sesion = servicios.GetRequiredService<IServicioSesion>();
        var catalogo = new GestionCatalogo(servicios.GetRequiredService<IProductoRepository>(), sesion);
        var ventas = new GestionVentas(servicios.GetRequiredService<IClienteRepository>(),
            servicios.GetRequiredService<IPedidoRepository>(), sesion);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => catalogo.EliminarAsync("P01"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ventas.AgregarClienteAsync(
            new Cliente("T01", "Cliente de prueba", CategoriaCliente.NORMAL, "")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ventas.GuardarPedidoValidadoAsync(new Pedido(), 1));
    }

    [Fact]
    public async Task CarritoCotizaKitDelCatalogoYGuardaLaMismaComposicion()
    {
        using var fixture = new TestFixture();
        using var scope = fixture.ServiceProvider.CreateScope();
        var servicios = scope.ServiceProvider;
        var productos = servicios.GetRequiredService<IProductoRepository>();
        var kits = servicios.GetRequiredService<IKitRepository>();
        var clientes = servicios.GetRequiredService<IClienteRepository>();
        var pedidos = servicios.GetRequiredService<IPedidoRepository>();
        var ventas = new GestionVentas(clientes, pedidos, servicios.GetRequiredService<IServicioSesion>(), new GuiaEntrega());
        var cliente = (await clientes.ObtenerTodosAsync()).Single(c => c.Categoria == CategoriaCliente.VIP);

        var kit = new Kit("KIT_NUEVO", "Kit de prueba");
        kit.AgregarComponente(new KitComponente(kit.Codigo, "KIT_OFICINA", true));
        kit.AgregarComponente(new KitComponente(kit.Codigo, "P04", false, 2));
        await kits.AgregarAsync(kit);

        var borrador = new Pedido("PED-CARRITO-KIT-NUEVO", 0);
        borrador.AgregarLinea(new PedidoDetalle(kit.Codigo, kit.Nombre, true, 1, 0m));
        var catalogo = new ComposicionCatalogo(await productos.ObtenerTodosAsync(), await kits.ObtenerTodosAsync());
        var cotizacion = ventas.Cotizar(borrador, cliente, TipoEnvio.EXPRES, catalogo);

        Assert.Equal(130m, cotizacion.Subtotal);
        Assert.Equal(13m, cotizacion.Descuento);
        Assert.Equal(12m, cotizacion.TarifaEnvio);
        Assert.Equal(129m, cotizacion.Total);
        var webcam = cotizacion.ComponentesFisicosSnapshot.Single(c => c.Sku == "P04");
        Assert.Equal(2, webcam.CantidadTotalFisica);
        Assert.Equal(0.35m, webcam.PesoKgSnapshot);
        await ventas.GuardarPedidoValidadoAsync(cotizacion, cotizacion.ClienteId);
        var guardado = await pedidos.ObtenerPorCodigoAsync(cotizacion.Codigo);
        Assert.NotNull(guardado);
        Assert.Equal(129m, guardado.Total);
        Assert.Equal(EstadoPedido.VALIDADO, guardado.Estado);
    }
}
