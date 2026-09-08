using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using GenericInventory.Data;
using GenericInventory.Products.Dtos;
using GenericInventory.Products.Entities;

namespace GenericInventory.Products.Services;

public class ProductsService
{
    private readonly AppDbContext _db;

    public ProductsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProductDto>> GetAsync(string? search, bool criticalOnly, CancellationToken cancellationToken = default)
    {
        var query = _db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(product =>
                product.Code.Contains(term) ||
                product.Description.Contains(term) ||
                product.CustomFieldsJson.Contains(term));
        }

        if (criticalOnly)
        {
            query = query.Where(product => product.CurrentStock <= product.MinimumStock);
        }

        return await query
            .OrderBy(product => product.Description)
            .Select(product => ProductDto.FromEntity(product))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductDto> CreateAsync(ProductFormDto form, CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(form.Code);
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Informe o codigo do produto.");
        }

        if (string.IsNullOrWhiteSpace(form.Description))
        {
            throw new InvalidOperationException("Informe a descricao do produto.");
        }

        if (await _db.Products.AnyAsync(product => product.Code == code, cancellationToken))
        {
            throw new InvalidOperationException("Ja existe um produto com este codigo.");
        }

        var product = new Product();
        Apply(product, form, code);

        _db.Products.Add(product);
        await _db.SaveChangesAsync(cancellationToken);
        return ProductDto.FromEntity(product);
    }

    public async Task<ProductDto> UpdateAsync(string code, ProductFormDto form, CancellationToken cancellationToken = default)
    {
        var product = await FindByCodeAsync(code, cancellationToken);
        var nextCode = string.IsNullOrWhiteSpace(form.Code) ? product.Code : NormalizeCode(form.Code);

        if (!string.Equals(product.Code, nextCode, StringComparison.OrdinalIgnoreCase) &&
            await _db.Products.AnyAsync(item => item.Code == nextCode, cancellationToken))
        {
            throw new InvalidOperationException("Ja existe um produto com este codigo.");
        }

        Apply(product, form, nextCode);
        await _db.SaveChangesAsync(cancellationToken);
        return ProductDto.FromEntity(product);
    }

    public async Task DeleteAsync(string code, CancellationToken cancellationToken = default)
    {
        var product = await FindByCodeAsync(code, cancellationToken);
        _db.Products.Remove(product);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Product> FindByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = NormalizeCode(code);
        return await _db.Products.FirstOrDefaultAsync(product => product.Code == normalized, cancellationToken)
            ?? throw new KeyNotFoundException("Produto nao encontrado.");
    }

    private static void Apply(Product product, ProductFormDto form, string code)
    {
        product.Code = code;
        product.Description = form.Description.Trim();
        product.CurrentStock = form.CurrentStock;
        product.MinimumStock = form.MinimumStock;
        product.SaleValue = form.SaleValue;
        product.ImagePath = form.ImagePath.Trim();
        product.LegacyImageUrl = form.LegacyImageUrl.Trim();
        product.CustomFieldsJson = JsonSerializer.Serialize(NormalizeFields(form.CustomFields));
        product.Catalyst = string.Empty;
    }

    private static IReadOnlyList<ProductFieldDto> NormalizeFields(IEnumerable<ProductFieldDto>? fields)
    {
        if (fields == null)
        {
            return Array.Empty<ProductFieldDto>();
        }

        return fields
            .Select(field => new ProductFieldDto
            {
                Name = field.Name.Trim(),
                Value = field.Value.Trim()
            })
            .Where(field => !string.IsNullOrWhiteSpace(field.Name) || !string.IsNullOrWhiteSpace(field.Value))
            .Take(24)
            .ToList();
    }

    private static string NormalizeCode(string code)
    {
        return code.Trim();
    }
}
