namespace Services.ReadModelBuilder.Exceptions;

public class OrderEnrichmentException : Exception
{
    public string Operation { get; }

    public OrderEnrichmentException(string operation, string message, Exception innerException)
        : base(message, innerException)
    {
        Operation = operation;
    }
}
