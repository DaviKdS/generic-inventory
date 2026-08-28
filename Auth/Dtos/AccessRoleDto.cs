namespace GenericInventory.Auth.Dtos;

public class AccessRoleDto
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Level { get; set; }
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
}

