// Moto.Core.Tests/Cloud/MockCloudProvider.cs
// Provider cloud simulé pour tests E2E (in-memory, pas d'API externe).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.Tests.Cloud
{
    public sealed class MockCloudFile
    {
        public string Path { get; init; } = string.Empty;
        public byte[] Content { get; init; } = Array.Empty<byte>();
        public DateTime ModifiedUtc { get; init; } = DateTime.UtcNow;
        public long SizeBytes => Content.Length;
    }

    /// <summary>
    /// Provider cloud simulé en mémoire pour les tests E2E.
    /// Simule Dropbox/GoogleDrive/OneDrive sans réseau.
    /// </summary>
    public sealed class MockCloudProvider
    {
        private readonly Dictionary<string, MockCloudFile> _storage = new();
        private readonly SemaphoreSlim _gate = new(1, 1);
        private int _uploadCount;
        private int _downloadCount;

        public int UploadCount => _uploadCount;
        public int DownloadCount => _downloadCount;
        public int FileCount => _storage.Count;

        public async Task<bool> UploadAsync(string remotePath, byte[] content, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                _storage[remotePath] = new MockCloudFile
                {
                    Path = remotePath,
                    Content = content,
                    ModifiedUtc = DateTime.UtcNow
                };
                Interlocked.Increment(ref _uploadCount);
                return true;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<byte[]?> DownloadAsync(string remotePath, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!_storage.TryGetValue(remotePath, out var file))
                    return null;
                Interlocked.Increment(ref _downloadCount);
                return file.Content;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<bool> ExistsAsync(string remotePath, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                return _storage.ContainsKey(remotePath);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                return _storage.Keys
                    .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<bool> DeleteAsync(string remotePath, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                return _storage.Remove(remotePath);
            }
            finally
            {
                _gate.Release();
            }
        }

        public void Clear()
        {
            _storage.Clear();
            _uploadCount = 0;
            _downloadCount = 0;
        }
    }
}
