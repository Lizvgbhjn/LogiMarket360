namespace LogiMarket.WinForms.Views;

using LogiMarket.Application.Consultas;

using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Ports;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Visitor;
using LogiMarket.Domain.Exceptions;

public class CatalogoKitsView : UserControl, IVistaRecargable
{
    private readonly ConsultasOperativas _consultas;
    private readonly ErrorProvider _errores = new();

    private sealed record OpcionComponente(string Codigo, string Nombre, bool EsKit)
    {
        public override string ToString() => $"{(EsKit ? "KIT" : "PROD")}: {Codigo} - {Nombre}";
    }
    private readonly GestionCatalogo _gestion;
    private readonly IMetadatosCatalogo _metadatos;
    private readonly IFormularioPrincipalMediator _mediator;

    private TextBox _txtBuscar = null!;
    private DataGridView _gridCatalogo = null!;
    private TreeView _treeKit = null!;
    private ComboBox _cbHijos = null!;
    private NumericUpDown _numCantidad = null!;
    private Label _lblKitSeleccionado = null!;

    public CatalogoKitsView(ConsultasOperativas consultas, GestionCatalogo gestion, IMetadatosCatalogo metadatos, IFormularioPrincipalMediator mediator)
    {
        _gestion = gestion;
        _metadatos = metadatos;
        _mediator = mediator;
        _consultas = consultas;
        ValidacionCampo.Configurar(this, _errores);
        InicializarComponentes();
    }

    private void InicializarComponentes()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(248, 250, 252);
        AutoScroll = true;

        var lblTitulo = new Label { Text = "Catálogo y kits", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Location = new Point(25, 20), AutoSize = true };
        var lblSubtitulo = new Label { Text = "Productos individuales y kits compuestos sin ciclos", Font = new Font("Segoe UI", 10), ForeColor = Color.FromArgb(100, 116, 139), Location = new Point(25, 55), AutoSize = true };

        // Buscador y botones
        var lblBuscar = new Label { Text = "Buscar producto:", Location = new Point(25, 90), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        _txtBuscar = new TextBox { Location = new Point(25, 115), Size = new Size(350, 30), Font = new Font("Segoe UI", 10), PlaceholderText = "SKU, nombre o categoría..." };
        _txtBuscar.TextChanged += async (s, e) => await FiltrarAsync();

        var btnNuevoProducto = new Button { Text = "Nuevo producto", Location = new Point(390, 114), Size = new Size(130, 32), BackColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9) };
        btnNuevoProducto.Click += async (s, e) => await EditarProductoAsync(false);

        var btnNuevoKit = new Button { Text = "Nuevo kit", Location = new Point(530, 114), Size = new Size(110, 32), BackColor = Color.FromArgb(44, 62, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        btnNuevoKit.Click += async (s, e) => await EjecutarCambioAsync(AgregarKitNuevoAsync);

        var btnEditar = new Button { Text = "Editar", Location = new Point(646, 114), Size = new Size(64, 32) };
        btnEditar.Click += async (s, e) => await EditarProductoAsync(true);
        var btnEliminar = new Button { Text = "Eliminar producto", Location = new Point(25, 650), Size = new Size(130, 32) };
        btnEliminar.Click += async (s, e) => await EliminarProductoAsync();

        // DataGridView Catálogo
        _gridCatalogo = new DataGridView
        {
            Location = new Point(25, 160),
            Size = new Size(680, 480),
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            ReadOnly = true,
            AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            Font = new Font("Segoe UI", 9.5f)
        };
        _gridCatalogo.Columns.Add("SKU", "SKU / Código");
        _gridCatalogo.Columns.Add("Producto", "Producto");
        _gridCatalogo.Columns.Add("Precio", "Precio");
        _gridCatalogo.Columns.Add("Stock", "Stock");
        _gridCatalogo.Columns.Add("Peso", "Peso (kg)");
        _gridCatalogo.SelectionChanged += async (s, e) => await OnFilaSeleccionadaAsync();

        // Panel lateral: Estructura del kit
        var panelKit = new Panel
        {
            Location = new Point(720, 115),
            Size = new Size(410, 525),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblKitTitulo = new Label { Text = "Estructura del kit", Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59), Location = new Point(15, 15), AutoSize = true };
        _lblKitSeleccionado = new Label { Text = "KIT_TELETRABAJO", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.FromArgb(51, 65, 85), Location = new Point(15, 45), AutoSize = true };

        _treeKit = new TreeView
        {
            Location = new Point(15, 75),
            Size = new Size(375, 270),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10)
        };

        var lblAgregarHijo = new Label { Text = "Añadir componente al kit:", Location = new Point(15, 355), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        _cbHijos = new ComboBox { Location = new Point(15, 380), Size = new Size(250, 28), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f) };
        _numCantidad = new NumericUpDown { Location = new Point(275, 380), Size = new Size(70, 28), Minimum = 1, Maximum = 99, Value = 1, Font = new Font("Segoe UI", 9.5f) };

        var btnGuardarComposicion = new Button
        {
            Text = "Guardar componente en kit",
            Location = new Point(15, 425),
            Size = new Size(375, 38),
            BackColor = Color.FromArgb(44, 62, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnGuardarComposicion.Click += async (s, e) => await EjecutarCambioAsync(AgregarComponenteAlKitAsync);

        panelKit.Controls.AddRange(new Control[] {
            lblKitTitulo, _lblKitSeleccionado, _treeKit, lblAgregarHijo, _cbHijos, _numCantidad, btnGuardarComposicion
        });

        Controls.AddRange(new Control[] {
            lblTitulo, lblSubtitulo, lblBuscar, _txtBuscar, btnNuevoProducto, btnNuevoKit, btnEditar, btnEliminar, _gridCatalogo, panelKit
        });
    }

    public async Task RecargarAsync()
    {
        var productos = await _consultas.ProductosAsync();
        var kits = await _consultas.KitsAsync();

        _gridCatalogo.Rows.Clear();
        foreach (var p in productos)
        {
            var metadata = _metadatos.Obtener(p);
            var fila = _gridCatalogo.Rows.Add(metadata.Sku, metadata.Nombre, $"USD {p.Precio:F2}", p.Stock, $"{p.PesoKg:F2}");
            _gridCatalogo.Rows[fila].Tag = metadata;
        }

        var catalogo = new ComposicionCatalogo(productos, kits);
        foreach (var k in kits)
        {
            // Precio del kit compuesto
            MostrarKit(k, catalogo);
        }

        // Cargar combos para añadir componentes
        _cbHijos.Items.Clear();
        foreach (var p in productos) _cbHijos.Items.Add(new OpcionComponente(p.Sku, p.Nombre, false));
        foreach (var k in kits) _cbHijos.Items.Add(new OpcionComponente(k.Codigo, k.Nombre, true));
        if (_cbHijos.Items.Count > 0) _cbHijos.SelectedIndex = 0;

        await MostrarEstructuraKitAsync("KIT_TELETRABAJO");
    }

    private void MostrarKit(Kit kit, ComposicionCatalogo catalogo)
    {
        try
        {
            var compuesto = catalogo.Obtener(kit.Codigo, true);
            var peso = new EstimacionPesoVisitor();
            compuesto.Aceptar(peso);
            _gridCatalogo.Rows.Add(kit.Codigo, kit.Nombre, $"USD {compuesto.ObtenerPrecio():F2}", "Compuesto", $"{peso.PesoTotalKg:F2}");
        }
        catch (DomainException)
        {
            _gridCatalogo.Rows.Add(kit.Codigo, kit.Nombre, "No cotizable", "Compuesto", "—");
        }
    }

    private async Task OnFilaSeleccionadaAsync()
    {
        if (_gridCatalogo.CurrentRow == null) return;
        var codigo = _gridCatalogo.CurrentRow.Cells["SKU"].Value?.ToString();
        if (string.IsNullOrEmpty(codigo)) return;

        var kit = await _consultas.KitPorCodigoAsync(codigo);
        if (kit != null)
        {
            await MostrarEstructuraKitAsync(kit.Codigo);
        }
    }

    private async Task MostrarEstructuraKitAsync(string kitCodigo)
    {
        var kit = await _consultas.KitPorCodigoAsync(kitCodigo);
        if (kit == null) return;

        _lblKitSeleccionado.Text = kit.Codigo;
        _treeKit.Nodes.Clear();

        var raiz = new TreeNode($"{kit.Codigo} ({kit.Nombre})");
        await CargarNodosKitAsync(kit, raiz);
        raiz.ExpandAll();
        _treeKit.Nodes.Add(raiz);
    }

    private async Task CargarNodosKitAsync(Kit kit, TreeNode parentNode)
    {
        var todosKits = await _consultas.KitsAsync();
        var todosProds = await _consultas.ProductosAsync();

        foreach (var comp in kit.Componentes)
        {
            if (comp.EsHijoKit)
            {
                var subKit = todosKits.FirstOrDefault(k => k.Codigo == comp.HijoCodigo);
                var nodoHijo = new TreeNode($"⌞ {comp.HijoCodigo} (x{comp.Cantidad})");
                parentNode.Nodes.Add(nodoHijo);
                if (subKit != null)
                {
                    await CargarNodosKitAsync(subKit, nodoHijo);
                }
            }
            else
            {
                var prod = todosProds.FirstOrDefault(p => p.Sku == comp.HijoCodigo);
                parentNode.Nodes.Add(new TreeNode($"  • {comp.HijoCodigo} - {prod?.Nombre ?? "Prod"} (x{comp.Cantidad})"));
            }
        }
    }

    private async Task FiltrarAsync()
    {
        var busqueda = _txtBuscar.Text.Trim();
        var productos = await _consultas.BuscarProductosAsync(busqueda);
        var kits = await _consultas.KitsAsync();

        _gridCatalogo.Rows.Clear();
        foreach (var p in productos)
        {
            var metadata = _metadatos.Obtener(p);
            var fila = _gridCatalogo.Rows.Add(metadata.Sku, metadata.Nombre, $"USD {p.Precio:F2}", p.Stock, $"{p.PesoKg:F2}");
            _gridCatalogo.Rows[fila].Tag = metadata;
        }

        var catalogo = new ComposicionCatalogo(await _consultas.ProductosAsync(), kits);
        foreach (var k in kits.Where(k => string.IsNullOrWhiteSpace(busqueda) || k.Codigo.Contains(busqueda, StringComparison.OrdinalIgnoreCase) || k.Nombre.Contains(busqueda, StringComparison.OrdinalIgnoreCase)))
        {
            MostrarKit(k, catalogo);
        }
    }

    private async Task AgregarComponenteAlKitAsync()
    {
        _errores.Clear();
        var kitCodigo = _lblKitSeleccionado.Text;
        var kit = await _consultas.KitPorCodigoAsync(kitCodigo);
        if (kit == null)
        {
            ValidacionCampo.Mostrar(_errores, _gridCatalogo, "Seleccione un kit primero en la tabla.");
            return;
        }

        if (_cbHijos.SelectedItem is not OpcionComponente seleccionado)
        {
            ValidacionCampo.Mostrar(_errores, _cbHijos, "Seleccione un componente.");
            return;
        }
        var cant = (int)_numCantidad.Value;

        try
        {
            await _gestion.AgregarComponenteAsync(kit.Codigo, seleccionado.Codigo, seleccionado.EsKit, cant);
            await RecargarAsync();
        }
        catch (Exception error)
        {
            ValidacionCampo.Mostrar(_errores, _cbHijos, error.Message);
        }
    }

    private Task EditarProductoAsync(bool editar) => EjecutarCambioAsync(async () =>
    {
        var skuSeleccionado = _gridCatalogo.CurrentRow?.Cells["SKU"].Value?.ToString();
        var actual = editar && skuSeleccionado != null ? await _consultas.ProductoPorSkuAsync(skuSeleccionado) : null;
        if (editar && actual == null)
            throw new InvalidOperationException("Seleccione un producto individual.");

        using var dialogo = new ProductoDialogo(actual);
        if (dialogo.ShowDialog(this) != DialogResult.OK || dialogo.Producto == null) return;
        await _gestion.GuardarAsync(dialogo.Producto, editar);
        await RecargarAsync();
        await _mediator.NotificarOperacionAsync("STOCK_AJUSTADO");
    });

    private Task EliminarProductoAsync() => EjecutarCambioAsync(async () =>
    {
        var sku = _gridCatalogo.CurrentRow?.Cells["SKU"].Value?.ToString();
        if (string.IsNullOrWhiteSpace(sku))
            throw new InvalidOperationException("Seleccione un producto individual.");

        if (MessageBox.Show($"¿Eliminar el producto {sku}?", "Catálogo", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        await _gestion.EliminarAsync(sku);
        await RecargarAsync();
        await _mediator.NotificarOperacionAsync("STOCK_AJUSTADO");
    });

    private async Task EjecutarCambioAsync(Func<Task> operacion)
    {
        try
        {
            _gestion.ExigirAdministrador();
            await operacion();
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "Catálogo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    private async Task AgregarKitNuevoAsync()
    {
        var codigo = PedirEntrada("Ingrese el código del kit (ej: KIT_GAMING):", "KIT_GAMING");
        if (string.IsNullOrWhiteSpace(codigo)) return;

        var nombre = PedirEntrada("Nombre del kit:", "Kit Gaming Pro");
        if (string.IsNullOrWhiteSpace(nombre)) return;

        await _gestion.CrearKitAsync(codigo, nombre);
        await RecargarAsync();
    }

    private static string PedirEntrada(string prompt, string valorPorDefecto)
    {
        using var form = new Form { Width = 400, Height = 170, FormBorderStyle = FormBorderStyle.FixedDialog, Text = "Entrada de datos", StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false };
        var lbl = new Label { Left = 20, Top = 15, Width = 350, Text = prompt };
        var txt = new TextBox { Left = 20, Top = 45, Width = 340, Text = valorPorDefecto };
        var btnOk = new Button { Text = "Aceptar", Left = 180, Top = 85, Width = 85, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancelar", Left = 275, Top = 85, Width = 85, DialogResult = DialogResult.Cancel };
        form.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;
        return form.ShowDialog() == DialogResult.OK ? txt.Text.Trim() : string.Empty;
    }
}
