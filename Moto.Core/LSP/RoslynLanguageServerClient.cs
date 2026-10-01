// Moto.Core/LSP/RoslynLanguageServerClient.cs
// Client LSP complet basé sur OmniSharp.Extensions.LanguageClient 0.19.9
// (API réactive nouvelle génération : IRequestProgressObservable<TReq, TResp>).
// Gère : diagnostics, complétion, hover, navigation, refactor, inlay hints,
// semantic tokens, code actions, rename, signature help.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
// Aliases pour lever l'ambiguïté avec les types Moto.Core.LSP (CodeAction, InlayHintKind,
// définis dans RoslynLspClient.cs) et System.Range / Microsoft.Maui.Devices.Sensors.Location.
using OmniRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using OmniCodeAction = OmniSharp.Extensions.LanguageServer.Protocol.Models.CodeAction;
using OmniInlayHintKind = OmniSharp.Extensions.LanguageServer.Protocol.Models.InlayHintKind;
using OmniLocation = OmniSharp.Extensions.LanguageServer.Protocol.Models.Location;

namespace Moto.Core.LSP
{
    /// <summary>
    /// Client LSP Roslyn complet.
    /// Communique avec un serveur OmniSharp via stdio.
    /// </summary>
    public sealed class RoslynLanguageServerClient : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly string _serverPath;
        private readonly string _workspaceRoot;
        private LanguageClient? _client;
        private Process? _serverProcess;
        private bool _initialized;
        private readonly SemaphoreSlim _initGate = new(1, 1);
        private readonly Dictionary<string, IReadOnlyList<LspDiagnostic>> _diagnosticsCache = new();

        /// <summary>Déclenché quand des diagnostics sont publiés par le serveur.</summary>
        public event Action<string, IReadOnlyList<LspDiagnostic>>? DiagnosticsPublished;

        public RoslynLanguageServerClient(
            string serverPath,
            string workspaceRoot,
            ILogger logger)
        {
            _serverPath = serverPath ?? throw new ArgumentNullException(nameof(serverPath));
            _workspaceRoot = workspaceRoot ?? throw new ArgumentNullException(nameof(workspaceRoot));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsInitialized => _initialized;

        /// <summary>
        /// Initialise la connexion avec le serveur LSP.
        /// </summary>
        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (_initialized) return;

            await _initGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_initialized) return;

                _logger.LogInformation("[LSP] Démarrage du serveur Roslyn : {Path}", _serverPath);

                var psi = new ProcessStartInfo
                {
                    FileName = _serverPath,
                    Arguments = $"--stdio --workspace \"{_workspaceRoot}\"",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                _serverProcess = Process.Start(psi);
                if (_serverProcess == null)
                {
                    _logger.LogError("[LSP] Impossible de démarrer le serveur.");
                    return;
                }

                // Draîne stderr pour éviter qu'un buffer plein ne bloque le serveur.
                _serverProcess.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        _logger.LogWarning("[LSP stderr] {Message}", e.Data);
                };
                _serverProcess.BeginErrorReadLine();

                _client = LanguageClient.PreInit(options =>
                {
                    options
                        .WithInput(_serverProcess.StandardInput.BaseStream)
                        .WithOutput(_serverProcess.StandardOutput.BaseStream)
                        .WithRootPath(_workspaceRoot)
                        .WithRootUri(DocumentUri.FromFileSystemPath(_workspaceRoot))
                        .WithClientInfo(new ClientInfo { Name = "MOTO Editor", Version = "1.0.0" });
                });

                // Abonnement aux diagnostics publiés par le serveur (notification server→client).
                _client.Register(registry =>
                {
                    registry.OnPublishDiagnostics(HandlePublishDiagnostics);
                });

                await _client.Initialize(ct).ConfigureAwait(false);
                _initialized = true;
                _logger.LogInformation("[LSP] Serveur Roslyn initialisé.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LSP] Échec d'initialisation.");
            }
            finally
            {
                _initGate.Release();
            }
        }

        // ── Gestion des documents ──

        public async Task OpenDocumentAsync(string filePath, string content, CancellationToken ct = default)
        {
            if (_client == null) return;
            await EnsureInitializedAsync(ct);

            var uri = DocumentUri.FromFileSystemPath(filePath);
            _client.DidOpenTextDocument(new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    Uri = uri,
                    LanguageId = DetectLanguageId(filePath),
                    Version = 1,
                    Text = content
                }
            });
        }

        public async Task UpdateDocumentAsync(string filePath, string content, int version, CancellationToken ct = default)
        {
            if (_client == null) return;
            await EnsureInitializedAsync(ct);

            var uri = DocumentUri.FromFileSystemPath(filePath);
            _client.DidChangeTextDocument(new DidChangeTextDocumentParams
            {
                TextDocument = new OptionalVersionedTextDocumentIdentifier { Uri = uri, Version = version },
                ContentChanges = new Container<TextDocumentContentChangeEvent>(
                    new[] { new TextDocumentContentChangeEvent { Text = content } })
            });
        }

        public async Task CloseDocumentAsync(string filePath, CancellationToken ct = default)
        {
            if (_client == null) return;
            await EnsureInitializedAsync(ct);

            var uri = DocumentUri.FromFileSystemPath(filePath);
            _client.DidCloseTextDocument(new DidCloseTextDocumentParams
            {
                TextDocument = new TextDocumentIdentifier(uri)
            });
        }

        // ── Diagnostics ──

        private void HandlePublishDiagnostics(PublishDiagnosticsParams diagnostics)
        {
            var filePath = diagnostics.Uri.GetFileSystemPath();
            var mapped = diagnostics.Diagnostics
                .Select(d => new LspDiagnostic
                {
                    StartLine = d.Range.Start.Line,
                    StartColumn = d.Range.Start.Character,
                    EndLine = d.Range.End.Line,
                    EndColumn = d.Range.End.Character,
                    Severity = MapSeverity(d.Severity),
                    Message = d.Message,
                    Code = d.Code?.String,
                    Source = "roslyn"
                })
                .ToList();

            _diagnosticsCache[filePath] = mapped;
            DiagnosticsPublished?.Invoke(filePath, mapped);
        }

        public IReadOnlyList<LspDiagnostic> GetCachedDiagnostics(string filePath)
            => _diagnosticsCache.TryGetValue(filePath, out var d) ? d : Array.Empty<LspDiagnostic>();

        // ── Complétion ──

        public async Task<IReadOnlyList<LspCompletionItem>> GetCompletionsAsync(
            string filePath, int line, int column, CancellationToken ct = default)
        {
            if (_client == null) return Array.Empty<LspCompletionItem>();
            await EnsureInitializedAsync(ct);

            try
            {
                var result = await _client.RequestCompletion(new CompletionParams
                {
                    TextDocument = new TextDocumentIdentifier(DocumentUri.FromFileSystemPath(filePath)),
                    Position = new Position(line, column)
                }, ct).AsTask().ConfigureAwait(false);

                if (result is null) return Array.Empty<LspCompletionItem>();

                return result.Items.Select(MapCompletionItem).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LSP] Erreur complétion.");
                return Array.Empty<LspCompletionItem>();
            }
        }

        // ── Hover ──

        public async Task<LspHoverInfo?> GetHoverAsync(
            string filePath, int line, int column, CancellationToken ct = default)
        {
            if (_client == null) return null;
            await EnsureInitializedAsync(ct);

            try
            {
                var result = await _client.RequestHover(new HoverParams
                {
                    TextDocument = new TextDocumentIdentifier(DocumentUri.FromFileSystemPath(filePath)),
                    Position = new Position(line, column)
                }, ct).ConfigureAwait(false);

                if (result == null) return null;

                var contents = result.Contents;
                var content = contents is { HasMarkupContent: true }
                    ? contents.MarkupContent?.Value ?? string.Empty
                    : contents.MarkedStrings is not null
                        ? string.Join("\n", contents.MarkedStrings.Select(v => v.Value))
                        : string.Empty;

                return new LspHoverInfo
                {
                    Content = content,
                    StartLine = result.Range?.Start.Line ?? line,
                    StartColumn = result.Range?.Start.Character ?? column,
                    EndLine = result.Range?.End.Line ?? line,
                    EndColumn = result.Range?.End.Character ?? column
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LSP] Erreur hover.");
                return null;
            }
        }

        // ── Navigation symbolique ──

        public async Task<IReadOnlyList<LspLocation>> GetDefinitionAsync(
            string filePath, int line, int column, CancellationToken ct = default)
        {
            if (_client == null) return Array.Empty<LspLocation>();
            await EnsureInitializedAsync(ct);

            try
            {
                var result = await _client.RequestDefinition(new DefinitionParams
                {
                    TextDocument = new TextDocumentIdentifier(DocumentUri.FromFileSystemPath(filePath)),
                    Position = new Position(line, column)
                }, ct).AsTask().ConfigureAwait(false);

                if (result is null) return Array.Empty<LspLocation>();

                return result.Select(MapLocation).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LSP] Erreur definition.");
                return Array.Empty<LspLocation>();
            }
        }

        public async Task<IReadOnlyList<LspLocation>> GetReferencesAsync(
            string filePath, int line, int column, bool includeDeclaration = true, CancellationToken ct = default)
        {
            if (_client == null) return Array.Empty<LspLocation>();
            await EnsureInitializedAsync(ct);

            try
            {
                var result = await _client.RequestReferences(new ReferenceParams
                {
                    TextDocument = new TextDocumentIdentifier(DocumentUri.FromFileSystemPath(filePath)),
                    Position = new Position(line, column),
                    Context = new ReferenceContext { IncludeDeclaration = includeDeclaration }
                }, ct).AsTask().ConfigureAwait(false);

                if (result is null) return Array.Empty<LspLocation>();

                return result.Select(MapLocation).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LSP] Erreur references.");
                return Array.Empty<LspLocation>();
            }
        }

        // ── Refactor sémantique ──

        public async Task<IReadOnlyList<LspCodeAction>> GetCodeActionsAsync(
            string filePath, int startLine, int startCol, int endLine, int endCol, CancellationToken ct = default)
        {
            if (_client == null) return Array.Empty<LspCodeAction>();
            await EnsureInitializedAsync(ct);

            try
            {
                var diagnostics = GetCachedDiagnostics(filePath)
                    .Where(d => d.StartLine >= startLine && d.EndLine <= endLine)
                    .Select(d => new Diagnostic
                    {
                        Range = new OmniRange(
                            new Position(d.StartLine, d.StartColumn),
                            new Position(d.EndLine, d.EndColumn)),
                        Message = d.Message,
                        Severity = MapSeverityBack(d.Severity)
                    })
                    .ToList();

                var result = await _client.RequestCodeAction(new CodeActionParams
                {
                    TextDocument = new TextDocumentIdentifier(DocumentUri.FromFileSystemPath(filePath)),
                    Range = new OmniRange(
                        new Position(startLine, startCol),
                        new Position(endLine, endCol)),
                    Context = new CodeActionContext
                    {
                        Diagnostics = new Container<Diagnostic>(diagnostics)
                    }
                }, ct).AsTask().ConfigureAwait(false);

                if (result is null) return Array.Empty<LspCodeAction>();

                return result
                    .Where(ca => ca.IsCodeAction && ca.CodeAction is not null)
                    .Select(ca => MapCodeAction(ca.CodeAction!))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LSP] Erreur code actions.");
                return Array.Empty<LspCodeAction>();
            }
        }

        public async Task<LspRenameResult> RenameSymbolAsync(
            string filePath, int line, int column, string newName, CancellationToken ct = default)
        {
            if (_client == null)
                return new LspRenameResult { Success = false, Message = "Client non initialisé." };

            await EnsureInitializedAsync(ct);

            try
            {
                var result = await _client.RequestRename(new RenameParams
                {
                    TextDocument = new TextDocumentIdentifier(DocumentUri.FromFileSystemPath(filePath)),
                    Position = new Position(line, column),
                    NewName = newName
                }, ct).ConfigureAwait(false);

                if (result?.Changes == null)
                    return new LspRenameResult { Success = false, Message = "Aucun changement." };

                var changes = result.Changes.ToDictionary(
                    kv => kv.Key.GetFileSystemPath(),
                    kv => (IReadOnlyList<LspTextEdit>)kv.Value.Select(MapTextEdit).ToList());

                return new LspRenameResult
                {
                    Success = true,
                    Message = $"Renommé dans {changes.Count} fichier(s).",
                    Changes = changes
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LSP] Erreur rename.");
                return new LspRenameResult { Success = false, Message = ex.Message };
            }
        }

        // ── Inlay Hints (LSP 3.17+) ──

        public async Task<IReadOnlyList<LspInlayHint>> GetInlayHintsAsync(
            string filePath, int startLine, int endLine, CancellationToken ct = default)
        {
            if (_client == null) return Array.Empty<LspInlayHint>();
            await EnsureInitializedAsync(ct);

            try
            {
                var result = await _client.RequestInlayHints(new InlayHintParams
                {
                    TextDocument = new TextDocumentIdentifier(DocumentUri.FromFileSystemPath(filePath)),
                    Range = new OmniRange(
                        new Position(startLine, 0),
                        new Position(endLine, 0))
                }, ct).ConfigureAwait(false);

                if (result is null) return Array.Empty<LspInlayHint>();

                return result.Select(h => new LspInlayHint
                {
                    Line = h.Position.Line,
                    Column = h.Position.Character,
                    Label = h.Label switch
                    {
                        { HasString: true } => h.Label.String ?? string.Empty,
                        { HasInlayHintLabelParts: true } => h.Label.InlayHintLabelParts is not null
                            ? string.Join("", h.Label.InlayHintLabelParts.Select(p => p.Value))
                            : string.Empty,
                        _ => string.Empty
                    },
                    Kind = h.Kind switch
                    {
                        OmniInlayHintKind.Type => LspInlayHintKind.Type,
                        OmniInlayHintKind.Parameter => LspInlayHintKind.Parameter,
                        _ => LspInlayHintKind.Type
                    },
                    Tooltip = ExtractString(h.Tooltip)
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LSP] Erreur inlay hints.");
                return Array.Empty<LspInlayHint>();
            }
        }

        // ── Semantic Tokens ──

        public async Task<IReadOnlyList<LspSemanticToken>> GetSemanticTokensAsync(
            string filePath, CancellationToken ct = default)
        {
            if (_client == null) return Array.Empty<LspSemanticToken>();
            await EnsureInitializedAsync(ct);

            try
            {
                var result = await _client.RequestSemanticTokensFull(new SemanticTokensParams
                {
                    TextDocument = new TextDocumentIdentifier(DocumentUri.FromFileSystemPath(filePath))
                }, ct).AsTask().ConfigureAwait(false);

                if (result == null) return Array.Empty<LspSemanticToken>();

                return DecodeSemanticTokens(result.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LSP] Erreur semantic tokens.");
                return Array.Empty<LspSemanticToken>();
            }
        }

        // ── Signature Help ──

        public async Task<LspSignatureHelp?> GetSignatureHelpAsync(
            string filePath, int line, int column, CancellationToken ct = default)
        {
            if (_client == null) return null;
            await EnsureInitializedAsync(ct);

            try
            {
                var result = await _client.RequestSignatureHelp(new SignatureHelpParams
                {
                    TextDocument = new TextDocumentIdentifier(DocumentUri.FromFileSystemPath(filePath)),
                    Position = new Position(line, column)
                }, ct).ConfigureAwait(false);

                if (result == null) return null;

                return new LspSignatureHelp
                {
                    Signatures = result.Signatures.Select(s => new LspSignatureInfo
                    {
                        Label = s.Label,
                        Documentation = ExtractString(s.Documentation),
                        Parameters = s.Parameters!.Select(p => new LspParameterInfo
                        {
                            Label = p.Label is { IsLabel: true } ? p.Label.Label ?? string.Empty : string.Empty,
                            Documentation = ExtractString(p.Documentation)
                        }).ToList()
                    }).ToList(),
                    ActiveSignature = result.ActiveSignature ?? 0,
                    ActiveParameter = result.ActiveParameter ?? 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LSP] Erreur signature help.");
                return null;
            }
        }

        // ── Helpers ──

        private async Task EnsureInitializedAsync(CancellationToken ct)
        {
            if (!_initialized)
                await InitializeAsync(ct).ConfigureAwait(false);
        }

        private static string DetectLanguageId(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".cs" => "csharp",
                ".py" => "python",
                ".js" => "javascript",
                ".ts" => "typescript",
                ".json" => "json",
                _ => "plaintext"
            };
        }

        private static LspSeverity MapSeverity(DiagnosticSeverity? severity)
            => severity switch
            {
                DiagnosticSeverity.Error => LspSeverity.Error,
                DiagnosticSeverity.Warning => LspSeverity.Warning,
                DiagnosticSeverity.Information => LspSeverity.Information,
                _ => LspSeverity.Hint
            };

        private static DiagnosticSeverity MapSeverityBack(LspSeverity severity)
            => severity switch
            {
                LspSeverity.Error => DiagnosticSeverity.Error,
                LspSeverity.Warning => DiagnosticSeverity.Warning,
                LspSeverity.Information => DiagnosticSeverity.Information,
                _ => DiagnosticSeverity.Hint
            };

        private static LspCompletionItem MapCompletionItem(CompletionItem item)
            => new()
            {
                Label = item.Label,
                Detail = item.Detail,
                Documentation = ExtractString(item.Documentation),
                InsertText = item.InsertText ?? item.Label,
                Kind = MapCompletionKind(item.Kind),
                SortText = item.SortText
            };

        private static LspCompletionKind MapCompletionKind(CompletionItemKind kind)
            => kind switch
            {
                CompletionItemKind.Method => LspCompletionKind.Method,
                CompletionItemKind.Function => LspCompletionKind.Function,
                CompletionItemKind.Constructor => LspCompletionKind.Constructor,
                CompletionItemKind.Field => LspCompletionKind.Field,
                CompletionItemKind.Variable => LspCompletionKind.Variable,
                CompletionItemKind.Class => LspCompletionKind.Class,
                CompletionItemKind.Interface => LspCompletionKind.Interface,
                CompletionItemKind.Module => LspCompletionKind.Module,
                CompletionItemKind.Property => LspCompletionKind.Property,
                CompletionItemKind.Enum => LspCompletionKind.Enum,
                CompletionItemKind.Keyword => LspCompletionKind.Keyword,
                CompletionItemKind.Snippet => LspCompletionKind.Snippet,
                _ => LspCompletionKind.Text
            };

        private static LspLocation MapLocation(LocationOrLocationLink loc)
        {
            if (loc.IsLocation && loc.Location != null)
                return MapLocation(loc.Location);

            if (loc.IsLocationLink && loc.LocationLink != null)
            {
                var link = loc.LocationLink;
                return new LspLocation
                {
                    FilePath = link.TargetUri.GetFileSystemPath(),
                    Line = link.TargetRange.Start.Line,
                    Column = link.TargetRange.Start.Character,
                    EndLine = link.TargetRange.End.Line,
                    EndColumn = link.TargetRange.End.Character
                };
            }

            return new LspLocation();
        }

        private static LspLocation MapLocation(OmniLocation loc)
            => new()
            {
                FilePath = loc.Uri.GetFileSystemPath(),
                Line = loc.Range.Start.Line,
                Column = loc.Range.Start.Character,
                EndLine = loc.Range.End.Line,
                EndColumn = loc.Range.End.Character
            };

        private static LspCodeAction MapCodeAction(OmniCodeAction action)
            => new()
            {
                Title = action.Title,
                Kind = string.IsNullOrEmpty(action.Kind.ToString()) ? "quickfix" : action.Kind.ToString(),
                Edits = action.Edit?.Changes?
                    .SelectMany(kv => kv.Value.Select(MapTextEdit))
                    .ToList()
            };

        private static LspTextEdit MapTextEdit(TextEdit edit)
            => new()
            {
                StartLine = edit.Range.Start.Line,
                StartColumn = edit.Range.Start.Character,
                EndLine = edit.Range.End.Line,
                EndColumn = edit.Range.End.Character,
                NewText = edit.NewText
            };

        private static string? ExtractString(StringOrMarkupContent? content)
            => content == null ? null
                : content.HasString ? content.String
                : content.MarkupContent?.Value;

        private IReadOnlyList<LspSemanticToken> DecodeSemanticTokens(ImmutableArray<int> data)
        {
            var tokens = new List<LspSemanticToken>();
            var rawData = data.ToArray();

            int line = 0, charPos = 0;
            for (int i = 0; i + 4 < rawData.Length; i += 5)
            {
                int deltaLine = rawData[i];
                int deltaChar = rawData[i + 1];
                int length = rawData[i + 2];
                int tokenType = rawData[i + 3];
                int tokenModifiers = rawData[i + 4];

                if (deltaLine > 0)
                {
                    line += deltaLine;
                    charPos = deltaChar;
                }
                else
                {
                    charPos += deltaChar;
                }

                tokens.Add(new LspSemanticToken
                {
                    Line = line,
                    StartChar = charPos,
                    Length = length,
                    Kind = MapSemanticTokenKind(tokenType),
                    Modifiers = (LspSemanticTokenModifiers)tokenModifiers
                });
            }
            return tokens;
        }

        private static LspSemanticTokenKind MapSemanticTokenKind(int type)
            => type switch
            {
                0 => LspSemanticTokenKind.Namespace,
                1 => LspSemanticTokenKind.Type,
                2 => LspSemanticTokenKind.Class,
                3 => LspSemanticTokenKind.Enum,
                4 => LspSemanticTokenKind.Interface,
                5 => LspSemanticTokenKind.Struct,
                6 => LspSemanticTokenKind.TypeParameter,
                7 => LspSemanticTokenKind.Parameter,
                8 => LspSemanticTokenKind.Variable,
                9 => LspSemanticTokenKind.Property,
                10 => LspSemanticTokenKind.EnumMember,
                11 => LspSemanticTokenKind.Event,
                12 => LspSemanticTokenKind.Function,
                13 => LspSemanticTokenKind.Method,
                14 => LspSemanticTokenKind.Macro,
                15 => LspSemanticTokenKind.Keyword,
                16 => LspSemanticTokenKind.Modifier,
                17 => LspSemanticTokenKind.Comment,
                18 => LspSemanticTokenKind.String,
                19 => LspSemanticTokenKind.Number,
                20 => LspSemanticTokenKind.Regexp,
                21 => LspSemanticTokenKind.Operator,
                _ => LspSemanticTokenKind.Variable
            };

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_client != null)
                {
                    await _client.Shutdown().ConfigureAwait(false);
                    _client.Dispose();
                }
                _serverProcess?.Kill();
                _serverProcess?.Dispose();
            }
            catch { }
            _initGate.Dispose();
        }
    }
}
