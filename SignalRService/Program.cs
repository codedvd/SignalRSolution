using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using SignalRService.Extensions;
using SignalRService.Hubs;
using SignalRService.Middleware;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddElasticsearchServices(builder.Configuration);

// Logging with Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials();
    });
});

builder.Host.UseSerilog();

var app = builder.Build();
app.UseCors("AllowAll");

// Pipeline
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "KUVE SignalR Service v1");
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<ApiKeyMiddleware>();
app.UseMiddleware<IPWhitelistMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health");

// Map SignalR Hubs
app.MapHub<ChatHub>("/hubs/chat").RequireCors("AllowAll"); ;

app.Run();
