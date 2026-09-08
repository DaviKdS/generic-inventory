using GenericInventory.Products.Entities;
using System.Text.Json;

namespace GenericInventory.Products.Dtos;

public class ProductDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal SaleValue { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public string LegacyImageUrl { get; set; } = string.Empty;
    public IReadOnlyList<ProductFieldDto> CustomFields { get; set; } = Array.Empty<ProductFieldDto>();
    public bool IsCritical => CurrentStock <= MinimumStock;

    public static ProductDto FromEntity(Product product)
    {
        return new ProductDto
        {
            Id = product.Id,
            Code = product.Code,
            Description = product.Description,
            CurrentStock = product.CurrentStock,
            MinimumStock = product.MinimumStock,
            SaleValue = product.SaleValue,
            ImagePath = product.ImagePath,
            LegacyImageUrl = product.LegacyImageUrl,
            CustomFields = ReadCustomFields(product.CustomFieldsJson)
        };
    }

    private static IReadOnlyList<ProductFieldDto> ReadCustomFields(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<ProductFieldDto>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<ProductFieldDto>>(json) ?? new List<ProductFieldDto>();
        }
        catch (JsonException)
        {
            return Array.Empty<ProductFieldDto>();
        }
    }
}
