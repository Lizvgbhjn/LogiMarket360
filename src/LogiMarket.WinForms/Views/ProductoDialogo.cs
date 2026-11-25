namespace LogiMarket.WinForms.Views;

using LogiMarket.Domain.Entities;

internal sealed class ProductoDialogo : Form
{
    public Producto? Producto { get; private set; }

    public ProductoDialogo(Producto? actual)
    {
        Text = actual == null ? "Nuevo producto" : "Editar producto";
        ClientSize = new Size(450, 305);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        var errores = new ErrorProvider { ContainerControl = this, BlinkStyle = ErrorBlinkStyle.NeverBlink };
        Disposed += (_, _) => errores.Dispose();

        var sku = new TextBox { Text = actual?.Sku ?? "", ReadOnly = actual != null };
        var nombre = new TextBox { Text = actual?.Nombre ?? "" };
        var categoria = new TextBox { Text = actual?.Categoria ?? "" };
        var descripcion = new TextBox { Text = actual?.Descripcion ?? "" };
        var precio = new NumericUpDown { DecimalPlaces = 2, Minimum = 0.01m, Maximum = 1_000_000_000m, Value = actual?.Precio ?? 0.01m };
        var peso = new NumericUpDown { DecimalPlaces = 4, Increment = 0.01m, Maximum = 1_000_000_000m, Value = actual?.PesoKg ?? 0m };
        var stock = new NumericUpDown { Maximum = int.MaxValue, Value = actual?.Stock ?? 0, Enabled = actual == null };
        var campos = new (string Etiqueta, Control Entrada)[]
        {
            ("SKU", sku), ("Nombre", nombre), ("Categoría", categoria), ("Descripción", descripcion),
            ("Precio USD", precio), ("Peso kg", peso), ("Stock inicial", stock)
        };

        for (var i = 0; i < campos.Length; i++)
        {
            var (etiqueta, entrada) = campos[i];
            var y = 15 + i * 34;
            Controls.Add(new Label { Text = etiqueta, Location = new Point(15, y + 4), AutoSize = true });
            entrada.Location = new Point(110, y);
            entrada.Width = 300;
            entrada.AccessibleName = etiqueta;
            entrada.TabIndex = i;
            Controls.Add(entrada);
        }

        var guardar = new Button { Text = "Guardar", Location = new Point(242, 262), Width = 90 };
        guardar.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(sku.Text) || string.IsNullOrWhiteSpace(nombre.Text))
            {
                errores.SetError(sku, string.IsNullOrWhiteSpace(sku.Text) ? "Ingrese el SKU." : "");
                errores.SetError(nombre, string.IsNullOrWhiteSpace(nombre.Text) ? "Ingrese el nombre." : "");
                (string.IsNullOrWhiteSpace(sku.Text) ? sku : nombre).Focus();
                return;
            }

            Producto = new Producto(sku.Text, nombre.Text, categoria.Text, descripcion.Text,
                precio.Value, (int)stock.Value, peso.Value);
            DialogResult = DialogResult.OK;
        };
        var cancelar = new Button { Text = "Cancelar", Location = new Point(340, 262), Width = 90, DialogResult = DialogResult.Cancel };
        Controls.AddRange([guardar, cancelar]);
        AcceptButton = guardar;
        CancelButton = cancelar;
    }
}
