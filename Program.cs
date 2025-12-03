using ABCRetail.Data;
using ABCRetail.Models;
using ABCRetail.Services;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var sqlConnection = builder.Configuration.GetConnectionString("AzureSqlConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (string.IsNullOrWhiteSpace(sqlConnection))
    {
        options.UseInMemoryDatabase("ABCRetailDev");
    }
    else
    {
        options.UseSqlServer(sqlConnection);
    }
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
});

var storageConnection = builder.Configuration.GetConnectionString("AzureStorageConnection");
if (string.IsNullOrWhiteSpace(storageConnection))
{
    storageConnection = builder.Configuration["AzureStorageConnection"];
}

var azureConfigured = !string.IsNullOrWhiteSpace(storageConnection)
    && !storageConnection.Contains("<", StringComparison.OrdinalIgnoreCase)
    && !storageConnection.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase)
    && !storageConnection.Contains("TODO", StringComparison.OrdinalIgnoreCase);

if (azureConfigured)
{
    try
    {
        var tableClient = new TableServiceClient(storageConnection!);
        var blobClient = new BlobServiceClient(storageConnection!);
        var queueClient = new QueueServiceClient(storageConnection!);
        var shareClient = new ShareServiceClient(storageConnection!);

        builder.Services.AddSingleton(tableClient);
        builder.Services.AddSingleton(blobClient);
        builder.Services.AddSingleton(queueClient);
        builder.Services.AddSingleton(shareClient);

        builder.Services.AddScoped<IStorageService, StorageService>();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Azure Storage configuration invalid: {ex.Message}. Falling back to no-op storage service.");
        azureConfigured = false;
    }
}

if (!azureConfigured)
{
    builder.Services.AddSingleton<IStorageService, NoopStorageService>();
}
builder.Services.AddScoped<InitializationService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<InitializationService>();
    await initializer.InitializeAsync();
}

app.Run();
