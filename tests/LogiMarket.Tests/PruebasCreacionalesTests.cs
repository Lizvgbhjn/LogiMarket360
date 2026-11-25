namespace LogiMarket.Tests;

using Microsoft.Extensions.DependencyInjection;
using LogiMarket.Application.Builder;
using LogiMarket.Application.Prototype;
using LogiMarket.Application.Singleton;
using LogiMarket.Domain.Composite;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;
using LogiMarket.Domain.Exceptions;
using LogiMarket.Infrastructure.AbstractFactory;
using LogiMarket.Infrastructure.FactoryMethod;
using Xunit;

public class PruebasCreacionalesTests
{
    [Fact]
    public async Task P11_FactoryMethod_AlternarEmailSms_RetornaNotificadorConcretoYRegistraAviso()
    {
        using var fixture = new TestFixture();
        using var scope = fixture.ServiceProvider.CreateScope();

        // Creador EMAIL
        NotificadorCreator creadorEmail = new EmailNotificadorCreator();
        var notificador1 = creadorEmail.CrearNotificador();
        Assert.IsType<EmailNotificador>(notificador1);
        Assert.Equal("EMAIL", notificador1.Canal);

        var resEmail = await creadorEmail.EnviarAvisoAsync("cliente@test.com", "Pedido confirmado");
        Assert.True(resEmail.Exitoso);
        Assert.Equal("EMAIL", resEmail.Canal);

        // Creador SMS
        NotificadorCreator creadorSms = new SmsNotificadorCreator();
        var notificador2 = creadorSms.CrearNotificador();
        Assert.IsType<SmsNotificador>(notificador2);
        Assert.Equal("SMS", notificador2.Canal);

        var resSms = await creadorSms.EnviarAvisoAsync("+5939999999", "Pedido en camino");
        Assert.True(resSms.Exitoso);
        Assert.Equal("SMS", resSms.Canal);
    }

    [Fact]
    public void P12_AbstractFactory_EstandarYExpres_TarifasYPlazosCoherentesPorFamilia()
    {
        // Familia ESTÁNDAR
        IServicioEntregaFactory fabricaEstandar = ServicioEntregaFactoryProvider.ObtenerFabrica(TipoEnvio.ESTANDAR);
        var calcTarifaStd = fabricaEstandar.CrearCalculadorTarifa();
        var calcPlazoStd = fabricaEstandar.CrearCalculadorPlazo();
        var genGuiaStd = fabricaEstandar.CrearGeneradorGuia();

        Assert.Equal(5.00m, calcTarifaStd.ObtenerTarifa());
        Assert.Equal(3, calcPlazoStd.ObtenerPlazoDias());
        var fechaDespacho = new DateTime(2026, 11, 24);
        Assert.Equal(new DateTime(2026, 11, 27), calcPlazoStd.CalcularFechaEntregaEstimada(fechaDespacho));
        Assert.Contains("GUIA-STD", genGuiaStd.GenerarGuia("PED-001"));

        // Familia EXPRÉS
        IServicioEntregaFactory fabricaExpres = ServicioEntregaFactoryProvider.ObtenerFabrica(TipoEnvio.EXPRES);
        var calcTarifaExp = fabricaExpres.CrearCalculadorTarifa();
        var calcPlazoExp = fabricaExpres.CrearCalculadorPlazo();
        var genGuiaExp = fabricaExpres.CrearGeneradorGuia();

        Assert.Equal(12.00m, calcTarifaExp.ObtenerTarifa());
        Assert.Equal(1, calcPlazoExp.ObtenerPlazoDias());
        Assert.Equal(new DateTime(2026, 11, 25), calcPlazoExp.CalcularFechaEntregaEstimada(fechaDespacho));
        Assert.Contains("GUIA-EXP", genGuiaExp.GenerarGuia("PED-001"));
    }

    [Fact]
    public void P13_Builder_PedidoVacioRechazado_PedidoCompletoCreaAgregadoValido()
    {
        var cliente = new Cliente("C001", "Ana", CategoriaCliente.VIP, "ana@test.com");
        var producto = new ProductoSimple("P01", "Mouse", "Periféricos", "", 20.00m, 0.20m, 8);

        // Caso Incompleto: sin cliente -> Debe arrojar DomainException
        Assert.Throws<DomainException>(() =>
        {
            new PedidoBuilder()
                .AgregarItem(producto, 1)
                .Construir();
        });

        // Caso Incompleto: sin líneas de items -> Debe arrojar DomainException
        Assert.Throws<DomainException>(() =>
        {
            new PedidoBuilder()
                .ConCliente(cliente)
                .Construir();
        });

        // Caso Completo: crea agregado válido con todas las invariantes y snapshots calculados
        var pedido = new PedidoBuilder()
            .ConCodigo("PED-COMPLETO-01")
            .ConCliente(cliente)
            .ConTipoEnvio(TipoEnvio.ESTANDAR)
            .AgregarItem(producto, 2)
            .Construir();

        Assert.NotNull(pedido);
        Assert.Equal(EstadoPedido.BORRADOR, pedido.Estado);
        Assert.Single(pedido.Lineas);
        Assert.Equal(40.00m, pedido.Subtotal); // 2 * 20
        Assert.Equal(4.00m, pedido.Descuento); // 10% VIP
        Assert.Equal(5.00m, pedido.TarifaEnvio);
        Assert.Equal(41.00m, pedido.Total);
        Assert.Single(pedido.ComponentesFisicosSnapshot);
        Assert.Equal(2, pedido.ComponentesFisicosSnapshot[0].CantidadTotalFisica);
    }

    [Fact]
    public void P14_Prototype_ClonarKitTeletrabajo_CopiaProfundaEIndependiente()
    {
        var ana = new Cliente("C001", "Ana", CategoriaCliente.VIP, "ana@test.com");
        var kitTeletrabajo = new KitCompuesto("KIT_TELETRABAJO", "Kit Teletrabajo Completo");
        var kitOficina = new KitCompuesto("KIT_OFICINA", "Kit Oficina");
        kitOficina.AgregarComponente(new ProductoSimple("P01", "Mouse", "", "", 20m, 0.2m, 8), 1);
        kitOficina.AgregarComponente(new ProductoSimple("P02", "Teclado", "", "", 30m, 0.7m, 5), 1);
        kitTeletrabajo.AgregarComponente(kitOficina, 1);
        kitTeletrabajo.AgregarComponente(new ProductoSimple("P03", "Memoria", "", "", 10m, 0.1m, 20), 1);

        var original = new PedidoBuilder()
            .ConCodigo("PED-ORIGINAL")
            .ConCliente(ana)
            .AgregarItem(kitTeletrabajo, 1)
            .Construir();

        // Clonar mediante Prototype
        var clon = PedidoBorradorPrototype.Clonar(original);

        Assert.NotNull(clon);
        Assert.NotEqual(original.Codigo, clon.Codigo);
        Assert.NotSame(original.Lineas, clon.Lineas);
        Assert.NotSame(original.ComponentesFisicosSnapshot, clon.ComponentesFisicosSnapshot);

        // Modificar una línea en el clon
        clon.CambiarCantidad(clon.Lineas[0], 99);

        // La plantilla/pedido original permanece intacta
        Assert.Equal(1, original.Lineas[0].Cantidad);
        Assert.Equal(99, clon.Lineas[0].Cantidad);
    }

    [Fact]
    public void P15_Singleton_ConsultarMetricasDesdeDosConsumidores_MismaInstanciaYThreadSafe()
    {
        using var fixture = new TestFixture();

        // Obtener la instancia de Singleton desde dos scopes / consumidores distintos
        IMetricasSesion consumidor1 = fixture.ServiceProvider.GetRequiredService<IMetricasSesion>();
        IMetricasSesion consumidor2;

        using (var scope = fixture.ServiceProvider.CreateScope())
        {
            consumidor2 = scope.ServiceProvider.GetRequiredService<IMetricasSesion>();
        }

        // Comprobación de que ambas referencias apuntan exactamente al mismo objeto en memoria
        Assert.Same(consumidor1, consumidor2);
        Assert.True(ReferenceEquals(consumidor1, consumidor2));

        consumidor1.ReiniciarMetricas();
        Assert.Equal(0, consumidor2.TotalPedidosConfirmados);

        // Operaciones concurrentes thread-safe
        Parallel.For(0, 100, i =>
        {
            consumidor1.RegistrarVentaConfirmada(10m);
        });

        Assert.Equal(100, consumidor2.TotalPedidosConfirmados);
        Assert.Equal(1000m, consumidor2.TotalVentasMonto);
    }
}
