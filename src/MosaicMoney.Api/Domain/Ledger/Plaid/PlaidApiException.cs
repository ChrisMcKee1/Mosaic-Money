using System.Net;

namespace MosaicMoney.Api.Domain.Ledger.Plaid;

public sealed class PlaidApiException : InvalidOperationException
{
    public PlaidApiException(
        string endpoint,
        HttpStatusCode statusCode,
        string? requestId,
        string? errorCode,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Endpoint = endpoint;
        StatusCode = statusCode;
        RequestId = requestId;
        ErrorCode = errorCode;
    }

    public string Endpoint { get; }

    public HttpStatusCode StatusCode { get; }

    public string? RequestId { get; }

    public string? ErrorCode { get; }
}
