namespace GenericInventory.Auth.AccessControl;

/// <summary>
/// Um degrau da hierarquia de acesso. O nivel define a ordem; as permissoes definem o que o papel pode fazer.
/// </summary>
public sealed class AccessRole
{
    public required string Name { get; init; }

    /// <summary>Peso hierarquico. Quanto maior, mais alto na hierarquia.</summary>
    public required int Level { get; init; }

    public required string Label { get; init; }

    public required string Description { get; init; }

    public required IReadOnlySet<string> Permissions { get; init; }
}

