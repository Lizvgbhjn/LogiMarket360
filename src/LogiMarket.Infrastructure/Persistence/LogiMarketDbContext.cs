namespace LogiMarket.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public class LogiMarketDbContext : DbContext
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Kit> Kits => Set<Kit>();
    public DbSet<KitComponente> KitComponentes => Set<KitComponente>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<PedidoDetalle> PedidoDetalles => Set<PedidoDetalle>();
    public DbSet<PedidoDetalleComponente> PedidoDetalleComponentes => Set<PedidoDetalleComponente>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<Despacho> Despachos => Set<Despacho>();
    public DbSet<Devolucion> Devoluciones => Set<Devolucion>();
    public DbSet<MovimientoInventario> MovimientosInventario => Set<MovimientoInventario>();
    public DbSet<EventoOutbox> EventosOutbox => Set<EventoOutbox>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();

    public LogiMarketDbContext() { }

    public LogiMarketDbContext(DbContextOptions<LogiMarketDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var rutaDb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logimarket360.db");
            optionsBuilder.UseSqlite($"Data Source={rutaDb}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Usuario
        modelBuilder.Entity<Usuario>(b =>
        {
            b.HasKey(u => u.Id);
            b.HasIndex(u => u.Username).IsUnique();
            b.Property(u => u.Username).HasMaxLength(50).IsRequired();
            b.Property(u => u.PasswordHash).IsRequired();
            b.Property(u => u.Rol).HasConversion<string>();
        });

        // Cliente
        modelBuilder.Entity<Cliente>(b =>
        {
            b.HasKey(c => c.Id);
            b.HasIndex(c => c.Codigo).IsUnique();
            b.Property(c => c.Codigo).HasMaxLength(20).IsRequired();
            b.Property(c => c.Nombre).HasMaxLength(150).IsRequired();
            b.Property(c => c.Categoria).HasConversion<string>();
        });

        // Producto
        modelBuilder.Entity<Producto>(b =>
        {
            b.HasKey(p => p.Sku);
            b.Property(p => p.Sku).HasMaxLength(30).IsRequired();
            b.Property(p => p.Nombre).HasMaxLength(150).IsRequired();
            b.Property(p => p.Precio).HasPrecision(18, 2);
            b.Property(p => p.PesoKg).HasPrecision(18, 3);
            b.HasIndex(p => p.Nombre);
            b.HasIndex(p => p.Categoria);
            b.Property(p => p.Version).IsConcurrencyToken(); // Concurrencia optimista
        });

        // Kit
        modelBuilder.Entity<Kit>(b =>
        {
            b.HasKey(k => k.Codigo);
            b.Property(k => k.Codigo).HasMaxLength(30).IsRequired();
            b.Property(k => k.Nombre).HasMaxLength(150).IsRequired();
            b.HasMany(k => k.Componentes)
             .WithOne()
             .HasForeignKey(c => c.KitCodigo)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // KitComponente
        modelBuilder.Entity<KitComponente>(b =>
        {
            b.HasKey(c => c.Id);
            b.HasIndex(c => new { c.KitCodigo, c.HijoCodigo }).IsUnique();
            b.Property(c => c.HijoCodigo).HasMaxLength(30).IsRequired();
        });

        // Pedido
        modelBuilder.Entity<Pedido>(b =>
        {
            b.HasKey(p => p.Id);
            b.HasIndex(p => p.Codigo).IsUnique();
            b.HasIndex(p => p.IdempotencyKey).IsUnique();
            b.Property(p => p.Codigo).HasMaxLength(40).IsRequired();
            b.Property(p => p.IdempotencyKey).HasMaxLength(64).IsRequired();
            b.Property(p => p.Estado).HasConversion<string>();
            b.Property(p => p.TipoEnvio).HasConversion<string>();
            b.Property(p => p.Subtotal).HasPrecision(18, 2);
            b.Property(p => p.Descuento).HasPrecision(18, 2);
            b.Property(p => p.TarifaEnvio).HasPrecision(18, 2);
            b.Property(p => p.Total).HasPrecision(18, 2);

            b.HasOne(p => p.Cliente)
             .WithMany()
             .HasForeignKey(p => p.ClienteId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasMany(p => p.Lineas)
             .WithOne()
             .HasForeignKey(l => l.PedidoId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(p => p.ComponentesFisicosSnapshot)
             .WithOne()
             .HasForeignKey(c => c.PedidoId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(p => p.Pago)
             .WithOne()
             .HasForeignKey<Pago>(p => p.PedidoId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(p => p.Despacho)
             .WithOne()
             .HasForeignKey<Despacho>(d => d.PedidoId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(p => p.Devolucion)
             .WithOne()
             .HasForeignKey<Devolucion>(d => d.PedidoId)
             .OnDelete(DeleteBehavior.Cascade);

            // Ignorar el objeto de estado de runtime
            b.Ignore(p => p.EstadoInterno);
        });

        // PedidoDetalle
        modelBuilder.Entity<PedidoDetalle>(b =>
        {
            b.HasKey(d => d.Id);
            b.Property(d => d.PrecioUnitario).HasPrecision(18, 2);
            b.Property(d => d.Subtotal).HasPrecision(18, 2);
        });

        // PedidoDetalleComponente
        modelBuilder.Entity<PedidoDetalleComponente>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.PrecioUnitarioSnapshot).HasPrecision(18, 2);
            b.Property(c => c.PesoKgSnapshot).HasPrecision(18, 3);
        });

        // Pago
        modelBuilder.Entity<Pago>(b =>
        {
            b.HasKey(p => p.Id);
            b.HasIndex(p => p.ClaveIdempotencia).IsUnique();
            b.HasIndex(p => p.ReferenciaTransaccion).IsUnique();
            b.Property(p => p.ClaveIdempotencia).HasMaxLength(64).IsRequired();
            b.Property(p => p.Monto).HasPrecision(18, 2);
            b.Property(p => p.Proveedor).HasMaxLength(50);
        });

        // Despacho
        modelBuilder.Entity<Despacho>(b =>
        {
            b.HasKey(d => d.Id);
            b.Property(d => d.GuiaSeguimiento).HasMaxLength(50).IsRequired();
            b.Property(d => d.TipoEntrega).HasConversion<string>();
        });

        // Devolucion - Restricción de devolución única por pedido
        modelBuilder.Entity<Devolucion>(b =>
        {
            b.HasKey(d => d.Id);
            b.HasIndex(d => d.PedidoId).IsUnique(); // Devolución única por pedido
            b.Property(d => d.Motivo).HasConversion<string>();
            b.Property(d => d.MontoReembolsado).HasPrecision(18, 2);
        });

        // MovimientoInventario
        modelBuilder.Entity<MovimientoInventario>(b =>
        {
            b.HasKey(m => m.Id);
            b.Property(m => m.Sku).HasMaxLength(30).IsRequired();
            b.Property(m => m.TipoMovimiento).HasConversion<string>();
        });

        // EventoOutbox
        modelBuilder.Entity<EventoOutbox>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.TipoEvento).HasMaxLength(100).IsRequired();
            b.Property(e => e.Estado).HasConversion<string>();
        });

        // Auditoria
        modelBuilder.Entity<Auditoria>(b =>
        {
            b.HasKey(a => a.Id);
            b.Property(a => a.Usuario).HasMaxLength(50);
            b.Property(a => a.Accion).HasMaxLength(100);
        });
    }
}
