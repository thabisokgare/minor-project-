using System.Linq;
using System.Security.Claims;
using ABCRetail.Data;
using ABCRetail.Models;
using ABCRetail.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ABCRetail.Controllers;

[Authorize]
public class OrdersController(ApplicationDbContext context, IStorageService storageService, ILogger<OrdersController> logger) : Controller
{
    private const string OrderQueue = "order-queue";

    public async Task<IActionResult> Checkout()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return RedirectToAction("Login", "Account");
        }

        var cartItems = await context.CartItems
            .Include(ci => ci.Product)
            .Where(ci => ci.UserId == userId)
            .ToListAsync();

        return View(cartItems);
    }

    [HttpGet]
    public IActionResult PlaceOrder()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("PlaceOrder")]
    public async Task<IActionResult> PlaceOrderConfirmed()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return RedirectToAction("Login", "Account");
        }

        var cartItems = await context.CartItems
            .Include(ci => ci.Product)
            .Where(ci => ci.UserId == userId)
            .ToListAsync();

        if (!cartItems.Any())
        {
            TempData["CartEmpty"] = true;
            return RedirectToAction("Index", "Cart");
        }

        var order = new Order
        {
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            Status = "Pending",
            Total = cartItems.Sum(ci => (ci.Product?.Price ?? 0M) * ci.Quantity)
        };

        foreach (var item in cartItems)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.Product?.Price ?? 0M
            });
        }

        context.Orders.Add(order);
        context.CartItems.RemoveRange(cartItems);
        await context.SaveChangesAsync();

        var queuePayload = new
        {
            OrderId = order.Id,
            UserId = userId,
            Total = order.Total,
            CreatedAt = order.CreatedAt,
            Items = order.Items.Select(i => new { i.ProductId, i.Quantity, i.UnitPrice })
        };

        var message = JsonConvert.SerializeObject(queuePayload);
        await storageService.SendToQueueAsync(OrderQueue, message);

        logger.LogInformation("Order {OrderId} placed by user {UserId}", order.Id, userId);

        return RedirectToAction(nameof(Confirmation), new { orderId = order.Id });
    }

    public async Task<IActionResult> MyOrders()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return RedirectToAction("Login", "Account");
        }

        var orders = await context.Orders
            .Include(o => o.Items)
            .ThenInclude(oi => oi.Product)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return View(orders);
    }

    public async Task<IActionResult> Confirmation(int orderId)
    {
        var order = await context.Orders
            .Include(o => o.Items)
            .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null)
        {
            return RedirectToAction(nameof(MyOrders));
        }

        return View(order);
    }
}
