using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using GatewayApi.Services;
using GatewayApi.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Force TLS 1.2 or higher
ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

// Add Ocelot configuration files
builder.Configuration.AddJsonFile("Configuration/ocelot.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile($"Configuration/ocelot.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register certificate service
builder.Services.AddSingleton<ICertificateService, CertificateService>();

// Configure HttpClient for client API with mTLS
builder.Services.AddHttpClient<ClientAuthService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ClientApi:BaseUrl"]);
})
.ConfigurePrimaryHttpMessageHandler(serviceProvider =>
{
    var certificateService = serviceProvider.GetRequiredService<ICertificateService>();
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    
    var handler = new HttpClientHandler
    {
        SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        ClientCertificateOptions = ClientCertificateOption.Manual
    };
    
    try
    {
        // Add client certificate for mTLS
        var clientCertificate = certificateService.GetClientCertificate();
        handler.ClientCertificates.Add(clientCertificate);
        
        // Validate server certificate
        handler.ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) =>
            certificateService.ValidateServerCertificate(request, certificate, chain, errors);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error configuring client certificates");
    }
    
    return handler;
});

// Register client auth service
builder.Services.AddScoped<ClientAuthService>();

// Add JWT Authentication for tokens from client's API
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer("ClientJwtSchema", options =>
{
    options.Authority = builder.Configuration["ClientApi:Authority"];
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["ClientApi:ValidIssuer"],
        ValidAudience = builder.Configuration["ClientApi:ValidAudience"],
        ClockSkew = TimeSpan.Zero
    };
});

// Add Ocelot services
builder.Services.AddOcelot();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Configure Ocelot middleware
await app.UseOcelot();

app.Run();