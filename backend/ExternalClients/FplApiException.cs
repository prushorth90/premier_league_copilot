using System.Net;

namespace Backend.ExternalClients;

public sealed class FplApiException : Exception
{
    public FplApiException(string endpoint, HttpStatusCode? statusCode, Exception? innerException = null)
        : base("The Fantasy Premier League service could not complete the request.", innerException)
    {
        Endpoint = endpoint;
        StatusCode = statusCode;
    }

    public string Endpoint { get; }

    public HttpStatusCode? StatusCode { get; }
}