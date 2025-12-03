using System.IO;
using ABCRetail.Data;
using ABCRetail.Models;
using ABCRetail.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ABCRetail.Controllers;

[Authorize]
public class ProductsController(ApplicationDbContext context, IStorageService storageService, ILogger<ProductsController> logger)
    : Controller
{
    private const string ProductContainer = "product-images";

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        var products = await context.Products.OrderByDescending(p => p.CreatedAt).ToListAsync();
        return View(products);
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var product = await context.Products.FindAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        return View(product);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
    {
        if (!ModelState.IsValid)
        {
            return View(product);
        }

        if (imageFile is not null && imageFile.Length > 0)
        {
            await using var stream = imageFile.OpenReadStream();
            var blobName = $"product-{Guid.NewGuid():N}{Path.GetExtension(imageFile.FileName)}";
            var imageUrl = await storageService.UploadToBlobAsync(ProductContainer, blobName, stream, imageFile.ContentType);
            product.ImageUrl = imageUrl;
            product.BlobName = blobName;
        }

        context.Products.Add(product);
        await context.SaveChangesAsync();

        logger.LogInformation("Product {ProductName} created", product.Name);
        return RedirectToAction(nameof(Index));
    }
}
