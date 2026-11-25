namespace LogiMarket.Infrastructure.Session;

using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;

public class ServicioSesion : IServicioSesion
{
    private Usuario? _usuarioActual;

    public Usuario? UsuarioActual => _usuarioActual;
    public bool EstaAutenticado => _usuarioActual != null && _usuarioActual.Activo;

    public void IniciarSesion(Usuario usuario)
    {
        _usuarioActual = usuario ?? throw new ArgumentNullException(nameof(usuario));
    }

    public void CerrarSesion()
    {
        _usuarioActual = null;
    }
}
