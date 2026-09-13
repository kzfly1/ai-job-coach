using System.Text;
using AIJobCoach.Api.Data;
using AIJobCoach.Api.Middleware;
using AIJobCoach.Api.Modules.AI.Application;
using AIJobCoach.Api.Modules.AI.Infrastructure;
using AIJobCoach.Api.Modules.Auth.Application;
using AIJobCoach.Api.Modules.Resumes.Application;
using AIJobCoach.Api.Modules.Resumes.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting AI Job Coach API");

    var builder = WebApplication.CreateBuilder(args);

    var jwtSecret = builder.Configuration["Jwt:Secret"];
    
    if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
    {
        throw new InvalidOperationException("Jwt:Secret must be at least 32 characters.");
    }
    
    var jwtIssuer = builder.Configuration["Jwt:Issuer"];
    var jwtAudience = builder.Configuration["Jwt:Audience"];

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(new CompactJsonFormatter());
    });
    
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSecret)),

                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (context.Request.Cookies.TryGetValue("access_token", out var token))
                    {
                        context.Token = token;
                    }

                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();

    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("Frontend", policy =>
            {
                policy
                    .WithOrigins("http://localhost:3000")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }

    builder.Services.AddScoped<AuthService>();
    builder.Services.AddScoped<JwtService>();
    builder.Services.AddScoped<RegisterRequestValidator>();
    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
    builder.Services.AddScoped<IResumeTextExtractor, ResumeTextExtractor>();
    builder.Services.AddScoped<ResumeService>();
    builder.Services.AddScoped<ResumeAnalysisService>();

    // AI module — OpenAI transport client.
    var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];
    if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(openAiApiKey))
    {
        throw new InvalidOperationException(
            "OpenAI:ApiKey must be configured outside of the Development environment.");
    }

    var openAiBaseUrl = builder.Configuration["OpenAI:BaseUrl"];
    if (string.IsNullOrWhiteSpace(openAiBaseUrl))
    {
        openAiBaseUrl = "https://api.openai.com/";
    }

    builder.Services.AddHttpClient(OpenAIClient.HttpClientName, client =>
    {
        client.BaseAddress = new Uri(openAiBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
        if (!string.IsNullOrWhiteSpace(openAiApiKey))
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", openAiApiKey);
        }
    })
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(new[]
        {
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(4),
            TimeSpan.FromSeconds(8)
        }));

    var promptsDirectory = Path.Combine(
        builder.Environment.ContentRootPath, "Modules", "AI", "Prompts");
    builder.Services.AddSingleton(new PromptLoader(promptsDirectory));
    builder.Services.AddScoped<IOpenAIClient, OpenAIClient>();

    builder.Services.AddControllers();
    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    });

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    var app = builder.Build();

    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.UseCors("Frontend");
    }
    
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "AI Job Coach API terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}