using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;

namespace LeaveLite.Server.Tests;

/// <summary>
/// Boots the real LeaveLite host in Development with a unique throwaway SQLite database per
/// test class. The MCP endpoint at /mcp is exercised by a real ModelContextProtocol SDK client
/// connected over HTTP to the in-memory test server — the stateless Streamable HTTP transport
/// means each connection is a plain request/response conversation, no session handshake dance.
/// </summary>
public sealed class McpServerFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"leavelite-mcp-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                // Absolute path keeps the database out of any project's working directory.
                ["ConnectionStrings:LeaveLite"] = $"Data Source={_databasePath}",
            }));
    }

    /// <summary>
    /// Connects a fresh MCP client to the factory's /mcp endpoint. The transport rides on the
    /// factory's own in-memory test HttpClient, so no real network port is involved.
    /// </summary>
    public async Task<McpClient> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var httpClient = CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress ?? new Uri("http://localhost/"), "mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            httpClient,
            loggerFactory: null,
            ownsHttpClient: false);

        return await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            DeleteDatabaseFiles();
        }
    }

    private void DeleteDatabaseFiles()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            try
            {
                File.Delete(_databasePath + suffix);
            }
            catch (IOException)
            {
                // Best effort: temp files are cleaned up by the OS.
            }
        }
    }
}
