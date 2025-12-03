using System.IO;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;

namespace ABCRetail.Services;

public class NoopStorageService(ILogger<NoopStorageService> logger) : IStorageService
{
    public Task SaveToTableAsync(string tableName, ITableEntity entity, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Skipped saving entity to table {TableName} because Azure Storage is not configured.", tableName);
        return Task.CompletedTask;
    }

    public Task<string?> UploadToBlobAsync(string containerName, string blobName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Skipped uploading blob {BlobName} in container {ContainerName} because Azure Storage is not configured.", blobName, containerName);
        return Task.FromResult<string?>(null);
    }

    public Task SendToQueueAsync(string queueName, string message, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Skipped sending message to queue {QueueName} because Azure Storage is not configured.", queueName);
        return Task.CompletedTask;
    }

    public Task UploadToFileShareAsync(string shareName, string directoryName, string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Skipped uploading file {FileName} to share {ShareName}/{DirectoryName} because Azure Storage is not configured.", fileName, shareName, directoryName);
        return Task.CompletedTask;
    }
}
