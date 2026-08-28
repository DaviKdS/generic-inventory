using GenericInventory.Auth.Interfaces;

namespace GenericInventory.Auth.Services;

public static class AccessBootstrapper
{
    /// <summary>
    /// Semeia a conta administradora configurada antes de a aplicacao aceitar requisicoes.
    /// Roda de forma sincrona de proposito: nenhuma requisicao deve ser atendida antes disso.
    /// Falha aqui nao derruba a aplicacao, apenas registra log.
    /// </summary>
    public static WebApplication EnsureAccessBootstrap(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(AccessBootstrapper));

        try
        {
            scope.ServiceProvider
                .GetRequiredService<IUserAccessService>()
                .EnsureBootstrapAdminAsync()
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Nao foi possivel semear a conta administradora.");
        }

        return app;
    }
}

