namespace LogiMarket.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public class DatabaseSeeder
{
    private readonly LogiMarketDbContext _context;

    public DatabaseSeeder(LogiMarketDbContext context)
    {
        _context = context;
    }

    public async Task InicializarAsync(CancellationToken ct = default)
    {
        await _context.Database.MigrateAsync(ct);
        await SeedUsuariosAsync(ct);
        await SeedClientesAsync(ct);
        await SeedProductosAsync(ct);
        await SeedKitsAsync(ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task RestablecerBasePruebasAsync(CancellationToken ct = default)
    {
        await _context.Database.EnsureDeletedAsync(ct);
        await InicializarAsync(ct);
    }

    private async Task SeedUsuariosAsync(CancellationToken ct)
    {
        if (!await _context.Usuarios.AnyAsync(ct))
        {
            var usuarios = new List<Usuario>
            {
                new Usuario("admin.demo", BCrypt.Net.BCrypt.HashPassword("admin123"), RolUsuario.ADMIN),
                new Usuario("ventas.demo", BCrypt.Net.BCrypt.HashPassword("ventas123"), RolUsuario.VENTAS),
                new Usuario("bodega.demo", BCrypt.Net.BCrypt.HashPassword("bodega123"), RolUsuario.BODEGA),
                new Usuario("consulta.demo", BCrypt.Net.BCrypt.HashPassword("consulta123"), RolUsuario.CONSULTA)
            };

            await _context.Usuarios.AddRangeAsync(usuarios, ct);
        }
    }

    private async Task SeedClientesAsync(CancellationToken ct)
    {
        if (!await _context.Clientes.AnyAsync(ct))
        {
            var clientes = new List<Cliente>
            {
                new Cliente("C001", "Ana López", CategoriaCliente.VIP, "ana@ejemplo.test"),
                new Cliente("C002", "Luis Ruiz", CategoriaCliente.NORMAL, "099 000 0000"),
                new Cliente("C003", "Carmen Mayor", CategoriaCliente.MAYORISTA, "carmen@ejemplo.test"),
                new Cliente("C004", "Sofía Díaz", CategoriaCliente.NORMAL, "sofia@ejemplo.test")
            };

            await _context.Clientes.AddRangeAsync(clientes, ct);
        }
    }

    private async Task SeedProductosAsync(CancellationToken ct)
    {
        if (!await _context.Productos.AnyAsync(ct))
        {
            var productos = new List<Producto>
            {
                new Producto("P01", "Mouse USB", "Periféricos", "Mouse óptico USB 1600 DPI", 20.00m, 8, 0.20m),
                new Producto("P02", "Teclado", "Periféricos", "Teclado estándar USB en español", 30.00m, 5, 0.70m),
                new Producto("P03", "Memoria USB", "Almacenamiento", "Pendrive USB 3.0 de 64 GB", 10.00m, 20, 0.10m),
                new Producto("P04", "Webcam", "Video", "Cámara web Full HD 1080p con micrófono", 40.00m, 4, 0.35m),
                new Producto("P05", "Alfombrilla", "Accesorios", "Alfombrilla antideslizante para ratón", 5.00m, 30, 0.10m)
            };

            await _context.Productos.AddRangeAsync(productos, ct);
        }
    }

    private async Task SeedKitsAsync(CancellationToken ct)
    {
        if (!await _context.Kits.AnyAsync(ct))
        {
            // KIT_OFICINA = P01 + P02 = USD 50.00
            var kitOficina = new Kit("KIT_OFICINA", "Kit Oficina Básica", "Incluye Mouse USB y Teclado");
            kitOficina.AgregarComponente(new KitComponente("KIT_OFICINA", "P01", false, 1));
            kitOficina.AgregarComponente(new KitComponente("KIT_OFICINA", "P02", false, 1));

            // KIT_TELETRABAJO = KIT_OFICINA + P03 = USD 60.00
            var kitTeletrabajo = new Kit("KIT_TELETRABAJO", "Kit Teletrabajo Completo", "Incluye Kit Oficina más Memoria USB");
            kitTeletrabajo.AgregarComponente(new KitComponente("KIT_TELETRABAJO", "KIT_OFICINA", true, 1));
            kitTeletrabajo.AgregarComponente(new KitComponente("KIT_TELETRABAJO", "P03", false, 1));

            await _context.Kits.AddRangeAsync(new[] { kitOficina, kitTeletrabajo }, ct);
        }
    }
}
