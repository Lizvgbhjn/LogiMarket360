namespace LogiMarket.Domain.Entities;

using LogiMarket.Domain.Enums;
using LogiMarket.Domain.Exceptions;
using LogiMarket.Domain.State;

public class Usuario
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public Usuario() { }

    public Usuario(string username, string passwordHash, RolUsuario rol)
    {
        Username = username.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        Rol = rol;
        Activo = true;
        FechaCreacion = DateTime.UtcNow;
    }
}

public class Cliente
{
    public int Id { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public CategoriaCliente Categoria { get; private set; } = CategoriaCliente.NORMAL;
    public string Contacto { get; private set; } = string.Empty;
    public bool Activo { get; private set; } = true;

    public Cliente() { }

    public Cliente(string codigo, string nombre, CategoriaCliente categoria, string contacto)
    {
        Codigo = codigo.Trim().ToUpperInvariant();
        Nombre = nombre.Trim();
        Categoria = categoria;
        Contacto = contacto.Trim();
        Activo = true;
    }

    public void ActualizarFicha(string nombre, CategoriaCliente categoria, string contacto)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del cliente es obligatorio.", nameof(nombre));

        Nombre = nombre.Trim();
        Categoria = categoria;
        Contacto = contacto.Trim();
    }
}

public class Producto
{
    public string Sku { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public string Categoria { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public decimal Precio { get; private set; }
    public int Stock { get; private set; }
    public decimal PesoKg { get; private set; }
    public uint Version { get; private set; } = 1; // Para control de concurrencia optimista

    public Producto() { }

    public Producto(string sku, string nombre, string categoria, string descripcion, decimal precio, int stock, decimal pesoKg)
    {
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("SKU obligatorio.", nameof(sku));
        if (precio <= 0) throw new ArgumentException("El precio debe ser positivo.", nameof(precio));
        if (stock < 0) throw new ArgumentException("El stock no puede ser negativo.", nameof(stock));
        if (pesoKg < 0) throw new ArgumentException("El peso no puede ser negativo.", nameof(pesoKg));

        Sku = sku.Trim().ToUpperInvariant();
        Nombre = nombre.Trim();
        Categoria = categoria.Trim();
        Descripcion = descripcion.Trim();
        Precio = precio;
        Stock = stock;
        PesoKg = pesoKg;
    }

    public void ActualizarFicha(string nombre, string categoria, string descripcion, decimal precio, decimal pesoKg)
    {
        if (string.IsNullOrWhiteSpace(nombre)) throw new ArgumentException("Nombre obligatorio.", nameof(nombre));
        if (precio <= 0) throw new ArgumentException("El precio debe ser positivo.", nameof(precio));
        if (pesoKg < 0) throw new ArgumentException("El peso no puede ser negativo.", nameof(pesoKg));

        Nombre = nombre.Trim();
        Categoria = categoria.Trim();
        Descripcion = descripcion.Trim();
        Precio = precio;
        PesoKg = pesoKg;
        Version++;
    }

    public void DescontarStock(int cantidad)
    {
        if (cantidad <= 0) throw new DomainException("La cantidad a descontar debe ser mayor a cero.");
        if (Stock < cantidad)
            throw new StockInsuficienteException($"Stock insuficiente para SKU '{Sku}'. Disponible: {Stock}, requerido: {cantidad}.");
        Stock -= cantidad;
        Version++;
    }

    public void ReintegrarStock(int cantidad)
    {
        if (cantidad <= 0) throw new DomainException("La cantidad a reintegrar debe ser mayor a cero.");
        Stock += cantidad;
        Version++;
    }
}

public class Kit
{
    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public bool Activo { get; private set; } = true;
    private readonly List<KitComponente> _componentes = new();
    public IReadOnlyList<KitComponente> Componentes => _componentes.AsReadOnly();

    public void AgregarComponente(KitComponente componente)
    {
        if (componente.KitCodigo != Codigo || _componentes.Any(actual => actual.HijoCodigo == componente.HijoCodigo))
            throw new InvalidOperationException("El componente no pertenece al kit o ya está incluido.");

        _componentes.Add(componente);
    }

    public Kit() { }

    public Kit(string codigo, string nombre, string descripcion = "")
    {
        Codigo = codigo.Trim().ToUpperInvariant();
        Nombre = nombre.Trim();
        Descripcion = descripcion.Trim();
        Activo = true;
    }
}

public class KitComponente
{
    public int Id { get; set; }
    public string KitCodigo { get; set; } = string.Empty;
    public string HijoCodigo { get; set; } = string.Empty;
    public bool EsHijoKit { get; set; }
    public int Cantidad { get; set; } = 1;

    public KitComponente() { }

    public KitComponente(string kitCodigo, string hijoCodigo, bool esHijoKit, int cantidad = 1)
    {
        KitCodigo = kitCodigo.Trim().ToUpperInvariant();
        HijoCodigo = hijoCodigo.Trim().ToUpperInvariant();
        EsHijoKit = esHijoKit;
        Cantidad = cantidad > 0 ? cantidad : 1;
    }
}
