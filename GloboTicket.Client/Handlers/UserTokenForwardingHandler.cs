using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using System.Net.Http.Headers;
using System.Net.Http;                  // For HttpClient, HttpRequestMessage, DelegatingHandler
using Microsoft.AspNetCore.Http;        // For IHttpContextAccessor

public class UserTokenForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserTokenForwardingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = await _httpContextAccessor.HttpContext!.GetTokenAsync("access_token");

        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}