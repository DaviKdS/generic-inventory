# Controle de Estoque

Aplicacao ASP.NET 8 para controle generico de estoque com SPA estatica em `wwwroot`, APIs JSON e SQLite local.

## Rodar Localmente

```bash
dotnet restore
dotnet run
```

Ou use o helper:

```bash
python server.py
```

- Site: http://localhost:5045
- Swagger: http://localhost:5045/api
- Banco local padrao: `App_Data/generic-inventory-dev.db`

No primeiro start, a aplicacao cria o banco vazio e a conta administradora de bootstrap configurada em `appsettings.json` ou por variaveis de ambiente. Nenhum dado, imagem, icone ou pacote legado e importado por padrao.

## Configuracao

Use variaveis de ambiente no formato ASP.NET `__` para valores por ambiente. Veja `.env.example`.

Principais chaves:

- `Data__ConnectionString`
- `Auth__ApproverEmail`
- `Auth__Bootstrap__Email`
- `App__PublicBaseUrl`
- `Reminders__SchedulerEnabled`
- `PowerAutomateReminders__DailyWebhookUrl`
- `PowerAutomateReminders__MovementWebhookUrl`
- `PowerAutomateReminders__ManualWebhookUrl`
- `Smtp__Host`, `Smtp__User`, `Smtp__Password`

Nao commite arquivos de `App_Data`, bancos SQLite, tokens, senhas ou URLs reais de webhook.

## Funcionalidades

- Autenticacao propria por cookie, senha e perfis.
- Perfil admin: produtos, funcionarios, lembretes e acessos.
- Perfil padrao: consulta de estoque e registro de entradas/saidas.
- Lembretes de estoque critico (`EstoqueAtual <= EstoqueMinimo`), editaveis pelo admin.
- Envio de lembrete por Power Automate HTTP quando configurado.
- Fallback por SMTP quando `Smtp__Host` estiver configurado.
- Sem Power Automate e sem SMTP, alertas ficam registrados em log local dentro de `App_Data`.
- Tema claro/escuro com preferencia salva no navegador.

## Validacao

```bash
dotnet test
```
