namespace GenericInventory.Products.Dtos;

public class ProductFormDto
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal SaleValue { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public string LegacyImageUrl { get; set; } = string.Empty;
    public string Catalyst { get; set; } = string.Empty;
}
