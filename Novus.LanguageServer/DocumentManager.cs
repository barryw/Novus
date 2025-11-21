using System.Collections.Concurrent;
using Antlr4.Runtime.Tree;
using Novus.Diagnostics;
using Novus.Parser;
using Novus.SemanticAnalysis;

namespace Novus.LanguageServer;

/// <summary>
/// Manages open documents and their parse states.
/// Tracks all documents currently open in the IDE, maintains their content,
/// parse trees, and diagnostic information.
/// </summary>
/// <remarks>
/// This class is thread-safe and can handle concurrent document operations.
/// It performs both syntax parsing and semantic analysis for each document.
/// </remarks>
public class DocumentManager
{
    private readonly ConcurrentDictionary<string, DocumentState> _documents = new();
    private readonly string _stdLibPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentManager"/> class.
    /// </summary>
    /// <param name="stdLibPath">Path to the Novus standard library directory.
    /// This is used for import resolution and type checking.</param>
    public DocumentManager(string stdLibPath)
    {
        _stdLibPath = stdLibPath;
        Console.Error.WriteLine($"[LSP] DocumentManager initialized with stdLibPath: {stdLibPath}");
    }

    /// <summary>
    /// Opens a new document and performs initial parsing and analysis.
    /// </summary>
    /// <param name="uri">The URI of the document (e.g., "file:///path/to/file.novus")</param>
    /// <param name="text">The initial content of the document</param>
    /// <param name="version">The document version number (used for change tracking)</param>
    /// <remarks>
    /// This method triggers immediate parsing and semantic analysis of the document.
    /// Diagnostics are collected and stored in the document state.
    /// </remarks>
    public void Open(string uri, string text, int version)
    {
        var state = new DocumentState(uri, text, version);
        _documents[uri] = state;
        ParseDocument(state);
    }

    /// <summary>
    /// Updates an existing document with new content and re-parses it.
    /// </summary>
    /// <param name="uri">The URI of the document to update</param>
    /// <param name="text">The new content of the document</param>
    /// <param name="version">The new document version number</param>
    /// <remarks>
    /// If the document is not currently tracked, this method does nothing.
    /// The document is re-parsed and re-analyzed after the update.
    /// </remarks>
    public void Update(string uri, string text, int version)
    {
        if (_documents.TryGetValue(uri, out var state))
        {
            state.Text = text;
            state.Version = version;
            ParseDocument(state);
        }
    }

    /// <summary>
    /// Closes a document and removes it from tracking.
    /// </summary>
    /// <param name="uri">The URI of the document to close</param>
    /// <remarks>
    /// The document state is immediately discarded. Callers should clear
    /// any published diagnostics for this document.
    /// </remarks>
    public void Close(string uri)
    {
        _documents.TryRemove(uri, out _);
    }

    /// <summary>
    /// Retrieves the current state of a document.
    /// </summary>
    /// <param name="uri">The URI of the document to retrieve</param>
    /// <returns>The document state, or null if the document is not tracked</returns>
    public DocumentState? Get(string uri)
    {
        _documents.TryGetValue(uri, out var state);
        return state;
    }

    private void ParseDocument(DocumentState state)
    {
        var diagnostics = new DiagnosticBag();

        Console.Error.WriteLine($"[LSP] Parsing document: {state.Uri}");

        // Parse in LSP mode (error-tolerant)
        var parser = NovusParserFactory.CreateParser(
            state.Text,
            diagnostics,
            state.Uri,
            NovusParserFactory.ParseMode.LanguageServer
        );

        var tree = parser.compilationUnit();

        Console.Error.WriteLine($"[LSP] Parse completed. Parse errors: {diagnostics.Diagnostics.Count}");

        // Run semantic analysis to catch type errors, etc.
        try
        {
            // Convert URI to file path (remove file:// prefix)
            var filePath = state.Uri.StartsWith("file://")
                ? Uri.UnescapeDataString(state.Uri.Substring("file://".Length))
                : state.Uri;

            var analyzer = new SemanticAnalyzer(filePath, state.Text, _stdLibPath);
            analyzer.Analyze(tree);

            // Merge semantic analysis diagnostics
            foreach (var diagnostic in analyzer.Diagnostics.Diagnostics)
            {
                diagnostics.Add(diagnostic);
            }

            // Store the analyzer for language server features (go-to-definition, hover, etc.)
            state.SemanticAnalyzer = analyzer;

            Console.Error.WriteLine($"[LSP] Semantic analysis completed. Diagnostics so far: {diagnostics.Diagnostics.Count}");

            // Run SSA-based diagnostic analysis for code quality issues
            try
            {
                var ssaAnalyzer = new SsaDiagnosticAnalyzer(analyzer, diagnostics);
                ssaAnalyzer.Analyze();
                Console.Error.WriteLine($"[LSP] SSA analysis completed. Total diagnostics: {diagnostics.Diagnostics.Count}");
            }
            catch (Exception ssaEx)
            {
                Console.Error.WriteLine($"[LSP] SSA analysis failed: {ssaEx.Message}");
                // Continue without SSA diagnostics
            }

            Console.Error.WriteLine($"[LSP] All analysis completed. Total diagnostics: {diagnostics.Diagnostics.Count}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LSP] Semantic analysis failed: {ex.Message}");
            // Continue with just parse diagnostics
        }

        state.ParseTree = tree;
        state.Diagnostics = diagnostics;
    }
}

/// <summary>
/// Represents the state of an open document.
/// Stores the document content, version, parse tree, and diagnostics.
/// </summary>
public class DocumentState
{
    /// <summary>
    /// Gets the URI of the document (e.g., "file:///path/to/file.novus").
    /// </summary>
    public string Uri { get; }

    /// <summary>
    /// Gets or sets the current text content of the document.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the document version number.
    /// Incremented on each change, used for synchronization.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the ANTLR parse tree for the document.
    /// Null if the document hasn't been parsed yet or parsing failed.
    /// </summary>
    public IParseTree? ParseTree { get; set; }

    /// <summary>
    /// Gets or sets the diagnostics (errors and warnings) for the document.
    /// Includes both syntax errors from parsing and semantic errors from analysis.
    /// </summary>
    public DiagnosticBag? Diagnostics { get; set; }

    /// <summary>
    /// Gets or sets the semantic analyzer for the document.
    /// Used for language server features like go-to-definition, hover, etc.
    /// </summary>
    public SemanticAnalyzer? SemanticAnalyzer { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentState"/> class.
    /// </summary>
    /// <param name="uri">The URI of the document</param>
    /// <param name="text">The initial text content</param>
    /// <param name="version">The initial version number</param>
    public DocumentState(string uri, string text, int version)
    {
        Uri = uri;
        Text = text;
        Version = version;
    }
}
