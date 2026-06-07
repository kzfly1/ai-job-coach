using AIJobCoach.Api.Data;
using AIJobCoach.Api.Modules.Auth.Application;
using Microsoft.EntityFrameworkCore;
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

    builder.Services.AddScoped<AuthService>();
    builder.Services.AddScoped<JwtService>();
    builder.Services.AddScoped<RegisterRequestValidator>();

    var app = builder.Build();

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