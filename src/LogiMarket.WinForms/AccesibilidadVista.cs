namespace LogiMarket.WinForms;

internal static class AccesibilidadVista
{
    public static void Configurar(Control vista, string nombre)
    {
        if (vista is ScrollableControl desplazable)
            desplazable.AutoScroll = true;

        ConfigurarGrupo(vista, nombre);
    }

    private static void ConfigurarGrupo(Control grupo, string nombre)
    {
        var controles = grupo.Controls.Cast<Control>().OrderBy(control => control.Top).ThenBy(control => control.Left).ToArray();
        var indice = 0;
        foreach (var control in controles)
        {
            if (control.TabStop)
            {
                control.TabIndex = indice++;
                if (string.IsNullOrWhiteSpace(control.AccessibleName))
                    control.AccessibleName = ObtenerNombre(control, controles, nombre);
            }

            if (control.HasChildren && control is not DataGridView)
                ConfigurarGrupo(control, nombre);
        }
    }

    private static string ObtenerNombre(Control control, IEnumerable<Control> vecinos, string nombre)
    {
        if (control is ButtonBase && !string.IsNullOrWhiteSpace(control.Text))
            return control.Text.Replace("&", "");

        var etiqueta = vecinos.OfType<Label>()
            .Where(label => label.Top <= control.Top && control.Top - label.Top <= 42 && label.Left <= control.Left)
            .OrderByDescending(label => label.Top)
            .ThenByDescending(label => label.Left)
            .FirstOrDefault();
        if (etiqueta != null && control is TextBoxBase or ComboBox or NumericUpDown or DateTimePicker)
            return etiqueta.Text.TrimEnd(':');

        return control switch
        {
            DataGridView => $"Tabla de {nombre}",
            TreeView => $"Estructura de {nombre}",
            ListBox => $"Lista de {nombre}",
            _ => string.IsNullOrWhiteSpace(control.Name) ? nombre : control.Name
        };
    }

    internal static void Reacomodar(ScrollableControl vista, Control principal, Control lateral, Control? debajoDe = null)
    {
        var separacion = lateral.Left - principal.Right;
        var diferenciaVertical = lateral.Top - principal.Top;
        bool? apilado = null;
        void Ajustar()
        {
            var escala = vista.DeviceDpi / 96f;
            var anchoLateral = lateral.Width;
            var margen = (int)Math.Ceiling(20 * escala);
            var lateralX = principal.Right - vista.AutoScrollPosition.X + separacion;
            var estrecho = vista.ClientSize.Width < lateralX + anchoLateral + margen;
            if (apilado == estrecho) return;

            apilado = estrecho;
            vista.AutoScrollPosition = Point.Empty;
            lateral.Location = estrecho
                ? new Point(principal.Left, Math.Max(principal.Bottom, debajoDe?.Bottom ?? 0) + margen)
                : new Point(principal.Right + separacion, principal.Top + diferenciaVertical);
        }

        vista.SizeChanged += (_, _) => Ajustar();
        vista.ParentChanged += (_, _) => { apilado = null; Ajustar(); };
        Ajustar();
    }
}
