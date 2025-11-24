using System.Diagnostics;
using Application.Dtos;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using UnitTests.Fakes;

namespace UnitTests.Infrastructure
{
    public class ChannelBackgroundBatchProcessorTests
    {
        [Fact]
        public async Task QueueBatchAsync_Should_Process_All_Ips_And_Complete_Batch()
        {
            // Arrange
            var batchId = Guid.NewGuid();
            var ips = new[] { "1.1.1.1", "8.8.8.8", "8.8.4.4" };

            // Domain.Batch in your code has a parameterless ctor.
            // We set properties manually instead of calling a (Guid, int) ctor.
            var batch = new Batch
            {
                Id = batchId,
                Status = BatchStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow,
                TotalCount = ips.Length,
                ProcessedCount = 0
            };

            foreach (var ip in ips)
            {
                batch.Items.Add(new BatchItem
                {
                    Id = Guid.NewGuid(),
                    BatchId = batchId,
                    Ip = ip,
                    Status = BatchItemStatus.Pending,
                    // Required navigation property – must be set
                    Batch = batch
                });
            }

            var batchRepo = new InMemoryBatchRepository(batch);
            var cacheRepo = new InMemoryGeoCacheRepository();

            var geoClientMock = new Mock<IGeoProviderClient>();

            geoClientMock
                .Setup(c => c.FetchIpInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string ip, CancellationToken _) => new IpGeoDto
                {
                    Ip = ip,
                    CountryCode = "GR",
                    CountryName = "Greece",
                    TimeZone = "Europe/Athens",
                    Latitude = 37.97945,
                    Longitude = 23.71622
                });

            var services = new ServiceCollection();
            services.AddSingleton<IBatchRepository>(batchRepo);
            services.AddSingleton<IGeoCacheRepository>(cacheRepo);
            services.AddSingleton(geoClientMock.Object);
            services.AddLogging();

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<ChannelBackgroundBatchProcessor>>();

            var processor = new ChannelBackgroundBatchProcessor(serviceProvider, logger);

            using var cts = new CancellationTokenSource();

            // Start background execution
            await processor.StartAsync(cts.Token);

            // Act: enqueue one batch
            await processor.QueueBatchAsync(ips, batchId, cts.Token);

            // Wait until the batch is completed or timeout
            var sw = Stopwatch.StartNew();
            while (batch.Status != BatchStatus.Completed && sw.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(100);
            }

            cts.Cancel();
            await processor.StopAsync(CancellationToken.None);

            // Assert
            batch.Status.Should().Be(BatchStatus.Completed);
            batch.ProcessedCount.Should().Be(ips.Length);

            batch.Items.Should().HaveCount(ips.Length);
            batch.Items.All(i => i.Status == BatchItemStatus.Succeeded).Should().BeTrue();

            geoClientMock.Verify(
                c => c.FetchIpInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Exactly(ips.Length));
        }
    }
}
