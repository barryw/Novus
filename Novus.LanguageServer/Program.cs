using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Server;

namespace Novus.LanguageServer;

class Program
{
    static async Task Main(string[] args)
    {
        Console.Error.WriteLine("[LSP] Novus Language Server starting...");

        // Find standard library path
        // The LSP server is in Novus.LanguageServer/bin/Debug/net9.0/
        // The std library is in Novus/std/
        // So we need to go up to the solution root and then into Novus/std
        var compilerDir = AppContext.BaseDirectory;

        // Try to find the std library in multiple locations
        string? stdLibPath = null;
        var possibleStdPaths = new[]
        {
            Path.Combine(compilerDir, "std"), // Same directory as LSP
            Path.Combine(compilerDir, "..", "..", "..", "..", "Novus", "std"), // Relative to LSP bin/Debug/net9.0
            "/Users/barry/RiderProjects/Novus/Novus/std" // Absolute path (development)
        };

        foreach (var path in possibleStdPaths)
        {
            var normalizedPath = Path.GetFullPath(path);
            if (Directory.Exists(normalizedPath))
            {
                stdLibPath = normalizedPath;
                break;
            }
        }

        if (stdLibPath == null)
        {
            stdLibPath = Path.Combine(compilerDir, "std"); // Fallback
            Console.Error.WriteLine($"[LSP] WARNING: std library not found, using fallback: {stdLibPath}");
        }

        Console.Error.WriteLine($"[LSP] Compiler directory: {compilerDir}");
        Console.Error.WriteLine($"[LSP] Standard library path: {stdLibPath}");

        var server = await OmniSharp.Extensions.LanguageServer.Server.LanguageServer.From(options =>
            options
                .WithInput(Console.OpenStandardInput())
                .WithOutput(Console.OpenStandardOutput())
                .ConfigureLogging(x => x
                    .AddLanguageProtocolLogging()
                    .SetMinimumLevel(LogLevel.Debug))
                .WithServices(services =>
                {
                    // Register our services here
                    var docManager = new DocumentManager(stdLibPath);
                    var stdlibIndexer = new StdlibIndexer(stdLibPath);

                    // Index stdlib at startup for auto-import feature
                    stdlibIndexer.IndexStdlib();

                    services.AddSingleton(docManager);
                    services.AddSingleton(stdlibIndexer);
                    services.AddSingleton(stdLibPath);  // Make stdLibPath available for injection
                })
                .WithHandler<TextDocumentHandler>()
                .WithHandler<DefinitionHandler>()
                .WithHandler<HoverHandler>()
                .WithHandler<SignatureHelpHandler>()
                .WithHandler<CompletionHandler>()
                .WithHandler<CodeActionHandler>()
                .WithHandler<ReferencesHandler>()
                .WithHandler<RenameHandler>()
                .OnInitialize((server, request, cancellationToken) =>
                {
                    Console.Error.WriteLine("[LSP] Server initialized!");
                    Console.Error.WriteLine($"[LSP] Root URI: {request.RootUri}");
                    return Task.CompletedTask;
                })
        );

        Console.Error.WriteLine("[LSP] Server created, waiting for exit...");
        await server.WaitForExit;
    }
}
