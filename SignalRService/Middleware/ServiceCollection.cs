using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SignalRService.Context;
using SignalRService.Hubs;
using System.Reflection;
using System.Text;

namespace SignalRService.Middleware
{
    public static class ServiceCollection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            //load environment variables
            Env.Load();
            var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
            var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING_POSTGREL");
            var mongoDbConnection = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING");
            var mongoDbName = Environment.GetEnvironmentVariable("MONGODB_DATABASE_NAME");
            var Key = Environment.GetEnvironmentVariable("ENCRYPTION_KEY");
            var Iv = Environment.GetEnvironmentVariable("ENCRYPTION_IV");
            var Salt = Environment.GetEnvironmentVariable("ENCRYPTION_SALT");
            var xapikey = Environment.GetEnvironmentVariable("XAPI_KEY");
            var elasticUrl = Environment.GetEnvironmentVariable("ELASTIC_SEARCH_URL");
            var elasticAPIKey = Environment.GetEnvironmentVariable("ELASTIC_APIKEY");
            config["Jwt:Key"] = jwtSecret;
            config["Encryption:Key"] = Key;
            config["Encryption:Iv"] = Iv;
            config["Encryption:Salt"] = Salt;
            config["ConnectionString"] = connectionString;
            config["MongoConnection"] = mongoDbConnection;
            config["MongoDbName"] = mongoDbName;
            config["XapiKey"] = xapikey;
            config["ElasticUrl"] = elasticUrl;
            config["ElasticAPIKey"] = elasticAPIKey;

            services.AddDbContext<DataContext>(options => options.UseNpgsql(config["ConnectionString"]));
            services.AddSingleton<MongoDbContext>();

            // CORS
            //services.AddCors(options =>
            //{
            //    options.AddPolicy("AllowAll", policy =>
            //    {
            //        policy.AllowAnyHeader()
            //              .AllowAnyMethod()
            //              .SetIsOriginAllowed(_ => true) // allows any origin
            //              .AllowCredentials()
            //              .WithExposedHeaders("Content-Disposition");
            //    });
            //});

            //services.AddSignalR(options =>
            //{
            //    options.AddFilter<CustomUserIdProvider>();
            //});

            // JWT Authentication
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)),
                    ValidateIssuer = false,
                    ValidateAudience = false
                };

                // Enable JWT for SignalR via query
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });


            // Swagger (JWT + API Key)
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "KUVE SignalR Service", Version = "v1" });

                var jwtScheme = new OpenApiSecurityScheme
                {
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Name = "JWT Authentication",
                    Type = SecuritySchemeType.Http,
                    Description = "Enter JWT token only.",
                    In = ParameterLocation.Header,
                    Reference = new OpenApiReference
                    {
                        Id = JwtBearerDefaults.AuthenticationScheme,
                        Type = ReferenceType.SecurityScheme
                    }
                };

                c.AddSecurityDefinition(jwtScheme.Reference.Id, jwtScheme);
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    { jwtScheme, Array.Empty<string>() }
                });

                var apiKeyScheme = new OpenApiSecurityScheme
                {
                    Description = "API Key in 'XApiKey' header",
                    Name = "XApiKey",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "ApiKeyScheme",
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "ApiKey"
                    }
                };

                c.AddSecurityDefinition("ApiKey", apiKeyScheme);
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    { apiKeyScheme, new List<string>() }
                });

                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            });

            services.AddEndpointsApiExplorer();
            services.AddAuthorization();

            // SignalR
            services.AddSignalR();

            // Controllers & Memory Cache
            services.AddControllers();
            services.AddMemoryCache();
            services.AddHealthChecks();

            return services;
        }
    }
}
