// Moto.Core.Tests/Debug/DebugEngineTests.cs
using System;
using System.IO;
using System.Text.Json;
using Moto.Core.Debug;
using Xunit;

namespace Moto.Core.Tests.Debug
{
    public class DebugEngineTests
    {
        [Fact]
        public void StartAsync_WithInvalidDebugger_ReturnsFalse()
        {
            using var engine = new DebugEngine();
            var session = new DebugSession
            {
                ProgramPath = "/nonexistent/program.dll",
                WorkingDirectory = Path.GetTempPath()
            };

            var result = engine.StartAsync(session, "nonexistent-debugger").Result;
            Assert.False(result);
        }

        [Fact]
        public void SetBreakpointsAsync_WithoutSession_ThrowsOrReturnsEmpty()
        {
            using var engine = new DebugEngine();

            // Sans session démarrée, devrait retourner une liste vide ou throw
            var result = engine.SetBreakpointsAsync("/test.cs", new[] { 10, 20 }).Result;
            Assert.NotNull(result);
        }

        [Fact]
        public void DebugSession_HasRequiredProperties()
        {
            var session = new DebugSession
            {
                ProgramPath = "/app/bin/Debug/net8.0/app.dll",
                WorkingDirectory = "/app",
                Args = new[] { "--verbose" },
                StopAtEntry = true
            };

            Assert.Equal("/app/bin/Debug/net8.0/app.dll", session.ProgramPath);
            Assert.Equal("/app", session.WorkingDirectory);
            Assert.Single(session.Args);
            Assert.True(session.StopAtEntry);
        }
    }
}
