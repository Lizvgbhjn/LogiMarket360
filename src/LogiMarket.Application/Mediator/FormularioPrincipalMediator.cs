namespace LogiMarket.Application.Mediator;

public interface IVistaRecargable
{
    Task RecargarAsync();
}

public interface IFormularioPrincipalMediator
{
    void RegistrarVista(string nombre, IVistaRecargable vista);
    Task NotificarOperacionAsync(string tipoOperacion, object? payload = null);
}

public class FormularioPrincipalMediator : IFormularioPrincipalMediator
{
    private readonly Dictionary<string, IVistaRecargable> _vistas = new(StringComparer.OrdinalIgnoreCase);

    public void RegistrarVista(string nombre, IVistaRecargable vista)
    {
        _vistas[nombre] = vista;
    }

    public async Task NotificarOperacionAsync(string tipoOperacion, object? payload = null)
    {
        switch (tipoOperacion.ToUpperInvariant())
        {
            case "LOGIN":
            case "LOGOUT":
                foreach (var v in _vistas.Values)
                {
                    await v.RecargarAsync();
                }
                break;

            case "PEDIDO_CONFIRMADO":
            case "DEVOLUCION_PROCESADA":
                if (_vistas.TryGetValue("Dashboard", out var vDash)) await vDash.RecargarAsync();
                if (_vistas.TryGetValue("Catalogo", out var vCat)) await vCat.RecargarAsync();
                if (_vistas.TryGetValue("Pedidos", out var vPed)) await vPed.RecargarAsync();
                if (_vistas.TryGetValue("Inventario", out var vInv)) await vInv.RecargarAsync();
                if (_vistas.TryGetValue("Auditoria", out var vAud)) await vAud.RecargarAsync();
                if (_vistas.TryGetValue("Carrito", out var vCar)) await vCar.RecargarAsync();
                break;

            case "DESPACHO_REGISTRADO":
            case "ENTREGA_REGISTRADA":
                if (_vistas.TryGetValue("Dashboard", out var vD)) await vD.RecargarAsync();
                if (_vistas.TryGetValue("Pedidos", out var vP)) await vP.RecargarAsync();
                if (_vistas.TryGetValue("Despacho", out var vDesp)) await vDesp.RecargarAsync();
                if (_vistas.TryGetValue("Auditoria", out var vA)) await vA.RecargarAsync();
                break;

            case "STOCK_AJUSTADO":
                if (_vistas.TryGetValue("Dashboard", out var vDash2)) await vDash2.RecargarAsync();
                if (_vistas.TryGetValue("Catalogo", out var vCat2)) await vCat2.RecargarAsync();
                if (_vistas.TryGetValue("Inventario", out var vInv2)) await vInv2.RecargarAsync();
                break;

            case "EVENTO_OUTBOX":
                if (_vistas.TryGetValue("Dashboard", out var vD3)) await vD3.RecargarAsync();
                if (_vistas.TryGetValue("Auditoria", out var vA3)) await vA3.RecargarAsync();
                break;

            default:
                foreach (var v in _vistas.Values)
                {
                    await v.RecargarAsync();
                }
                break;
        }
    }
}
