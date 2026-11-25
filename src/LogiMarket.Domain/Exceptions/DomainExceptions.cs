namespace LogiMarket.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception innerException) : base(message, innerException) { }
}

public class TransicionEstadoInvalidaException : DomainException
{
    public TransicionEstadoInvalidaException(string message) : base(message) { }
}

public class CicloDetectadoException : DomainException
{
    public CicloDetectadoException(string message) : base(message) { }
}

public class StockInsuficienteException : DomainException
{
    public StockInsuficienteException(string message) : base(message) { }
}
