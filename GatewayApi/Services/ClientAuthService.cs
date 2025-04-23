using System.Net.Http.Headers;
using System.Text.Json;

namespace GatewayApi.Services
{
    public class TokenResponse
    {
        public string Token { get; set; }
        public int ExpiresIn { get; set; }
        public string TokenType { get; set; }
    }

    public class ClientAuthService(HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ClientAuthService> logger)
    {
        private string _authToken;
        private DateTime _tokenExpiry = DateTime.MinValue;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public async Task<string> GetAuthTokenAsync(bool forceRefresh = false)
        {
            // Check if we already have a valid token and don't need to refresh
            if (!forceRefresh && !string.IsNullOrEmpty(_authToken) && DateTime.UtcNow < _tokenExpiry)
            {
                return _authToken;
            }
            
            // Use semaphore to prevent multiple simultaneous token requests
            await _semaphore.WaitAsync();
            
            try
            {
                // Double-check after acquiring the semaphore
                if (!forceRefresh && !string.IsNullOrEmpty(_authToken) && DateTime.UtcNow < _tokenExpiry)
                {
                    return _authToken;
                }
                
                // Prepare authentication request according to client's API requirements
                var authRequest = new
                {
                    ClientId = configuration["ClientApi:ClientId"],
                    ClientSecret = configuration["ClientApi:ClientSecret"],
                    GrantType = "client_credentials"
                };
                
                var content = JsonContent.Create(authRequest);
                
                // Request token
                var response = await httpClient.PostAsync(
                    configuration["ClientApi:AuthEndpoint"], 
                    content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    logger.LogError("Failed to get token. Status: {Status}, Error: {Error}", 
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"Failed to get auth token: {response.StatusCode}");
                }
                
                var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
                
                // Store the token with a safety margin for expiration
                _authToken = tokenResponse.Token;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60);
                
                return _authToken;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting authentication token");
                throw;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        public async Task<HttpRequestMessage> CreateAuthenticatedRequestAsync(
            HttpMethod method, string endpoint)
        {
            var token = await GetAuthTokenAsync();
            var request = new HttpRequestMessage(method, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return request;
        }
    }
}