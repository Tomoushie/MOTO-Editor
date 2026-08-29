// Moto.Core/LSP/LspModels.cs
// Modèles LSP partagés entre le client Roslyn et l'UI.
using System;
using System.Collections.Generic;

namespace Moto.Core.LSP
{
    public enum LspSeverity { Error, Warning, Information, Hint }

    public sealed class LspDiagnostic
    {
        public int StartLine { get; init; }
        public int StartColumn { get; init; }
        public int EndLine { get; init; }
        public int EndColumn { get; init; }
        public LspSeverity Severity { get; init; }
        public string Message { get; init; } = string.Empty;
        public string? Code { get; init; }
        public string Source { get; init; } = "roslyn";
    }

    public sealed class LspCompletionItem
    {
        public string Label { get; init; } = string.Empty;
        public string? Detail { get; init; }
        public string? Documentation { get; init; }
        public string InsertText { get; init; } = string.Empty;
        public LspCompletionKind Kind { get; init; }
        public string? SortText { get; init; }
    }

    public enum LspCompletionKind
    {
        Text, Method, Function, Constructor, Field, Variable, Class,
        Interface, Module, Property, Unit, Value, Enum, Keyword,
        Snippet, Color, File, Reference, Folder
    }

    public sealed class LspHoverInfo
    {
        public string Content { get; init; } = string.Empty;
        public string? Documentation { get; init; }
        public int StartLine { get; init; }
        public int StartColumn { get; init; }
        public int EndLine { get; init; }
        public int EndColumn { get; init; }
    }

    public sealed class LspLocation
    {
        public string FilePath { get; init; } = string.Empty;
        public int Line { get; init; }
        public int Column { get; init; }
        public int EndLine { get; init; }
        public int EndColumn { get; init; }
    }

    public sealed class LspCodeAction
    {
        public string Title { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty; // quickfix, refactor, source
        public string? DiagnosticsJson { get; init; }
        public IReadOnlyList<LspTextEdit>? Edits { get; init; }
    }

    public sealed class LspTextEdit
    {
        public int StartLine { get; init; }
        public int StartColumn { get; init; }
        public int EndLine { get; init; }
        public int EndColumn { get; init; }
        public string NewText { get; init; } = string.Empty;
    }

    public sealed class LspInlayHint
    {
        public int Line { get; init; }
        public int Column { get; init; }
        public string Label { get; init; } = string.Empty;
        public LspInlayHintKind Kind { get; init; }
        public string? Tooltip { get; init; }
    }

    public enum LspInlayHintKind { Type, Parameter, ReturnValue }

    public sealed class LspSemanticToken
    {
        public int Line { get; init; }
        public int StartChar { get; init; }
        public int Length { get; init; }
        public LspSemanticTokenKind Kind { get; init; }
        public LspSemanticTokenModifiers Modifiers { get; init; } = LspSemanticTokenModifiers.None;
    }

    public enum LspSemanticTokenKind
    {
        Namespace, Type, Class, Enum, Interface, Struct, TypeParameter,
        Parameter, Variable, Property, EnumMember, Event, Function, Method,
        Macro, Keyword, Modifier, Comment, String, Number, Regexp, Operator
    }

    [Flags]
    public enum LspSemanticTokenModifiers
    {
        None = 0,
        Declaration = 1 << 0,
        Definition = 1 << 1,
        Readonly = 1 << 2,
        Static = 1 << 3,
        Deprecated = 1 << 4,
        Abstract = 1 << 5,
        Async = 1 << 6,
        Modification = 1 << 7,
        Documentation = 1 << 8,
        DefaultLibrary = 1 << 9
    }

    public sealed class LspSymbolInfo
    {
        public string Name { get; init; } = string.Empty;
        public LspSymbolKind Kind { get; init; }
        public string FilePath { get; init; } = string.Empty;
        public int Line { get; init; }
        public int Column { get; init; }
        public string? ContainerName { get; init; }
    }

    public enum LspSymbolKind
    {
        File, Module, Namespace, Package, Class, Method, Property, Field,
        Constructor, Enum, Interface, Function, Variable, Constant, String,
        Number, Boolean, Array, Object, Key, Null, EnumMember, Struct, Event,
        Operator, TypeParameter
    }

    public sealed class LspRenameResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public IReadOnlyDictionary<string, IReadOnlyList<LspTextEdit>>? Changes { get; init; }
    }

    public sealed class LspSignatureHelp
    {
        public IReadOnlyList<LspSignatureInfo> Signatures { get; init; } = Array.Empty<LspSignatureInfo>();
        public int ActiveSignature { get; init; }
        public int ActiveParameter { get; init; }
    }

    public sealed class LspSignatureInfo
    {
        public string Label { get; init; } = string.Empty;
        public string? Documentation { get; init; }
        public IReadOnlyList<LspParameterInfo> Parameters { get; init; } = Array.Empty<LspParameterInfo>();
    }

    public sealed class LspParameterInfo
    {
        public string Label { get; init; } = string.Empty;
        public string? Documentation { get; init; }
    }
}
