namespace LogiMarket.Infrastructure.Session;

using LogiMarket.Application.Ports;

public sealed class VerificadorContrasena : IVerificadorContrasena
{
    public bool Verificar(string contrasena, string hash) => BCrypt.Net.BCrypt.Verify(contrasena, hash);
}
