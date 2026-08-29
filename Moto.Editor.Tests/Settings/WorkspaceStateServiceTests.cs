// Moto.Core.Tests/Settings/WorkspaceStateServiceTests.cs
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moto.Core.Settings;
using Moq;
using Xunit;

namespace Moto.Core.Tests.Settings
{
    public class WorkspaceStateServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly Mock<ILogger<WorkspaceStateService>> _loggerMock;
        private readonly WorkspaceStateService _service;

        public WorkspaceStateServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            _loggerMock = new Mock<ILogger<WorkspaceStateService>>();
            _service = new WorkspaceStateService(_tempDir, _loggerMock.Object);
        }

        [Fact]
        public async Task LoadAsync_FileNotFound_ReturnsEmptyState()
        {
            var state = await _service.LoadAsync();
            Assert.NotNull(state);
            Assert.Empty(state.Sessions);
            Assert.Equal(1, state.Version);
        }

        [Fact]
        public async Task SetSessionSectionAsync_PersistsCorrectly()
        {
            await _service.SetSessionSectionAsync("session1", SessionSection.Pinned);

            var state = await _service.LoadAsync();
            Assert.Equal(SessionSection.Pinned, state.Sessions["session1"]);
        }

        [Fact]
        public async Task SetSessionSectionAsync_RecentRemovesEntry()
        {
            await _service.SetSessionSectionAsync("session1", SessionSection.Pinned);
            await _service.SetSessionSectionAsync("session1", SessionSection.Recent);

            var state = await _service.LoadAsync();
            Assert.False(state.Sessions.ContainsKey("session1"));
        }

        [Fact]
        public async Task SetSessionSectionAsync_ConcurrentCalls_Debounced()
        {
            // Lance 10 appels en rafale
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = _service.SetSessionSectionAsync($"session{i}", SessionSection.Pinned);
            }
            await Task.WhenAll(tasks);

            // Vérifie que toutes les sessions sont persistées
            var state = await _service.LoadAsync();
            Assert.Equal(10, state.Sessions.Count);
        }

        [Fact]
        public async Task LoadAsync_CorruptedFile_ReturnsEmptyState()
        {
            // Crée un fichier corrompu
            var filePath = Path.Combine(_tempDir, ".moto", "workspace.json");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            await File.WriteAllTextAsync(filePath, "{ invalid json }");

            var state = await _service.LoadAsync();
            Assert.NotNull(state);
            Assert.Empty(state.Sessions);
        }

        public void Dispose()
        {
            _service.Dispose();
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
    }
}
