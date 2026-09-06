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

            // ★ CORRECTION (06/09) : le nom du test tolère les deux issues (throw OU
            // liste vide) mais le corps, lui, n'acceptait QUE "liste non nulle" — sans
            // try/catch, l'InvalidOperationException réelle ("Session de debug non
            // démarrée.") faisait échouer le test alors que throw fait partie du contrat
            // annoncé par son propre nom. On accepte maintenant explicitement les deux.
            try
            {
                var result = engine.SetBreakpointsAsync("/test.cs", new[] { 10, 20 }).Result;
                Assert.NotNull(result);
            }
            catch (AggregateException ex) when (ex.InnerException is InvalidOperationException)
            {
                // Comportement accepté : refuse explicitement sans session démarrée.
            }
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
