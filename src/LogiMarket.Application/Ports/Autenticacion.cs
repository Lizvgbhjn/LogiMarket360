namespace LogiMarket.Application.Ports;

public interface IVerificadorContrasena
{
    bool Verificar(string contrasena, string hash);
}

public sealed class Autenticacion(
    IUsuarioRepository usuarios, IVerificadorContrasena verificador, IServicioSesion sesion)
{
    public async Task<bool> IngresarAsync(string usuario, string contrasena, CancellationToken ct = default)
    {
        var cuenta = await usuarios.ObtenerPorUsernameAsync(usuario.Trim(), ct);
        if (cuenta is not { Activo: true } || !verificador.Verificar(contrasena, cuenta.PasswordHash))
            return false;

        sesion.IniciarSesion(cuenta);
        return true;
    }
}
