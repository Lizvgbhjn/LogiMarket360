namespace LogiMarket.WinForms;

internal static class ValidacionCampo
{
    internal static void Configurar(ContainerControl vista, ErrorProvider errores)
    {
        errores.ContainerControl = vista;
        errores.BlinkStyle = ErrorBlinkStyle.NeverBlink;
        vista.Disposed += (_, _) => errores.Dispose();
    }

    internal static void Mostrar(ErrorProvider errores, Control campo, string mensaje)
    {
        errores.SetError(campo, mensaje);
        campo.Focus();
    }
}
