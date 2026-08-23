using LeaveLite.Application;
using LeaveLite.Infrastructure;
using LeaveLite.Infrastructure.Initialization;
using ModelContextProtocol.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog console logging, configured from the "Serilog" section of appsettings.
builder.Services.AddSerilog(loggerConfiguration => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// The showcase: LeaveLite exposed as an MCP server over Streamable HTTP at /mcp.
// Stateless mode is the SDK's recommendation for servers that never call back into the client
// (no sampling/elicitation) — each POST is self-contained, no session affinity required.
// Tools/resources/prompts are attribute-discovered from this assembly.
var assembly = typeof(Program).Assembly;

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithToolsFromAssembly(assembly)
    .WithResourcesFromAssembly(assembly)
    .WithPromptsFromAssembly(assembly);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapMcp("/mcp");

// Development convenience: apply migrations and seed the demo organization (idempotent).
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
