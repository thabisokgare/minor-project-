using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;

namespace ABCRetail.Services;

public class StorageService(
    TableServiceClient tableServiceClient,
    BlobServiceClient blobServiceClient,
    QueueServiceClient queueServiceClient,
    ShareServiceClient shareServiceClient,
    ILogger<StorageService> logger) : IStorageService
{
    public async Task SaveToTableAsync(string tableName, ITableEntity entity, CancellationToken cancellationToken = default)
    {
        try
        {
            var tableClient = tableServiceClient.GetTableClient(tableName);
            await tableClient.CreateIfNotExistsAsync(cancellationToken);
            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save entity to table {Table}", tableName);
            throw;
        }
    }

    public async Task<string?> UploadToBlobAsync(string containerName, string blobName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
            var blobClient = containerClient.GetBlobClient(blobName);

            await blobClient.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: cancellationToken);

            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload blob {Blob} in container {Container}", blobName, containerName);
            throw;
        }
    }

    public async Task SendToQueueAsync(string queueName, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var queueClient = queueServiceClient.GetQueueClient(queueName);
            await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            await queueClient.SendMessageAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send message to queue {Queue}", queueName);
            throw;
        }
    }

    public async Task UploadToFileShareAsync(string shareName, string directoryName, string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        try
        {
            var shareClient = shareServiceClient.GetShareClient(shareName);
            await shareClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            var directoryClient = shareClient.GetDirectoryClient(directoryName);
            await directoryClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            var fileClient = directoryClient.GetFileClient(fileName);
            await fileClient.CreateAsync(content.Length, cancellationToken: cancellationToken);
            await fileClient.UploadRangeAsync(new Azure.HttpRange(0, content.Length), content, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload file {File} to share {Share}/{Directory}", fileName, shareName, directoryName);
            throw;
        }
    }
}
