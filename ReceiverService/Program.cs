using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReceiverService.Services;
using ReceiverService.Models;
using ReceiverService.Repositories;
using ReceiverService.Configuration;
using ReceiverService.Messaging;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Configuration;
using System;
using MongoDB;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Receiver Service API", Version = "v1" });
});

// Configure RequestsMongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("RequestsDbSettings"));
builder.Services.AddSingleton<IMongoDbRepository, RequestsDbRepository>();

// Configure MessageMongoDb
builder.Services.Configure<MessageMongoDbSettings>(
    builder.Configuration.GetSection("MessageMongoDbSettings"));
builder.Services.AddSingleton<IMessageRepository, MessageRepository>();

// Configure RabbitMQ
builder.Services.Configure<RabbitMqSettings>(
    builder.Configuration.GetSection("RabbitMqSettings"));
builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();

// Configure Receiver Service
builder.Services.AddScoped<IReceiverService, ReceiverService.Services.ReceiverService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();