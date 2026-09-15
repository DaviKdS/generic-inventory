namespace GenericInventory.Auth.Dtos;

public class ScreenVisibilityDto
{
    public Dictionary<string, List<string>> HiddenScreensByRole { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class ScreenVisibilityFormDto
{
    public Dictionary<string, List<string>> HiddenScreensByRole { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
