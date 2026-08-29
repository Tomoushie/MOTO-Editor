using FluentAssertions;
using Moto.Core.AI.Internal;
using Moq;
using Moq.Protected;
using System.Net;
using Xunit;

namespace Moto.Tests.E2E;

/// <summary>
/// Tests E2E : téléchargement interrompu (simulateur réseau).
/// </summary>
public class DownloadInterruptionTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly ModelDownloaderService _downloader;

    public DownloadInterruptionTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_handlerMock.Object);
        _downloader = new ModelDownloaderService(_httpClient);
    }

    [Fact]
    public async Task DownloadWithNetworkFailure_ShouldThrowException()
    {
        // Arrange : simule une erreur réseau après 50% du téléchargement
        var url = "https://example.com/model.onnx";
        var cancellationToken = new CancellationToken();

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == url),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.PartialContent,
                Content = new StreamContent(new InterruptedStream())
            });

        // Act & Assert : le téléchargement doit échouer
        var exception = await Assert.ThrowsAsync<DownloadInterruptedException>(
            () => _downloader.DownloadModelAsync("test-model", progress: _ => { }));

        exception.Message.Should().Contain("interrupted");
    }

    [Fact]
    public async Task DownloadWithResume_ShouldContinueFromByte()
    {
        // Arrange : simule une reprise de téléchargement
        var url = "https://example.com/model.onnx";
        var resumeFrom = 1024L;

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString() == url &&
                    req.Headers.Range?.Ranges.First().From == resumeFrom),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.PartialContent,
                Content = new ByteArrayContent(new byte[1024])
            });

        // Act
        var result = await _downloader.ResumeDownloadAsync(
            "test-model", resumeFrom, progress: _ => { });

        // Assert
        result.Should().NotBeNull();
    }
}

/// <summary>
/// Flux simulé qui s'interrompt au milieu du téléchargement.
/// </summary>
public class InterruptedStream : Stream
{
    private int _bytesRead;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => 1024;
    public override long Position { get => _bytesRead; set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        _bytesRead += count;
        if (_bytesRead > 512) throw new IOException("Network interrupted");
        return count;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
