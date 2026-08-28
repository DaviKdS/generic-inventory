namespace GenericInventory.Auth.AccessControl;

/// <summary>
/// Nomes canonicos das permissoes da aplicacao.
/// Cada permissao vira uma policy de autorizacao registrada em <see cref="AccessControlExtensions"/>.
/// </summary>
public static class AccessPermissions
{
    public const string AccessManage = "access.manage";
    public const string StockRead = "stock.read";
    public const string StockMove = "stock.move";
    public const string ProductsManage = "products.manage";
    public const string EmployeesManage = "employees.manage";
    public const string RemindersManage = "reminders.manage";

    public static IReadOnlyList<string> All { get; } = new[]
    {
        AccessManage,
        StockRead,
        StockMove,
        ProductsManage,
        EmployeesManage,
        RemindersManage
    };

    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [AccessManage] = "Distribuir acessos",
        [StockRead] = "Consultar estoque",
        [StockMove] = "Registrar entradas e saidas",
        [ProductsManage] = "Gerenciar produtos",
        [EmployeesManage] = "Gerenciar funcionarios",
        [RemindersManage] = "Gerenciar lembretes"
    };

    public static string LabelOf(string permission)
    {
        return Labels.TryGetValue(permission, out var label) ? label : permission;
    }
}

