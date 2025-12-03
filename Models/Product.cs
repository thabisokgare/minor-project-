using System.ComponentModel.DataAnnotations;

namespace ABCRetail.Models;

public class Product
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999")]
    public decimal Price { get; set; }

    public string? ImageUrl { get; set; }
    public string? BlobName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
