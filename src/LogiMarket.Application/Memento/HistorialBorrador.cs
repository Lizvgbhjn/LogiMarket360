namespace LogiMarket.Application.Memento;

using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;
using LogiMarket.Domain.Exceptions;

public class PedidoBorradorMemento
{
    public int ClienteId { get; }
    public TipoEnvio TipoEnvio { get; }
    public Pedido.ImportesBorrador Importes { get; }
    public List<PedidoDetalle> LineasSnapshot { get; }
    public List<PedidoDetalleComponente> ComponentesSnapshot { get; }
    public DateTime FechaMemento { get; }

    public PedidoBorradorMemento(Pedido pedido)
    {
        ClienteId = pedido.ClienteId;
        TipoEnvio = pedido.TipoEnvio;
        Importes = pedido.CapturarImportes();
        FechaMemento = DateTime.UtcNow;

        LineasSnapshot = pedido.Lineas.Select(l => new PedidoDetalle(
            l.ArticuloCodigo, l.NombreArticulo, l.EsKit, l.Cantidad, l.PrecioUnitario
        )).ToList();

        ComponentesSnapshot = pedido.ComponentesFisicosSnapshot.Select(c => new PedidoDetalleComponente(
            c.Sku, c.NombreProducto, c.CantidadTotalFisica, c.PrecioUnitarioSnapshot, c.PesoKgSnapshot
        )).ToList();
    }
}

public class HistorialBorradorCaretaker
{
    private const int MaxCapacidad = 5;
    private readonly Stack<PedidoBorradorMemento> _deshacerStack = new();
    private readonly Stack<PedidoBorradorMemento> _rehacerStack = new();
    private bool _bloqueado = false;

    public int ConteoDeshacer => _deshacerStack.Count;
    public int ConteoRehacer => _rehacerStack.Count;

    public void GuardarEstado(Pedido pedido)
    {
        if (_bloqueado || pedido.Estado != EstadoPedido.BORRADOR)
            return;

        if (_deshacerStack.Count >= MaxCapacidad)
        {
            // Eliminar el más antiguo manteniendo el límite de 5
            var lista = _deshacerStack.ToList();
            lista.RemoveAt(lista.Count - 1);
            _deshacerStack.Clear();
            for (int i = lista.Count - 1; i >= 0; i--)
            {
                _deshacerStack.Push(lista[i]);
            }
        }

        _deshacerStack.Push(new PedidoBorradorMemento(pedido));
        _rehacerStack.Clear(); // Nuevo cambio invalida la pila de rehacer
    }

    public bool PuedeDeshacer => !_bloqueado && _deshacerStack.Count > 0;
    public bool PuedeRehacer => !_bloqueado && _rehacerStack.Count > 0;

    public void Deshacer(Pedido pedido)
    {
        if (_bloqueado || pedido.Estado != EstadoPedido.BORRADOR)
            throw new InvalidOperationException("No se permite revertir cambios en un pedido que ya no es BORRADOR o confirmado.");

        if (!PuedeDeshacer)
            throw new InvalidOperationException("No hay acciones previas para deshacer.");

        // Guardar estado actual en la pila de rehacer
        _rehacerStack.Push(new PedidoBorradorMemento(pedido));

        var memento = _deshacerStack.Pop();
        RestaurarMemento(pedido, memento);
    }

    public void Rehacer(Pedido pedido)
    {
        if (_bloqueado || pedido.Estado != EstadoPedido.BORRADOR)
            throw new InvalidOperationException("No se permite rehacer en un pedido confirmado.");

        if (!PuedeRehacer)
            throw new InvalidOperationException("No hay acciones para rehacer.");

        _deshacerStack.Push(new PedidoBorradorMemento(pedido));

        var memento = _rehacerStack.Pop();
        RestaurarMemento(pedido, memento);
    }

    public void BloquearYLimpiar()
    {
        _bloqueado = true;
        _deshacerStack.Clear();
        _rehacerStack.Clear();
    }

    private static void RestaurarMemento(Pedido pedido, PedidoBorradorMemento memento)
    {
        pedido.ConfigurarBorrador(memento.ClienteId, memento.TipoEnvio);

        pedido.LimpiarContenido();
        foreach (var l in memento.LineasSnapshot)
        {
            pedido.AgregarLinea(new PedidoDetalle(l.ArticuloCodigo, l.NombreArticulo, l.EsKit, l.Cantidad, l.PrecioUnitario));
        }

        foreach (var c in memento.ComponentesSnapshot)
        {
            pedido.AgregarComponenteFisico(new PedidoDetalleComponente(
                c.Sku, c.NombreProducto, c.CantidadTotalFisica, c.PrecioUnitarioSnapshot, c.PesoKgSnapshot));
        }
        pedido.RestaurarImportes(memento.Importes);
    }
}
