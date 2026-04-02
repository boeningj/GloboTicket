using System.Net.Http;                  // For HttpClient, HttpRequestMessage, DelegatingHandler
using System.Threading;                 // For CancellationToken
using System.Threading.Tasks;           // For Task
using Microsoft.Extensions.Logging;     // For ILogger

public class AuthLoggingHandler : DelegatingHandler
{
    private readonly ILogger<AuthLoggingHandler> _logger;

    public AuthLoggingHandler(ILogger<AuthLoggingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization != null)
        {
            _logger.LogInformation("Authorization header: {AuthHeader}", request.Headers.Authorization);
        }
        else
        {
            _logger.LogWarning("No Authorization header on outgoing request!");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}