// Moto.Core/Debug/DebugEnginePro.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Moto.Core.Debug
{
    public sealed class ConditionalBreakpoint
    {
        public int Id { get; init; }
        public string FilePath { get; init; } = string.Empty;
        public int Line { get; init; }
        public string Condition { get; init; } = string.Empty;
        public int HitCount { get; set; }
        public bool Enabled { get; set; } = true;
    }

    public sealed class WatchExpression
    {
        public string Expression { get; init; } = string.Empty;
        public string? Value { get; set; }
        public string? Type { get; set; }
        public bool HasError { get; set; }
    }

    public sealed class VariableInfo
    {
        public string Name { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public bool IsExpandable { get; init; }
    }

    public sealed class StackFramePro
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? FilePath { get; init; }
        public int Line { get; init; }
        public int Column { get; init; }
        public IReadOnlyList<VariableInfo> Locals { get; init; } = Array.Empty<VariableInfo>();
    }

    /// <summary>
    /// Debugger Pro : breakpoints conditionnels, watch, call stack avancé, step-out.
    /// Étend DebugEngine (DAP). En production : pilote netcoredbg/lldb via stdio.
    /// </summary>
    public sealed class DebugEnginePro : DebugEngine
    {
        private readonly List<ConditionalBreakpoint> _breakpoints = new();
        private readonly List<WatchExpression> _watches = new();
        private readonly List<StackFramePro> _callStack = new();

        public event Action<IReadOnlyList<StackFramePro>>? CallStackChanged;
        public event Action<IReadOnlyList<WatchExpression>>? WatchesUpdated;

        public ConditionalBreakpoint AddConditionalBreakpoint(string filePath, int line, string condition)
        {
            var bp = new ConditionalBreakpoint
            {
                Id = _breakpoints.Count + 1,
                FilePath = filePath,
                Line = line,
                Condition = condition
            };
            _breakpoints.Add(bp);
            return bp;
        }

        public void RemoveBreakpoint(int id) => _breakpoints.RemoveAll(b => b.Id == id);

        public void ToggleBreakpoint(int id)
        {
            var bp = _breakpoints.Find(b => b.Id == id);
            if (bp != null) bp.Enabled = !bp.Enabled;
        }

        public WatchExpression AddWatch(string expression)
        {
            var w = new WatchExpression { Expression = expression };
            _watches.Add(w);
            return w;
        }

        public void RemoveWatch(string expression) => _watches.RemoveAll(w => w.Expression == expression);

        /// <summary>Évalue toutes les watch expressions (DAP "evaluate").</summary>
        public async Task EvaluateWatchesAsync()
        {
            foreach (var w in _watches)
            {
                try
                {
                    w.Value = $"<eval:{w.Expression}>"; // placeholder DAP evaluate
                    w.Type = "?";
                    w.HasError = false;
                }
                catch (Exception ex)
                {
                    w.Value = ex.Message;
                    w.HasError = true;
                }
            }
            WatchesUpdated?.Invoke(_watches);
            await Task.CompletedTask;
        }

        /// <summary>Step out (DAP "stepOut").</summary>
        public Task StepOutAsync(int threadId = 1) => Task.CompletedTask;

        /// <summary>Call stack avancé avec locales (DAP stackTrace + scopes + variables).</summary>
        public Task<IReadOnlyList<StackFramePro>> GetCallStackAsync(int threadId = 1)
        {
            CallStackChanged?.Invoke(_callStack);
            return Task.FromResult<IReadOnlyList<StackFramePro>>(_callStack);
        }

        public IReadOnlyList<ConditionalBreakpoint> GetBreakpoints() => _breakpoints;
        public IReadOnlyList<WatchExpression> GetWatches() => _watches;
    }
}
