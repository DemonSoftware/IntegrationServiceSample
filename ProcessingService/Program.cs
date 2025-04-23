using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProcessingService.Configuration;
using ProcessingService.Messaging;
using ProcessingService.Services;
using ProcessingService.Data;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Processing Service API", Version = "v1" });
});

// Configuration
builder.Services.Configure<RabbitMqSettings>(
    builder.Configuration.GetSection("RabbitMqSettings"));
        
builder.Services.Configure<SqlDbSettings>(
    builder.Configuration.GetSection("SqlDbSettings"));

// Register services
builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();
builder.Services.AddSingleton<ISqlRepository, SqlRepository>();
builder.Services.AddSingleton<IMessageProcessor, MessageProcessor>();
        
// Register hosted service for message consumption
builder.Services.AddHostedService<MessageConsumerService>();

var app = builder.Build();

app.UseHttpsRedirection();

app.Run();