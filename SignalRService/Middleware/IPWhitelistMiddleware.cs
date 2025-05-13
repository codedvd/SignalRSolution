using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using SignalRService.Context;

namespace SignalRService.Middleware
{
    public class IPWhitelistMiddleware(RequestDelegate next, MongoDbContext mongoContext)
    {
        private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
        private readonly MongoDbContext _mongoContext = mongoContext ?? throw new ArgumentNullException(nameof(mongoContext));

        public async Task InvokeAsync(HttpContext context)
        {
            var remoteIp = context.Connection.RemoteIpAddress?.ToString();

            if (string.IsNullOrEmpty(remoteIp))
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Forbidden: IP not allowed.");
                return;
            }
            var mongoDb = _mongoContext.WhitelistedIPs;
            var isWhitelisted = await mongoDb.Find(x => x.IPAddress == remoteIp && x.IsActive).AnyAsync();

            if (!isWhitelisted)
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Forbidden: IP not allowed.");
                return;
            }

            await _next(context);
        }
    }
}
