using Application.Common;
using Application.Dtos;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace UnitTests.Application;

public class GeoApplicationServiceTests
{
    private readonly Mock<IBatchRepository> _batchRepo = new();
    private readonly Mock<IBatchItemRepository> _batchItemRepo = new();
    private readonly Mock<IGeoCacheRepository> _cacheRepo = new();
    private readonly Mock<IGeoProviderClient> _geoProvider = new();
    private readonly Mock<IBackgroundBatchProcessor> _batchProcessor = new();
    private readonly Mock<ILogger<GeoApplicationService>> _log = new();

    private GeoApplicationService CreateSut()
        => new(
            _batchRepo.Object,
            _batchItemRepo.Object,
            _cacheRepo.Object,
            _geoProvider.Object,
            _batchProcessor.Object,
            _log.Object);
    
    [Fact]
    public async Task GetGeoForIpAsync_When_Ip_Is_NullOrWhitespace_Returns_Validation_Error()
    {
        var sut = CreateSut();

        var result = await sut.GetGeoForIpAsync("   ");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("IP address is required.");
    }

    [Fact]
    public async Task GetGeoForIpAsync_When_Ip_Is_Invalid_Returns_Validation_Error()
    {
        var sut = CreateSut();

        var result = await sut.GetGeoForIpAsync("not-an-ip");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("Invalid IP address format.");
    }

    [Fact]
    public async Task GetGeoForIpAsync_When_Cache_Hit_Returns_Cached_And_Does_Not_Call_Provider()
    {
        // Arrange
        var ip = "8.8.8.8";
        var cacheEntity = new IpGeoCache
        {
            Ip = ip,
            CountryCode = "US",
            CountryName = "United States",
            TimeZone = "America/New_York",
            Latitude = 10.0,
            Longitude = 20.0,
            LastFetchedUtc = DateTime.UtcNow // well within TTL
        };

        _cacheRepo
            .Setup(r => r.GetAsync(ip))
            .ReturnsAsync(cacheEntity);

        var sut = CreateSut();

        // Act
        var result = await sut.GetGeoForIpAsync(ip);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.Ip.Should().Be(ip);
        dto.CountryCode.Should().Be("US");
        dto.CountryName.Should().Be("United States");
        dto.TimeZone.Should().Be("America/New_York");
        dto.Latitude.Should().Be(10.0);
        dto.Longitude.Should().Be(20.0);

        _geoProvider.Verify(
            p => p.FetchIpInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetGeoForIpAsync_When_Not_In_Cache_Fetches_From_Provider_And_Caches()
    {
        // Arrange
        var ip = "1.1.1.1";

        _cacheRepo
            .Setup(r => r.GetAsync(ip))
            .ReturnsAsync((IpGeoCache?)null);

        var providerDto = new IpGeoDto
        {
            Ip = ip,
            CountryCode = "AU",
            CountryName = "Australia",
            TimeZone = "Australia/Sydney",
            Latitude = -33.86,
            Longitude = 151.2094
        };

        _geoProvider
            .Setup(p => p.FetchIpInfoAsync(ip, It.IsAny<CancellationToken>()))
            .ReturnsAsync(providerDto);

        var sut = CreateSut();

        // Act
        var result = await sut.GetGeoForIpAsync(ip);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CountryCode.Should().Be("AU");

        _geoProvider.Verify(
            p => p.FetchIpInfoAsync(ip, It.IsAny<CancellationToken>()),
            Times.Once);

        _cacheRepo.Verify(
            r => r.AddOrUpdateAsync(It.Is<IpGeoCache>(c => c.Ip == ip)),
            Times.Once);
        _cacheRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetGeoForIpAsync_When_Provider_Returns_Null_Returns_Unexpected_Error()
    {
        // Arrange
        var ip = "8.8.4.4";

        _cacheRepo
            .Setup(r => r.GetAsync(ip))
            .ReturnsAsync((IpGeoCache?)null);

        _geoProvider
            .Setup(p => p.FetchIpInfoAsync(ip, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IpGeoDto?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.GetGeoForIpAsync(ip);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unexpected);
        result.Error.Message.Should().Be("Geo provider did not return data.");
    }

    [Fact]
    public async Task EnqueueBatchAsync_When_No_Ips_Returns_Validation_Error()
    {
        var sut = CreateSut();

        var result = await sut.EnqueueBatchAsync(Array.Empty<string>());

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("At least one IP is required.");
    }

    [Fact]
    public async Task EnqueueBatchAsync_When_All_Ips_InvalidOrEmpty_Returns_Validation_Error()
    {
        var sut = CreateSut();

        var result = await sut.EnqueueBatchAsync(new[] { "   ", "\t" });

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("At least one valid IP is required.");
    }

    [Fact]
    public async Task EnqueueBatchAsync_When_Invalid_Ip_Returns_Validation_Error()
    {
        var sut = CreateSut();

        var result = await sut.EnqueueBatchAsync(new[] { "8.8.8.8", "bad-ip" });

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("Invalid IP format: 'bad-ip'.");
    }

    [Fact]
    public async Task EnqueueBatchAsync_Persists_Batch_And_Queues_Background_Processor()
    {
        // Arrange
        var ips = new[] { " 8.8.8.8 ", "8.8.8.8", "1.1.1.1" }; // duplicates + whitespace
        var normalized = new[] { "8.8.8.8", "1.1.1.1" };

        var createdBatchId = Guid.NewGuid();
        Batch? capturedBatch = null;

        _batchRepo
            .Setup(r => r.CreateAsync(It.IsAny<Batch>()))
            .Callback<Batch>(b =>
            {
                capturedBatch = b;
                b.Id = createdBatchId;
            })
            .ReturnsAsync(() => capturedBatch!);

        _batchProcessor
            .Setup(p => p.QueueBatchAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        var result = await sut.EnqueueBatchAsync(ips);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.BatchId.Should().Be(createdBatchId);

        _batchRepo.Verify(r => r.CreateAsync(It.IsAny<Batch>()), Times.Once);
        _batchRepo.Verify(r => r.SaveChangesAsync(), Times.Once);

        _batchProcessor.Verify(
            p => p.QueueBatchAsync(
                It.Is<IEnumerable<string>>(seq => seq.SequenceEqual(normalized)),
                createdBatchId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnqueueBatchAsync_When_Background_Processor_Throws_Maps_To_Unexpected_Error()
    {
        // Arrange
        var ips = new[] { "8.8.8.8" };
        var batchId = Guid.NewGuid();
        Batch? capturedBatch = null;

        _batchRepo
            .Setup(r => r.CreateAsync(It.IsAny<Batch>()))
            .Callback<Batch>(b =>
            {
                capturedBatch = b;
                b.Id = batchId;
            })
            .ReturnsAsync(() => capturedBatch!);

        _batchProcessor
            .Setup(p => p.QueueBatchAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("queue error"));

        var sut = CreateSut();

        // Act
        var result = await sut.EnqueueBatchAsync(ips);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unexpected);
        result.Error.Message.Should().Be("Internal server error while enqueuing batch.");
    }

    // ------------------------------------------------------------
    // GetBatchStatusAsync
    // ------------------------------------------------------------

    [Fact]
    public async Task GetBatchStatusAsync_When_Id_Is_Empty_Returns_Validation_Error()
    {
        var sut = CreateSut();

        var result = await sut.GetBatchStatusAsync(Guid.Empty);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("Batch id is required.");
    }

    [Fact]
    public async Task GetBatchStatusAsync_When_Batch_Not_Found_Returns_NotFound_Error()
    {
        var sut = CreateSut();
        var id = Guid.NewGuid();

        _batchRepo
            .Setup(r => r.GetByIdAsync(id))
            .ReturnsAsync((Batch?)null);

        var result = await sut.GetBatchStatusAsync(id);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Message.Should().Be("Batch not found.");
    }

    [Fact]
    public async Task GetBatchStatusAsync_Maps_Batch_To_Dto_With_Estimated_Completion()
    {
        // Arrange
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var batch = new Batch
        {
            Id = id,
            Status = BatchStatus.Running,
            TotalCount = 10,
            ProcessedCount = 5,
            CreatedAtUtc = now.AddMinutes(-2),
            StartedAtUtc = now.AddMinutes(-2),
            AverageMsPerItem = 1000 // 1 second per item
        };

        _batchRepo
            .Setup(r => r.GetByIdAsync(id))
            .ReturnsAsync(batch);

        var sut = CreateSut();

        // Act
        var result = await sut.GetBatchStatusAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var dto = result.Value;
        dto.BatchId.Should().Be(id);
        dto.Processed.Should().Be(5);
        dto.Total.Should().Be(10);
        dto.Status.Should().Be("Running");
        dto.StartedAtUtc.Should().Be(batch.StartedAtUtc);

        dto.EstimatedCompletionUtc.Should().NotBeNull();
        dto.EstimatedCompletionUtc.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task GetBatchStatusAsync_When_Repo_Throws_Returns_Unexpected_Error()
    {
        var sut = CreateSut();
        var id = Guid.NewGuid();

        _batchRepo
            .Setup(r => r.GetByIdAsync(id))
            .ThrowsAsync(new Exception("db error"));

        var result = await sut.GetBatchStatusAsync(id);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unexpected);
        result.Error.Message.Should().Be("Internal server error while retrieving batch status.");
    }
}
