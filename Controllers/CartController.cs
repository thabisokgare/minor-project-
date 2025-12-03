using System;
using System.Linq;
using System.Security.Claims;
using ABCRetail.Data;
using ABCRetail.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ABCRetail.Controllers;

[Authorize]
public class CartController(ApplicationDbContext context, ILogger<CartController> logger) : Controller
{
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return RedirectToAction("Login", "Account");
        }

        var items = await context.CartItems
            .Include(ci => ci.Product)
            .Where(ci => ci.UserId == userId)
            .ToListAsync();

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> AddToCart(int productId)
    {
        var product = await context.Products.FindAsync(productId);
        if (product is null)
        {
            return NotFound();
        }

        ViewBag.Product = product;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return RedirectToAction("Login", "Account");
        }

        var cartItem = await context.CartItems.FirstOrDefaultAsync(ci => ci.UserId == userId && ci.ProductId == productId);
        if (cartItem is null)
        {
            cartItem = new CartItem
            {
                UserId = userId,
                ProductId = productId,
                Quantity = quantity
            };
            context.CartItems.Add(cartItem);
        }
        else
        {
            cartItem.Quantity += quantity;
        }

        await context.SaveChangesAsync();
        logger.LogInformation("User {UserId} added product {ProductId} to cart", userId, productId);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Remove(int id)
    {
        var cartItem = await context.CartItems
            .Include(ci => ci.Product)
            .FirstOrDefaultAsync(ci => ci.Id == id);

        if (cartItem is null)
        {
            return NotFound();
        }

        return View(cartItem);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("Remove")]
    public async Task<IActionResult> RemoveConfirmed(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return RedirectToAction("Login", "Account");
        }

        var cartItem = await context.CartItems.FirstOrDefaultAsync(ci => ci.Id == id && ci.UserId == userId);
        if (cartItem is not null)
        {
            context.CartItems.Remove(cartItem);
            await context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Update(int id)
    {
        var cartItem = await context.CartItems
            .Include(ci => ci.Product)
            .FirstOrDefaultAsync(ci => ci.Id == id);

        if (cartItem is null)
        {
            return NotFound();
        }

        return View(cartItem);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, int quantity)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return RedirectToAction("Login", "Account");
        }

        var cartItem = await context.CartItems.FirstOrDefaultAsync(ci => ci.Id == id && ci.UserId == userId);
        if (cartItem is null)
        {
            return RedirectToAction(nameof(Index));
        }

        cartItem.Quantity = Math.Max(1, quantity);
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}
