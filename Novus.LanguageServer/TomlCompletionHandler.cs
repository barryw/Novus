using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Novus.LanguageServer;

/// <summary>
/// Provides completion suggestions for TOML files in Novus projects.
/// Completes section headers, keys, and known values based on the project.toml schema.
/// </summary>
public class TomlCompletionHandler : ICompletionHandler
{
    private readonly ProjectManager _projectManager;

    // Section headers
    private static readonly CompletionItem[] SectionCompletions = new[]
    {
        new CompletionItem
        {
            Label = "[package]",
            Kind = CompletionItemKind.Module,
            Detail = "Package metadata section",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Package metadata and configuration (name, version, type, etc.)"
            },
            InsertText = "[package]\n"
        },
        new CompletionItem
        {
            Label = "[build]",
            Kind = CompletionItemKind.Module,
            Detail = "Build configuration section",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Build configuration and compiler settings (CPU target, optimization, etc.)"
            },
            InsertText = "[build]\n"
        },
        new CompletionItem
        {
            Label = "[paths]",
            Kind = CompletionItemKind.Module,
            Detail = "Path configuration section",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Source and library path configuration"
            },
            InsertText = "[paths]\n"
        },
        new CompletionItem
        {
            Label = "[dependencies]",
            Kind = CompletionItemKind.Module,
            Detail = "Dependencies section",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "External package dependencies"
            },
            InsertText = "[dependencies]\n"
        }
    };

    // Package section keys
    private static readonly CompletionItem[] PackageKeyCompletions = new[]
    {
        new CompletionItem
        {
            Label = "name",
            Kind = CompletionItemKind.Property,
            Detail = "string (required)",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Package name. Used as the output binary name."
            },
            InsertText = "name = \"$1\"",
            InsertTextFormat = InsertTextFormat.Snippet
        },
        new CompletionItem
        {
            Label = "version",
            Kind = CompletionItemKind.Property,
            Detail = "string (required)",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Semantic version (e.g., 1.0.0, 0.2.1)"
            },
            InsertText = "version = \"0.1.0\"",
        },
        new CompletionItem
        {
            Label = "type",
            Kind = CompletionItemKind.Property,
            Detail = "string (required)",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Package type: cli, workbench, dual, library, device, handler, or resource"
            },
            InsertText = "type = \"$1\"",
            InsertTextFormat = InsertTextFormat.Snippet
        },
        new CompletionItem
        {
            Label = "description",
            Kind = CompletionItemKind.Property,
            Detail = "string (optional)",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Brief description of your package"
            },
            InsertText = "description = \"$1\"",
            InsertTextFormat = InsertTextFormat.Snippet
        },
        new CompletionItem
        {
            Label = "entry",
            Kind = CompletionItemKind.Property,
            Detail = "string (optional)",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Entry point file. Defaults to main.novus or <name>.novus"
            },
            InsertText = "entry = \"$1\"",
            InsertTextFormat = InsertTextFormat.Snippet
        },
        new CompletionItem
        {
            Label = "authors",
            Kind = CompletionItemKind.Property,
            Detail = "array of strings (optional)",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "List of package authors"
            },
            InsertText = "authors = [\"$1\"]",
            InsertTextFormat = InsertTextFormat.Snippet
        },
        new CompletionItem
        {
            Label = "license",
            Kind = CompletionItemKind.Property,
            Detail = "string (optional)",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "License identifier (e.g., MIT, Apache-2.0, GPL-3.0)"
            },
            InsertText = "license = \"$1\"",
            InsertTextFormat = InsertTextFormat.Snippet
        }
    };

    // Build section keys
    private static readonly CompletionItem[] BuildKeyCompletions = new[]
    {
        new CompletionItem
        {
            Label = "target_cpu",
            Kind = CompletionItemKind.Property,
            Detail = "string",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Target CPU: 68020, 68030, 68040, 68060, 68080, or auto"
            },
            InsertText = "target_cpu = \"$1\"",
            InsertTextFormat = InsertTextFormat.Snippet
        },
        new CompletionItem
        {
            Label = "fpu",
            Kind = CompletionItemKind.Property,
            Detail = "string",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "FPU configuration: soft, 68881, 68040, or auto"
            },
            InsertText = "fpu = \"$1\"",
            InsertTextFormat = InsertTextFormat.Snippet
        },
        new CompletionItem
        {
            Label = "output",
            Kind = CompletionItemKind.Property,
            Detail = "string",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Output directory for compiled binaries"
            },
            InsertText = "output = \"build\"",
        },
        new CompletionItem
        {
            Label = "optimization_level",
            Kind = CompletionItemKind.Property,
            Detail = "integer",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Optimization level (0-3). Default is 2."
            },
            InsertText = "optimization_level = 2",
        },
        new CompletionItem
        {
            Label = "emit_asm",
            Kind = CompletionItemKind.Property,
            Detail = "boolean",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Emit .s assembly files alongside binaries"
            },
            InsertText = "emit_asm = $1",
            InsertTextFormat = InsertTextFormat.Snippet
        },
        new CompletionItem
        {
            Label = "asm_files",
            Kind = CompletionItemKind.Property,
            Detail = "array of strings",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "External assembly files to link"
            },
            InsertText = "asm_files = [\"$1\"]",
            InsertTextFormat = InsertTextFormat.Snippet
        }
    };

    // Paths section keys
    private static readonly CompletionItem[] PathsKeyCompletions = new[]
    {
        new CompletionItem
        {
            Label = "src",
            Kind = CompletionItemKind.Property,
            Detail = "string",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Source directory containing Novus files"
            },
            InsertText = "src = \".\"",
        },
        new CompletionItem
        {
            Label = "lib",
            Kind = CompletionItemKind.Property,
            Detail = "array of strings",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Additional library search paths"
            },
            InsertText = "lib = [\"$1\"]",
            InsertTextFormat = InsertTextFormat.Snippet
        }
    };

    // Dependency keys (for use inside [dependencies.<name>])
    private static readonly CompletionItem[] DependencyKeyCompletions = new[]
    {
        new CompletionItem
        {
            Label = "version",
            Kind = CompletionItemKind.Property,
            Detail = "string",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Version constraint (e.g., \"1.0.0\", \"^0.2\")"
            },
            InsertText = "version = \"$1\"",
            InsertTextFormat = InsertTextFormat.Snippet
        },
        new CompletionItem
        {
            Label = "path",
            Kind = CompletionItemKind.Property,
            Detail = "string",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Path to local dependency"
            },
            InsertText = "path = \"$1\"",
            InsertTextFormat = InsertTextFormat.Snippet
        },
        new CompletionItem
        {
            Label = "git",
            Kind = CompletionItemKind.Property,
            Detail = "string",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Git repository URL"
            },
            InsertText = "git = \"$1\"",
            InsertTextFormat = InsertTextFormat.Snippet
        },
        new CompletionItem
        {
            Label = "branch",
            Kind = CompletionItemKind.Property,
            Detail = "string",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Git branch (requires git field)"
            },
            InsertText = "branch = \"$1\"",
            InsertTextFormat = InsertTextFormat.Snippet
        },
        new CompletionItem
        {
            Label = "tag",
            Kind = CompletionItemKind.Property,
            Detail = "string",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Git tag (requires git field)"
            },
            InsertText = "tag = \"$1\"",
            InsertTextFormat = InsertTextFormat.Snippet
        }
    };

    // Package type values
    private static readonly CompletionItem[] PackageTypeValues = new[]
    {
        new CompletionItem
        {
            Label = "\"cli\"",
            Kind = CompletionItemKind.Value,
            Detail = "CLI executable",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Command-line interface executable"
            }
        },
        new CompletionItem
        {
            Label = "\"workbench\"",
            Kind = CompletionItemKind.Value,
            Detail = "Workbench application",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Workbench GUI application"
            }
        },
        new CompletionItem
        {
            Label = "\"dual\"",
            Kind = CompletionItemKind.Value,
            Detail = "Dual CLI/Workbench",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Application that works in both CLI and Workbench"
            }
        },
        new CompletionItem
        {
            Label = "\"library\"",
            Kind = CompletionItemKind.Value,
            Detail = "Shared library",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "AmigaOS shared library (.library)"
            }
        },
        new CompletionItem
        {
            Label = "\"device\"",
            Kind = CompletionItemKind.Value,
            Detail = "Device driver",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "AmigaOS device driver (.device)"
            }
        },
        new CompletionItem
        {
            Label = "\"handler\"",
            Kind = CompletionItemKind.Value,
            Detail = "DOS handler",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "AmigaDOS filesystem handler"
            }
        },
        new CompletionItem
        {
            Label = "\"resource\"",
            Kind = CompletionItemKind.Value,
            Detail = "System resource",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "AmigaOS system resource"
            }
        }
    };

    // CPU target values
    private static readonly CompletionItem[] CpuTargetValues = new[]
    {
        new CompletionItem
        {
            Label = "\"68020\"",
            Kind = CompletionItemKind.Value,
            Detail = "68020/68030 (recommended)",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Motorola 68020/68030 CPU (recommended default)"
            }
        },
        new CompletionItem
        {
            Label = "\"68040\"",
            Kind = CompletionItemKind.Value,
            Detail = "68040",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Motorola 68040 CPU (cache-aware)"
            }
        },
        new CompletionItem
        {
            Label = "\"68060\"",
            Kind = CompletionItemKind.Value,
            Detail = "68060",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Motorola 68060 CPU (optimized)"
            }
        },
        new CompletionItem
        {
            Label = "\"auto\"",
            Kind = CompletionItemKind.Value,
            Detail = "Auto-detect",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Runtime CPU detection"
            }
        }
    };

    // FPU values
    private static readonly CompletionItem[] FpuValues = new[]
    {
        new CompletionItem
        {
            Label = "\"soft\"",
            Kind = CompletionItemKind.Value,
            Detail = "Software FPU",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Software floating-point emulation"
            }
        },
        new CompletionItem
        {
            Label = "\"68881\"",
            Kind = CompletionItemKind.Value,
            Detail = "68881/68882",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Motorola 68881 or 68882 FPU"
            }
        },
        new CompletionItem
        {
            Label = "\"68040\"",
            Kind = CompletionItemKind.Value,
            Detail = "68040 FPU",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Built-in 68040 FPU"
            }
        },
        new CompletionItem
        {
            Label = "\"auto\"",
            Kind = CompletionItemKind.Value,
            Detail = "Auto-detect",
            Documentation = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Runtime FPU detection"
            }
        }
    };

    // Boolean values
    private static readonly CompletionItem[] BooleanValues = new[]
    {
        new CompletionItem
        {
            Label = "true",
            Kind = CompletionItemKind.Value,
            Detail = "boolean"
        },
        new CompletionItem
        {
            Label = "false",
            Kind = CompletionItemKind.Value,
            Detail = "boolean"
        }
    };

    public TomlCompletionHandler(ProjectManager projectManager)
    {
        _projectManager = projectManager;
    }

    public Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        var position = request.Position;

        Console.Error.WriteLine($"[LSP] TOML completion request for {uri} at {position.Line}:{position.Character}");

        var completions = new List<CompletionItem>();

        // Get the project state
        var project = _projectManager.GetProject(uri);
        if (project == null)
        {
            Console.Error.WriteLine("[LSP] No TOML project state found");
            return Task.FromResult(new CompletionList(completions));
        }

        try
        {
            // Get the current line
            var lines = project.Text.Split('\n');
            if (position.Line >= lines.Length)
            {
                return Task.FromResult(new CompletionList(completions));
            }

            var line = lines[(int)position.Line];
            var column = (int)position.Character;

            // Determine completion context
            var context = DetermineCompletionContext(line, lines, (int)position.Line, column);

            Console.Error.WriteLine($"[LSP] Completion context: {context.Type}");

            switch (context.Type)
            {
                case CompletionContextType.SectionHeader:
                    completions.AddRange(SectionCompletions);
                    break;

                case CompletionContextType.Key:
                    completions.AddRange(GetKeyCompletionsForSection(context.Section));
                    break;

                case CompletionContextType.Value:
                    completions.AddRange(GetValueCompletions(context.Section, context.Key));
                    break;
            }

            Console.Error.WriteLine($"[LSP] Returning {completions.Count} completions");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LSP] Error generating TOML completions: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
        }

        return Task.FromResult(new CompletionList(completions));
    }

    /// <summary>
    /// Determines what kind of completion is appropriate based on the cursor position.
    /// </summary>
    private static CompletionContext DetermineCompletionContext(string line, string[] lines, int lineIndex, int column)
    {
        var trimmedLine = line.Trim();

        // Empty line or starting with '[' -> section header
        if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("["))
        {
            return new CompletionContext(CompletionContextType.SectionHeader);
        }

        // Check if we're after '=' (value completion)
        var equalsIndex = line.IndexOf('=');
        if (equalsIndex >= 0 && column > equalsIndex)
        {
            // Extract key name
            var keyPart = line.Substring(0, equalsIndex).Trim();
            var section = FindCurrentSection(lines, lineIndex);
            return new CompletionContext(CompletionContextType.Value, section, keyPart);
        }

        // Default to key completion
        var currentSection = FindCurrentSection(lines, lineIndex);
        return new CompletionContext(CompletionContextType.Key, currentSection);
    }

    /// <summary>
    /// Finds the current TOML section by looking backwards.
    /// </summary>
    private static string? FindCurrentSection(string[] lines, int currentLine)
    {
        for (int i = currentLine - 1; i >= 0; i--)
        {
            var match = System.Text.RegularExpressions.Regex.Match(lines[i], @"^\s*\[([^\]]+)\]\s*$");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets key completions for a specific section.
    /// </summary>
    private static IEnumerable<CompletionItem> GetKeyCompletionsForSection(string? section)
    {
        if (section == null)
        {
            return Array.Empty<CompletionItem>();
        }

        return section.ToLowerInvariant() switch
        {
            "package" => PackageKeyCompletions,
            "build" => BuildKeyCompletions,
            "paths" => PathsKeyCompletions,
            var s when s.StartsWith("dependencies.") => DependencyKeyCompletions,
            _ => Array.Empty<CompletionItem>()
        };
    }

    /// <summary>
    /// Gets value completions for a specific key in a section.
    /// </summary>
    private static IEnumerable<CompletionItem> GetValueCompletions(string? section, string? key)
    {
        if (section == null || key == null)
        {
            return Array.Empty<CompletionItem>();
        }

        var sectionLower = section.ToLowerInvariant();
        var keyLower = key.ToLowerInvariant();

        // Package section value completions
        if (sectionLower == "package" && keyLower == "type")
        {
            return PackageTypeValues;
        }

        // Build section value completions
        if (sectionLower == "build")
        {
            if (keyLower == "target_cpu")
            {
                return CpuTargetValues;
            }
            if (keyLower == "fpu")
            {
                return FpuValues;
            }
            if (keyLower == "emit_asm")
            {
                return BooleanValues;
            }
        }

        return Array.Empty<CompletionItem>();
    }

    public CompletionRegistrationOptions GetRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities)
    {
        return new CompletionRegistrationOptions
        {
            DocumentSelector = new TextDocumentSelector(
                new TextDocumentFilter
                {
                    Pattern = "**/*.toml"
                }
            ),
            TriggerCharacters = new[] { "[", "=", "\"" },
            ResolveProvider = false
        };
    }

    private enum CompletionContextType
    {
        SectionHeader,
        Key,
        Value
    }

    private class CompletionContext
    {
        public CompletionContextType Type { get; }
        public string? Section { get; }
        public string? Key { get; }

        public CompletionContext(CompletionContextType type, string? section = null, string? key = null)
        {
            Type = type;
            Section = section;
            Key = key;
        }
    }
}
