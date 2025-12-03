using ABCRetail.Data;
using ABCRetail.Models;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Files.Shares;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ABCRetail.Services;

public class InitializationService(IServiceProvider services, ILogger<InitializationService> logger)
{
    private const string ProductContainer = "product-images";
    private const string OrderQueue = "order-queue";
    private const string ProcessingQueue = "processing-queue";
    private const string ContractsShare = "contracts";
    private const string ContractsDirectory = "customer-contracts";
    private const string CustomersTable = "Customers";
    private const string ProductsTable = "Products";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var tableServiceClient = scope.ServiceProvider.GetService<TableServiceClient>();
        var blobServiceClient = scope.ServiceProvider.GetService<BlobServiceClient>();
        var queueServiceClient = scope.ServiceProvider.GetService<QueueServiceClient>();
        var shareServiceClient = scope.ServiceProvider.GetService<ShareServiceClient>();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();

        logger.LogInformation("Starting application initialization.");

        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Database migration completed.");
        }
        else
        {
            logger.LogInformation("Skipping migrations for provider {ProviderName}.", context.Database.ProviderName);
        }

        if (blobServiceClient is not null && queueServiceClient is not null && shareServiceClient is not null && tableServiceClient is not null)
        {
            await EnsureAzureResourcesAsync(blobServiceClient, queueServiceClient, shareServiceClient, tableServiceClient, cancellationToken);
        }
        else
        {
            logger.LogWarning("Azure Storage clients are not configured. Skipping cloud resource provisioning.");
        }

        await EnsureRolesAsync(roleManager);
        await SeedUsersAsync(userManager, storageService, cancellationToken);
        await SeedProductsAsync(context, storageService, cancellationToken);

        logger.LogInformation("Initialization completed.");
    }

    private static async Task EnsureAzureResourcesAsync(
        BlobServiceClient blobServiceClient,
        QueueServiceClient queueServiceClient,
        ShareServiceClient shareServiceClient,
        TableServiceClient tableServiceClient,
        CancellationToken cancellationToken)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient(ProductContainer);
        await containerClient.CreateIfNotExistsAsync(Azure.Storage.Blobs.Models.PublicAccessType.Blob, cancellationToken: cancellationToken);

        var orderQueueClient = queueServiceClient.GetQueueClient(OrderQueue);
        await orderQueueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var processingQueueClient = queueServiceClient.GetQueueClient(ProcessingQueue);
        await processingQueueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var shareClient = shareServiceClient.GetShareClient(ContractsShare);
        await shareClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var directoryClient = shareClient.GetDirectoryClient(ContractsDirectory);
        await directoryClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var customerTable = tableServiceClient.GetTableClient(CustomersTable);
        await customerTable.CreateIfNotExistsAsync(cancellationToken);
        var productTable = tableServiceClient.GetTableClient(ProductsTable);
        await productTable.CreateIfNotExistsAsync(cancellationToken);
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        var roles = new[] { "Admin", "Customer" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager, IStorageService storageService, CancellationToken cancellationToken)
    {
        var adminUsers = new List<(string Email, string DisplayName)>
        {
            ("admin1@abcretail.demo", "Alex Morgan"),
            ("admin2@abcretail.demo", "Jordan Lee")
        };

        var customerUsers = new List<(string Email, string DisplayName)>
        {
            ("customer1@abcretail.demo", "Jamie Rivera"),
            ("customer2@abcretail.demo", "Taylor Brooks"),
            ("customer3@abcretail.demo", "Morgan Patel")
        };

        const string defaultPassword = "Passw0rd!";

        foreach (var admin in adminUsers)
        {
            if (await userManager.FindByEmailAsync(admin.Email) is ApplicationUser existingAdmin)
            {
                if (!await userManager.IsInRoleAsync(existingAdmin, "Admin"))
                {
                    await userManager.AddToRoleAsync(existingAdmin, "Admin");
                }

                continue;
            }

            var newAdmin = new ApplicationUser
            {
                UserName = admin.Email,
                Email = admin.Email,
                DisplayName = admin.DisplayName,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(newAdmin, defaultPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRolesAsync(newAdmin, new[] { "Admin" });
            }
        }

        foreach (var customer in customerUsers)
        {
            var user = await userManager.FindByEmailAsync(customer.Email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = customer.Email,
                    Email = customer.Email,
                    DisplayName = customer.DisplayName,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(user, defaultPassword);
                if (!result.Succeeded)
                {
                    continue;
                }
            }

            if (!await userManager.IsInRoleAsync(user, "Customer"))
            {
                await userManager.AddToRoleAsync(user, "Customer");
            }

            var customerEntity = new CustomerEntity
            {
                RowKey = user.Id,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName ?? string.Empty,
                IdentityUserId = user.Id
            };

            await storageService.SaveToTableAsync(CustomersTable, customerEntity, cancellationToken);
        }
    }

    private static async Task SeedProductsAsync(ApplicationDbContext context, IStorageService storageService, CancellationToken cancellationToken)
    {
        if (await context.Products.AnyAsync(cancellationToken))
        {
            return;
        }

        var products = new List<Product>
        {
            new() { Name = "Sage Structured Blazer", Description = "Tailored fit with breathable lining.", Price = 2899M, ImageUrl = "https://images.unsplash.com/photo-1521572163474-6864f9cf17ab?auto=format&fit=crop&w=900&q=80" },
            new() { Name = "Charcoal Ripstop Parka", Description = "Weather-ready outerwear for daily commute.", Price = 3299M, ImageUrl = "https://images.unsplash.com/photo-1503342217505-b0a15ec3261c?auto=format&fit=crop&w=900&q=80" },
            new() { Name = "Minimalist Leather Sneaker", Description = "Premium leather with recycled sole.", Price = 1599M, ImageUrl = "https://images.unsplash.com/photo-1529921879218-f99242337d78?auto=format&fit=crop&w=900&q=80" },
            new() { Name = "Everyday Knit Crew", Description = "Soft-touch knit for layering.", Price = 799M, ImageUrl = "https://images.unsplash.com/photo-1542291026-7eec264c27ff?auto=format&fit=crop&w=900&q=80" },
            new() { Name = "Performance Chino", Description = "Stretch fabric chino with moisture control.", Price = 1199M, ImageUrl = "https://images.unsplash.com/photo-1524504388940-b1c1722653e1?auto=format&fit=crop&w=900&q=80" }
        };

        await context.Products.AddRangeAsync(products, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        foreach (var product in products)
        {
            var entity = new ProductEntity
            {
                RowKey = product.Id.ToString(),
                Name = product.Name,
                Price = product.Price,
                BlobName = product.BlobName ?? string.Empty,
                ImageUrl = product.ImageUrl ?? string.Empty
            };

            await storageService.SaveToTableAsync(ProductsTable, entity, cancellationToken);
        }
    }
}
