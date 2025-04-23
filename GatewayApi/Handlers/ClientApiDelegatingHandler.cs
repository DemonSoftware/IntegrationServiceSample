using System.Net;
using System.Net.Http.Headers;
using GatewayApi.Services;

namespace GatewayApi.Handlers
{
    public class ClientApiDelegatingHandler(ClientAuthService authService,
            ILogger<ClientApiDelegatingHandler> logger)
        : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                // Add JWT token to the outgoing request
                var token = await authService.GetAuthTokenAsync();
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                
                // Send the request to the client API
                var response = await base.SendAsync(request, cancellationToken);
                
                // If we get an unauthorized response, the token might have expired
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    // Try again with a fresh token
                    token = await authService.GetAuthTokenAsync(forceRefresh: true);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    
                    // Create a new request since the original one has been used
                    var newRequest = new HttpRequestMessage
                    {
                        Method = request.Method,
                        RequestUri = request.RequestUri,
                        Content = request.Content
                    };
                    
                    // Copy headers except Authorization which we just set
                    foreach (var header in request.Headers)
                    {
                        if (header.Key != "Authorization")
                        {
                            newRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }
                    
                    // Send the new request
                    response = await base.SendAsync(newRequest, cancellationToken);
                }
                
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in ClientApiDelegatingHandler");
                throw;
            }
        }
    }
}