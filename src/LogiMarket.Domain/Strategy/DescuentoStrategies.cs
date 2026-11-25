namespace LogiMarket.Domain.Strategy;

using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Enums;

public record ResultadoEstrategiaDescuento(string NombreEstrategia, decimal Tasa, decimal MontoDescuento);

public interface IDescuentoStrategy
{
    string Nombre { get; }
    int Prioridad { get; } // Menor número = mayor prioridad
    bool Aplica(Pedido pedido);
    ResultadoEstrategiaDescuento CalcularDescuento(Pedido pedido);
}

public class MayoristaDescuentoStrategy : IDescuentoStrategy
{
    public string Nombre => "MAYORISTA (15%)";
    public int Prioridad => 1; // Prioridad más alta

    public bool Aplica(Pedido pedido)
    {
        // Aplica si el total de unidades físicas finales es al menos 10, sin importar la categoría del cliente
        return pedido.TotalUnidadesFisicas >= 10;
    }

    public ResultadoEstrategiaDescuento CalcularDescuento(Pedido pedido)
    {
        const decimal tasa = 0.15m;
        var subtotal = pedido.Subtotal;
        var descuento = Math.Round(subtotal * tasa, 2, MidpointRounding.AwayFromZero);
        return new ResultadoEstrategiaDescuento(Nombre, tasa, descuento);
    }
}

public class VipDescuentoStrategy : IDescuentoStrategy
{
    public string Nombre => "VIP (10%)";
    public int Prioridad => 2;

    public bool Aplica(Pedido pedido)
    {
        // Aplica 10% si el cliente pertenece a esa categoría y no calificó para mayorista
        return pedido.Cliente?.Categoria == CategoriaCliente.VIP;
    }

    public ResultadoEstrategiaDescuento CalcularDescuento(Pedido pedido)
    {
        const decimal tasa = 0.10m;
        var subtotal = pedido.Subtotal;
        var descuento = Math.Round(subtotal * tasa, 2, MidpointRounding.AwayFromZero);
        return new ResultadoEstrategiaDescuento(Nombre, tasa, descuento);
    }
}

public class SinDescuentoStrategy : IDescuentoStrategy
{
    public string Nombre => "NORMAL (0%)";
    public int Prioridad => 999; // Fallback

    public bool Aplica(Pedido pedido) => true;

    public ResultadoEstrategiaDescuento CalcularDescuento(Pedido pedido)
    {
        return new ResultadoEstrategiaDescuento(Nombre, 0m, 0m);
    }
}

public class CalculadoraDescuentoContext
{
    private readonly List<IDescuentoStrategy> _estrategias;

    public CalculadoraDescuentoContext(IEnumerable<IDescuentoStrategy>? estrategias = null)
    {
        _estrategias = (estrategias ?? new IDescuentoStrategy[]
        {
            new MayoristaDescuentoStrategy(),
            new VipDescuentoStrategy(),
            new SinDescuentoStrategy()
        }).OrderBy(s => s.Prioridad).ToList();
    }

    public void RegistrarEstrategia(IDescuentoStrategy nuevaEstrategia)
    {
        _estrategias.Add(nuevaEstrategia);
        _estrategias.Sort((a, b) => a.Prioridad.CompareTo(b.Prioridad));
    }

    public ResultadoEstrategiaDescuento DeterminarDescuento(Pedido pedido)
    {
        foreach (var estrategia in _estrategias)
        {
            if (estrategia.Aplica(pedido))
            {
                return estrategia.CalcularDescuento(pedido);
            }
        }

        return new ResultadoEstrategiaDescuento("Sin Descuento", 0m, 0m);
    }
}
