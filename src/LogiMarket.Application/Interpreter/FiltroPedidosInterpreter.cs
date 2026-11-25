namespace LogiMarket.Application.Interpreter;

using System.Globalization;
using LogiMarket.Domain.Entities;
using LogiMarket.Domain.Exceptions;

public class SintaxisFiltroInvalidaException : DomainException
{
    public SintaxisFiltroInvalidaException(string message) : base(message) { }
}

public interface IExpresionFiltro
{
    bool Interpretar(Pedido pedido);
}

// Expresiones no terminales
public class AndExpresion : IExpresionFiltro
{
    private readonly IExpresionFiltro _izquierda;
    private readonly IExpresionFiltro _derecha;

    public AndExpresion(IExpresionFiltro izquierda, IExpresionFiltro derecha)
    {
        _izquierda = izquierda ?? throw new ArgumentNullException(nameof(izquierda));
        _derecha = derecha ?? throw new ArgumentNullException(nameof(derecha));
    }

    public bool Interpretar(Pedido pedido) => _izquierda.Interpretar(pedido) && _derecha.Interpretar(pedido);
}

public class OrExpresion : IExpresionFiltro
{
    private readonly IExpresionFiltro _izquierda;
    private readonly IExpresionFiltro _derecha;

    public OrExpresion(IExpresionFiltro izquierda, IExpresionFiltro derecha)
    {
        _izquierda = izquierda ?? throw new ArgumentNullException(nameof(izquierda));
        _derecha = derecha ?? throw new ArgumentNullException(nameof(derecha));
    }

    public bool Interpretar(Pedido pedido) => _izquierda.Interpretar(pedido) || _derecha.Interpretar(pedido);
}

// Expresiones terminales
public class EstadoExpresion : IExpresionFiltro
{
    private readonly string _estadoEsperado;

    public EstadoExpresion(string estadoEsperado)
    {
        _estadoEsperado = estadoEsperado.Trim().ToUpperInvariant();
    }

    public bool Interpretar(Pedido pedido)
    {
        return pedido.Estado.ToString().Equals(_estadoEsperado, StringComparison.OrdinalIgnoreCase);
    }
}

public class TotalExpresion : IExpresionFiltro
{
    private readonly string _operador;
    private readonly decimal _valor;

    public TotalExpresion(string operador, decimal valor)
    {
        _operador = operador;
        _valor = valor;
    }

    public bool Interpretar(Pedido pedido) => _operador switch
    {
        ">=" => pedido.Total >= _valor,
        "<=" => pedido.Total <= _valor,
        ">" => pedido.Total > _valor,
        "<" => pedido.Total < _valor,
        "=" or "==" => pedido.Total == _valor,
        _ => throw new SintaxisFiltroInvalidaException($"Operador de total no soportado: '{_operador}'.")
    };
}

public class ClienteExpresion : IExpresionFiltro
{
    private readonly string _texto;

    public ClienteExpresion(string texto)
    {
        _texto = texto.Trim();
    }

    public bool Interpretar(Pedido pedido)
    {
        var nombre = pedido.Cliente?.Nombre ?? string.Empty;
        var codigo = pedido.Cliente?.Codigo ?? string.Empty;
        return nombre.Contains(_texto, StringComparison.OrdinalIgnoreCase)
            || codigo.Equals(_texto, StringComparison.OrdinalIgnoreCase);
    }
}

// Parser descendente recursivo con precedencia
public class FiltroPedidosParser
{
    public static IExpresionFiltro Parse(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new SintaxisFiltroInvalidaException("La consulta de filtro no puede estar vacía.");

        var tokens = Tokenizar(query);
        var posicion = 0;

        var expresion = ParseOr(tokens, ref posicion);

        if (posicion < tokens.Count)
        {
            throw new SintaxisFiltroInvalidaException($"Token inesperado al final de la consulta: '{tokens[posicion]}'.");
        }

        return expresion;
    }

    private static IExpresionFiltro ParseOr(List<string> tokens, ref int pos)
    {
        var nodo = ParseAnd(tokens, ref pos);

        while (pos < tokens.Count && tokens[pos].Equals("OR", StringComparison.OrdinalIgnoreCase))
        {
            pos++; // Consumir 'OR'
            var derecha = ParseAnd(tokens, ref pos);
            nodo = new OrExpresion(nodo, derecha);
        }

        return nodo;
    }

    private static IExpresionFiltro ParseAnd(List<string> tokens, ref int pos)
    {
        var nodo = ParseFactor(tokens, ref pos);

        while (pos < tokens.Count && tokens[pos].Equals("AND", StringComparison.OrdinalIgnoreCase))
        {
            pos++; // Consumir 'AND'
            var derecha = ParseFactor(tokens, ref pos);
            nodo = new AndExpresion(nodo, derecha);
        }

        return nodo;
    }

    private static IExpresionFiltro ParseFactor(List<string> tokens, ref int pos)
    {
        if (pos >= tokens.Count)
            throw new SintaxisFiltroInvalidaException("Consulta incompleta: se esperaba una condición o paréntesis.");

        if (tokens[pos] == "(")
        {
            pos++; // Consumir '('
            var expresion = ParseOr(tokens, ref pos);
            if (pos >= tokens.Count || tokens[pos] != ")")
            {
                throw new SintaxisFiltroInvalidaException("Error de sintaxis: falta paréntesis de cierre ')'.");
            }
            pos++; // Consumir ')'
            return expresion;
        }

        // Condición primaria: CAMPO OPERADOR VALOR
        if (pos + 2 >= tokens.Count)
        {
            throw new SintaxisFiltroInvalidaException($"Condición incompleta alrededor de '{tokens[pos]}'. Se espera 'CAMPO OP VALOR'.");
        }

        var campo = tokens[pos].ToUpperInvariant();
        var op = tokens[pos + 1];
        var valor = tokens[pos + 2];
        pos += 3;

        if (campo == "ESTADO")
        {
            if (op != "=" && op != "==")
                throw new SintaxisFiltroInvalidaException($"Operador inválido para ESTADO: '{op}'. Se espera '='.");
            return new EstadoExpresion(valor);
        }
        else if (campo == "TOTAL")
        {
            if (!decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var monto) &&
                !decimal.TryParse(valor, out monto))
            {
                throw new SintaxisFiltroInvalidaException($"Valor numérico inválido para TOTAL: '{valor}'.");
            }
            return new TotalExpresion(op, monto);
        }
        else if (campo == "CLIENTE")
        {
            return new ClienteExpresion(valor);
        }

        throw new SintaxisFiltroInvalidaException($"Campo de filtro desconocido: '{campo}'. Campos soportados: ESTADO, TOTAL, CLIENTE.");
    }

    private static List<string> Tokenizar(string input)
    {
        var tokens = new List<string>();
        var i = 0;

        while (i < input.Length)
        {
            if (char.IsWhiteSpace(input[i]))
            {
                i++;
                continue;
            }

            if (input[i] == '(' || input[i] == ')')
            {
                tokens.Add(input[i].ToString());
                i++;
                continue;
            }

            // Operadores de comparación >=, <=, ==, =, >, <
            if (input[i] == '>' || input[i] == '<' || input[i] == '=')
            {
                if (i + 1 < input.Length && input[i + 1] == '=')
                {
                    tokens.Add(input.Substring(i, 2));
                    i += 2;
                }
                else
                {
                    tokens.Add(input[i].ToString());
                    i++;
                }
                continue;
            }

            // Cadenas entre comillas
            if (input[i] == '\'' || input[i] == '"')
            {
                var quote = input[i];
                i++;
                var start = i;
                while (i < input.Length && input[i] != quote) i++;
                if (i >= input.Length)
                    throw new SintaxisFiltroInvalidaException("Comilla sin cerrar en la consulta de filtro.");
                tokens.Add(input.Substring(start, i - start));
                i++; // Consumir comilla de cierre
                continue;
            }

            // Palabras y números
            var startWord = i;
            while (i < input.Length && !char.IsWhiteSpace(input[i]) && input[i] != '(' && input[i] != ')' &&
                   input[i] != '>' && input[i] != '<' && input[i] != '=' && input[i] != '\'' && input[i] != '"')
            {
                i++;
            }
            tokens.Add(input.Substring(startWord, i - startWord));
        }

        return tokens;
    }
}
