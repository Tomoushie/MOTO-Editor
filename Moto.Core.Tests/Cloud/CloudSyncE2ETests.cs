// Moto.Core.Tests/Cloud/CloudSyncE2ETests.cs
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Moto.Core.Tests.Cloud
{
    public class CloudSyncE2ETests : IDisposable
    {
        private readonly string _tempDir;
        private readonly MockCloudProvider _cloud = new();

        public CloudSyncE2ETests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "moto-cloud-e2e-" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        [Fact]
        public async Task E2E_Upload_SingleFile_RoundTrip()
        {
            var content = Encoding.UTF8.GetBytes("Hello MOTO Editor!");
            var remotePath = "/MotoEditor/test.txt";

            var uploadOk = await _cloud.UploadAsync(remotePath, content);
            Assert.True(uploadOk);
            Assert.Equal(1, _cloud.UploadCount);

            var downloaded = await _cloud.DownloadAsync(remotePath);
            Assert.NotNull(downloaded);
            Assert.Equal(content, downloaded);
            Assert.Equal(1, _cloud.DownloadCount);
        }

        [Fact]
        public async Task E2E_Upload_MultipleFiles_PreservesAll()
        {
            for (int i = 0; i < 10; i++)
            {
                var content = Encoding.UTF8.GetBytes($"File {i}");
                await _cloud.UploadAsync($"/MotoEditor/file{i}.txt", content);
            }

            Assert.Equal(10, _cloud.UploadCount);
            Assert.Equal(10, _cloud.FileCount);

            var list = await _cloud.ListAsync("/MotoEditor/");
            Assert.Equal(10, list.Count);
        }

        [Fact]
        public async Task E2E_Overwrite_ReplacesContent()
        {
            var path = "/MotoEditor/overwrite.txt";
            await _cloud.UploadAsync(path, Encoding.UTF8.GetBytes("v1"));
            await _cloud.UploadAsync(path, Encoding.UTF8.GetBytes("v2"));

            var downloaded = await _cloud.DownloadAsync(path);
            Assert.Equal("v2", Encoding.UTF8.GetString(downloaded!));
            Assert.Equal(1, _cloud.FileCount);
        }

        [Fact]
        public async Task E2E_Delete_RemovesFile()
        {
            var path = "/MotoEditor/todelete.txt";
            await _cloud.UploadAsync(path, Encoding.UTF8.GetBytes("x"));
            Assert.True(await _cloud.ExistsAsync(path));

            var deleted = await _cloud.DeleteAsync(path);
            Assert.True(deleted);
            Assert.False(await _cloud.ExistsAsync(path));
        }

        [Fact]
        public async Task E2E_Download_NonExistent_ReturnsNull()
        {
            var result = await _cloud.DownloadAsync("/non/existent.txt");
            Assert.Null(result);
        }

        [Fact]
        public async Task E2E_LargeFile_HandlesCorrectly()
        {
            var largeContent = new byte[1024 * 1024]; // 1 MB
            new Random(42).NextBytes(largeContent);
            var path = "/MotoEditor/large.bin";

            var ok = await _cloud.UploadAsync(path, largeContent);
            Assert.True(ok);

            var downloaded = await _cloud.DownloadAsync(path);
            Assert.NotNull(downloaded);
            Assert.Equal(largeContent.Length, downloaded!.Length);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }
}
