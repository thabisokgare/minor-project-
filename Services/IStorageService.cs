using Azure.Data.Tables;

namespace ABCRetail.Services;

public interface IStorageService
{
    Task SaveToTableAsync(string tableName, ITableEntity entity, CancellationToken cancellationToken = default);

    Task<string?> UploadToBlobAsync(
        string containerName,
        string blobName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task SendToQueueAsync(string queueName, string message, CancellationToken cancellationToken = default);

    Task UploadToFileShareAsync(
        string shareName,
        string directoryName,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);
}
