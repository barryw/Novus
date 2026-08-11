using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Server;

namespace Novus.LanguageServer;

class Program
{
    static async Task Main(string[] args)
    {
        // Find standard library path
        // The LSP server is in Novus.LanguageServer/bin/Debug/net10.0/
        // The std library is in Novus/std/
        // So we need to go up to the solution root and then into Novus/std
        var compilerDir = AppContext.BaseDirectory;

        // Try to find the std library in multiple locations
        var stdLibPath = Novus.PathUtility.FindStdLibPath(compilerDir);

        // Fallback to std subdirectory if not found
        if (stdLibPath == null)
        {
            stdLibPath = Path.Combine(compilerDir, "std");
        }

        var server = await OmniSharp.Extensions.LanguageServer.Server.LanguageServer.From(options =>
            options
                .WithInput(Console.OpenStandardInput())
                .WithOutput(Console.OpenStandardOutput())
                .ConfigureLogging(x => x
                    .AddFilter("OmniSharp", LogLevel.None)
                    .AddFilter("Microsoft", LogLevel.None)
                    .SetMinimumLevel(LogLevel.None))
                .WithServices(services =>
                {
                    // Register our services here
                    var docManager = new DocumentManager(stdLibPath);
                    var stdlibIndexer = new StdlibIndexer(stdLibPath);
                    var projectManager = new ProjectManager();

                    // Link DocumentManager to ProjectManager for CPU config lookup
                    docManager.SetProjectManager(projectManager);

                    // Index stdlib at startup for auto-import feature
                    stdlibIndexer.IndexStdlib();

                    services.AddSingleton(docManager);
                    services.AddSingleton(stdlibIndexer);
                    services.AddSingleton(projectManager);
                    services.AddSingleton<TomlDocumentHandler>();
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
                // TOML support handlers
                .WithHandler<TomlHoverHandler>()
                .WithHandler<TomlCompletionHandler>()
                .OnInitialize((server, request, cancellationToken) =>
                {
                    // Set workspace root in ProjectManager
                    var projectManager = server.Services.GetService(typeof(ProjectManager)) as ProjectManager;
                    projectManager?.SetWorkspaceRoot(request.RootUri?.ToString());

                    return Task.CompletedTask;
                })
        );

        await server.WaitForExit;
    }
}
