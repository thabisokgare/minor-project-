using ABCRetail.Data;
using ABCRetail.Models;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ABCRetail.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private const string CustomersTable = "Customers";
    private const string ContractsShare = "contracts";
    private const string ContractsDirectory = "customer-contracts";

    private readonly ApplicationDbContext _context;
    private readonly TableServiceClient? _tableServiceClient;
    private readonly QueueServiceClient? _queueServiceClient;
    private readonly ShareServiceClient? _shareServiceClient;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        ApplicationDbContext context,
        IServiceProvider services,
        ILogger<AdminController> logger)
    {
        _context = context;
        _logger = logger;
        _tableServiceClient = services.GetService<TableServiceClient>();
        _queueServiceClient = services.GetService<QueueServiceClient>();
        _shareServiceClient = services.GetService<ShareServiceClient>();
    }

    public async Task<IActionResult> Orders()
    {
        var orders = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return View(orders);
    }

    public async Task<IActionResult> OrderDetails(int id)
    {
        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Items)
            .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
        {
            return NotFound();
        }

        return View(order);
    }

    [HttpGet]
    public async Task<IActionResult> UpdateStatus(int id)
    {
        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Items)
            .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
        {
            return NotFound();
        }

        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order is null)
        {
            return NotFound();
        }

        order.Status = status;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Order {OrderId} status updated to {Status}", id, status);

        return RedirectToAction(nameof(OrderDetails), new { id });
    }

    public async Task<IActionResult> Customers()
    {
        if (_tableServiceClient is null)
        {
            ViewBag.AzureUnavailable = true;
            return View(Array.Empty<CustomerEntity>());
        }

        var tableClient = _tableServiceClient.GetTableClient(CustomersTable);
        var customers = new List<CustomerEntity>();

        await foreach (var entity in tableClient.QueryAsync<CustomerEntity>())
        {
            customers.Add(entity);
        }

        return View(customers);
    }

    public async Task<IActionResult> Contracts()
    {
        if (_shareServiceClient is null)
        {
            ViewBag.AzureUnavailable = true;
            return View(Array.Empty<string>());
        }

        var shareClient = _shareServiceClient.GetShareClient(ContractsShare);
        var directoryClient = shareClient.GetDirectoryClient(ContractsDirectory);
        var files = new List<string>();

        await foreach (var item in directoryClient.GetFilesAndDirectoriesAsync())
        {
            if (!item.IsDirectory)
            {
                files.Add(item.Name);
            }
        }

        return View(files);
    }

    public async Task<IActionResult> QueueMonitor()
    {
        if (_queueServiceClient is null)
        {
            ViewBag.AzureUnavailable = true;
            return View(new Dictionary<string, int>());
        }

        var orderQueue = _queueServiceClient.GetQueueClient("order-queue");
        var processingQueue = _queueServiceClient.GetQueueClient("processing-queue");

        var metrics = new Dictionary<string, int>();

        try
        {
            var orderProps = await orderQueue.GetPropertiesAsync();
            metrics["order-queue"] = orderProps.Value.ApproximateMessagesCount;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Unable to read order queue metrics");
            metrics["order-queue"] = 0;
        }

        try
        {
            var processingProps = await processingQueue.GetPropertiesAsync();
            metrics["processing-queue"] = processingProps.Value.ApproximateMessagesCount;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Unable to read processing queue metrics");
            metrics["processing-queue"] = 0;
        }

        return View(metrics);
    }
}
