using Serilog;
using SignalRService.Extensions;
using SignalRService.Hubs;
using SignalRService.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddInfrastructure(builder.Configuration);

// Conditionally add Elasticsearch only if configuration exists
try
{
    builder.Services.AddElasticsearchServices(builder.Configuration);
}
catch (Exception ex)
{
    Console.WriteLine($"Elasticsearch services not initialized: {ex.Message}");
    // Continue without Elasticsearch if it's not critical
}

// Logging with Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

// CORS configuration - add more specific origins if needed in production
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Host.UseSerilog();

// Register SignalR with specific settings for better reliability
builder.Services.AddSignalR(options => {
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 102400; // 100 KB
});

var app = builder.Build();

// Pipeline
app.UseStaticFiles();
app.UseRouting(); // Add explicit routing

app.UseCors("AllowAll");

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "KUVE SignalR Service v1");
});

// Optional: Consider conditionally enabling HTTPS redirection
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ApiKeyMiddleware>();
app.UseMiddleware<IPWhitelistMiddleware>();

// UseEndpoints with explicit routing
app.MapControllers();
app.MapHealthChecks("/health");

// Map SignalR Hubs
app.MapHub<ChatHub>("/hubs/chat").RequireCors("AllowAll");

app.Run();