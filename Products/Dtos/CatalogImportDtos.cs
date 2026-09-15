namespace GenericInventory.Products.Dtos;

public class CatalogImportPreviewDto
{
    public IReadOnlyList<string> Columns { get; set; } = Array.Empty<string>();
    public IReadOnlyList<Dictionary<string, string>> Rows { get; set; } = Array.Empty<Dictionary<string, string>>();
    public int TotalRows { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class CatalogImportMappingDto
{
    public string CodeColumn { get; set; } = string.Empty;
    public string DescriptionColumn { get; set; } = string.Empty;
    public string CurrentStockColumn { get; set; } = string.Empty;
    public string MinimumStockColumn { get; set; } = string.Empty;
    public string SaleValueColumn { get; set; } = string.Empty;
    public string ImagePathColumn { get; set; } = string.Empty;
    public List<string> CustomColumns { get; set; } = new();
}

public class CatalogImportResultDto
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}
