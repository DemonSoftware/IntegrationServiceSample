using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;

namespace GatewayApi.Services
{
    public interface ICertificateService
    {
        X509Certificate2 GetClientCertificate();
        bool ValidateServerCertificate(
            HttpRequestMessage requestMessage, 
            X509Certificate2 certificate, 
            X509Chain chain, 
            SslPolicyErrors sslErrors);
    }

    public class CertificateService(IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<CertificateService> logger)
        : ICertificateService
    {
        public X509Certificate2 GetClientCertificate()
        {
            var certSettings = configuration.GetSection("ClientCertificate");
            string certPath = certSettings["FilePath"];
            string certPassword = certSettings["Password"];
            
            // In production, consider getting these from secure storage
            if (environment.IsProduction())
            {
                certPassword = Environment.GetEnvironmentVariable("CLIENT_CERT_PASSWORD");
            }
            
            string fullPath = Path.Combine(environment.ContentRootPath, certPath);
            
            if (!File.Exists(fullPath))
            {
                logger.LogError("Client certificate file not found at {Path}", fullPath);
                throw new FileNotFoundException("Client certificate file not found", fullPath);
            }
            
            try
            {
                return new X509Certificate2(
                    fullPath, 
                    certPassword,
                    X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading client certificate");
                throw;
            }
        }

        public bool ValidateServerCertificate(
            HttpRequestMessage requestMessage, 
            X509Certificate2 certificate, 
            X509Chain chain, 
            SslPolicyErrors sslErrors)
        {
            // Bypass certificate validation in development if needed
            if (environment.IsDevelopment() && 
                configuration.GetValue<bool>("ClientApi:IgnoreCertificateErrorsInDevelopment"))
            {
                return true;
            }
            
            // For production: Implement proper validation
            if (sslErrors == SslPolicyErrors.None)
            {
                // Check if the certificate thumbprint matches our trusted thumbprint
                var trustedThumbprint = configuration["TrustedServerCertificateThumbprint"];
                if (!string.IsNullOrEmpty(trustedThumbprint) && 
                    certificate.Thumbprint.Equals(trustedThumbprint, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                
                // For additional security, check if cert is in trusted root store
                var isRootTrusted = chain.ChainElements
                    .Cast<X509ChainElement>()
                    .Any(element => element.Certificate.Subject == element.Certificate.Issuer);
                
                return isRootTrusted;
            }
            
            logger.LogWarning("Server certificate validation failed: {Errors}", sslErrors);
            return false;
        }
    }
}