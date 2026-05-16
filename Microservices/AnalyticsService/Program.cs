using System.Security.Claims;
using System.Text;
using AnalyticsService.BusinessLayer.Abstractions;
using AnalyticsService.BusinessLayer.Implementations;
using AnalyticsService.DataLayer;
using AnalyticsService.DataLayer.Abstractions;
using AnalyticsService.DataLayer.Implementations;
using AnalyticsService.Initializers;
using AnalyticsService.Kafka;
using AnalyticsService.Kafka.Consumers;
using DotNetEnv;
using Kafka.Messaging;
using Kafka.Messaging.Contracts.Requests;
using Kafka.Messaging.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SharedLibrary.Dapper.DapperRepositories;
using SharedLibrary.Dapper.DapperRepositories.Abstractions;

namespace AnalyticsService;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        if (builder.Environment.IsDevelopment())
            Env.Load();

        builder.Configuration.AddEnvironmentVariables();

        ConfigureServices(
            builder.Services,
            builder.Configuration);

        var app = builder.Build();

        using var scope = app.Services.CreateScope();

        using var appDbContext =
            scope.ServiceProvider
                .GetRequiredService<AnalyticsDbContext>();

        await DbContextInitializer.Migrate(appDbContext);

        app.UseCors("AllowApiGateway");

        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseAuthentication();

        app.UseAuthorization();

        app.MapControllers();

        await app.RunAsync();
    }

    private static void ConfigureServices(
        IServiceCollection services,
        IConfigurationManager configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowApiGateway", policy =>
            {
                policy
                    .WithOrigins(
                        Environment.GetEnvironmentVariable(
                            "GATEWAY")!)
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        AddAuthentication(services, configuration);

        /*
         * DATABASE
         */

        var host =
            Environment.GetEnvironmentVariable("HOST");

        var port =
            Environment.GetEnvironmentVariable("PORT");

        var database =
            Environment.GetEnvironmentVariable(
                "POSTGRES_DB");

        var user =
            Environment.GetEnvironmentVariable(
                "USERNAME");

        var pass =
            Environment.GetEnvironmentVariable(
                "PASSWORD");

        var conn =
            $"Host={host};" +
            $"Port={port};" +
            $"Database={database};" +
            $"Username={user};" +
            $"Password={pass}";

        var userDatabase =
            Environment.GetEnvironmentVariable(
                "POSTGRES_USER_DB");

        var userHost =
            Environment.GetEnvironmentVariable(
                "USER_HOST");

        var userConnection =
            $"Host={userHost};" +
            $"Port={port};" +
            $"Database={userDatabase};" +
            $"Username={user};" +
            $"Password={pass}";

        services.AddScoped<IUserRepository>(provider =>
            new UserRepository(userConnection));

        DbContextInitializer.Initialize(
            services,
            conn);

        /*
         * BUSINESS
         */

        services.AddScoped<ITaskManager, TaskManager>();

        services.AddScoped<IProjectManager, ProjectManager>();

        services.AddScoped<
            ITaskHistoryRepository,
            TaskHistoryRepository>();

        /*
         * KAFKA SETTINGS
         */

        // Извлекаем адрес сервера из .env напрямую, чтобы избежать проблем с маппингом регистров букв
        var bootstrapServers = Environment.GetEnvironmentVariable("KAFKA__GETITEMSBYPROJECTREQUEST__BOOTSTRAPSERVERS") ?? "kafka:29092";

        // Регистрируем именованные опции и принудительно проставляем им BootstrapServers
        services.Configure<KafkaSettings>(
            "GetItemsByProjectRequest",
            options => {
                configuration.GetSection("KAFKA:GETITEMSBYPROJECTREQUEST").Bind(options);
                options.BootstrapServers = bootstrapServers;
            });

        services.Configure<KafkaSettings>(
            "GetItemsByProjectResponse",
            options => {
                configuration.GetSection("KAFKA:GETITEMSBYPROJECTRESPONSE").Bind(options);
                options.BootstrapServers = bootstrapServers;
            });

        services.Configure<KafkaSettings>(
            "GetItemByIdRequest",
            options => {
                configuration.GetSection("KAFKA:GETITEMBYIDREQUEST").Bind(options);
                options.BootstrapServers = bootstrapServers;
            });

        services.Configure<KafkaSettings>(
            "GetItemByIdResponse",
            options => {
                configuration.GetSection("KAFKA:GETITEMBYIDRESPONSE").Bind(options);
                options.BootstrapServers = bootstrapServers;
            });

        services.Configure<KafkaSettings>(
            "GetProjectByIdRequest",
            options => {
                configuration.GetSection("KAFKA:GETPROJECTBYIDREQUEST").Bind(options);
                options.BootstrapServers = bootstrapServers;
            });

        services.Configure<KafkaSettings>(
            "GetProjectByIdResponse",
            options => {
                configuration.GetSection("KAFKA:GETPROJECTBYIDRESPONSE").Bind(options);
                options.BootstrapServers = bootstrapServers;
            });

        /*
         * KAFKA PRODUCERS
         */

        services.AddScoped<
            IKafkaProducer<GetItemsByProjectRequest>,
            KafkaProducer<GetItemsByProjectRequest>>();

        services.AddScoped<
            IKafkaProducer<GetItemByIdRequest>,
            KafkaProducer<GetItemByIdRequest>>();

        services.AddScoped<
            IKafkaProducer<GetProjectByIdRequest>,
            KafkaProducer<GetProjectByIdRequest>>();

        /*
         * KAFKA CLIENT
         */

        services.AddScoped<IProjectKafkaClient, ProjectKafkaClient>();

        /*
         * KAFKA CONSUMERS
         */

        services.AddHostedService<
            GetItemsByProjectResponseConsumer>();

        services.AddHostedService<
            GetItemByIdResponseConsumer>();

        services.AddHostedService<
            GetProjectByIdResponseConsumer>();

        /*
         * API
         */

        services.AddControllers();

        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition(
                "Bearer",
                new OpenApiSecurityScheme
                {
                    Description =
                        "JWT Authorization header using Bearer scheme",

                    Name = "Authorization",

                    In = ParameterLocation.Header,

                    Type = SecuritySchemeType.ApiKey
                });

            options.AddSecurityRequirement(
                new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference =
                                new OpenApiReference
                                {
                                    Type =
                                        ReferenceType
                                            .SecurityScheme,

                                    Id = "Bearer"
                                }
                        },
                        Array.Empty<string>()
                    }
                });

            var xmlFile =
                $"{AppDomain.CurrentDomain.FriendlyName}.xml";

            var xmlPath =
                Path.Combine(
                    AppContext.BaseDirectory,
                    xmlFile);

            options.IncludeXmlComments(xmlPath);
        });
    }

    private static void AddAuthentication(
        IServiceCollection services,
        IConfigurationManager configuration)
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme =
                    JwtBearerDefaults.AuthenticationScheme;

                options.DefaultChallengeScheme =
                    JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,

                        ValidateAudience = true,

                        ValidateLifetime = true,

                        ValidateIssuerSigningKey = true,

                        ValidIssuer =
                            configuration["Jwt:Issuer"],

                        ValidAudience =
                            configuration["Jwt:Audience"],

                        IssuerSigningKey =
                            new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(
                                    Environment
                                        .GetEnvironmentVariable(
                                            "JWT_KEY")!)),

                        RoleClaimType =
                            ClaimTypes.Role
                    };
            });
    }
}