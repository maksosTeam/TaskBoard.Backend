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

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

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

        services.Configure<KafkaSettings>(
            "GetItemsByProjectRequest",
            configuration.GetSection(
                "KAFKA:GETITEMSBYPROJECTREQUEST"));

        services.Configure<KafkaSettings>(
            "GetItemsByProjectResponse",
            configuration.GetSection(
                "KAFKA:GETITEMSBYPROJECTRESPONSE"));

        services.Configure<KafkaSettings>(
            "GetItemByIdRequest",
            configuration.GetSection(
                "KAFKA:GETITEMBYIDREQUEST"));

        services.Configure<KafkaSettings>(
            "GetItemByIdResponse",
            configuration.GetSection(
                "KAFKA:GETITEMBYIDRESPONSE"));

        services.Configure<KafkaSettings>(
            "GetProjectByIdRequest",
            configuration.GetSection(
                "KAFKA:GETPROJECTBYIDREQUEST"));

        services.Configure<KafkaSettings>(
            "GetProjectByIdResponse",
            configuration.GetSection(
                "KAFKA:GETPROJECTBYIDRESPONSE"));

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

        services.AddScoped<ProjectKafkaClient>();

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