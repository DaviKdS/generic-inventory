# Controle de Estoque

Aplicação ASP.NET 8 para controle genérico de estoque com SPA estática em `wwwroot`, APIs JSON e SQLite local.

## Rodar Localmente

```bash
dotnet restore
dotnet run
```

Ou use o helper:

```bash
python server.py
```

Para publicar com um domínio temporário automático da Cloudflare:

```bash
python public_server.py
```

Esse comando gera uma URL `https://...trycloudflare.com`, grava o endereço atual em `App_Data/current-public-url.txt` e inicia a aplicação com `App__PublicBaseUrl` ajustado para essa URL. Ao reiniciar pelo mesmo script, um novo domínio temporário é gerado e aplicado automaticamente.

- Site: http://localhost:5045
- Swagger: http://localhost:5045/api
- Banco local padrão: `App_Data/generic-inventory-dev.db`

No primeiro start, a aplicação cria o banco vazio e a conta administradora de bootstrap configurada em `appsettings.json` ou por variáveis de ambiente. Nenhum dado, imagem, ícone ou pacote legado é importado por padrão.

## Acesso Developer de Teste

Esta versão beta inclui um perfil `Developer`, acima do `Administrador`, para validar recursos técnicos antes de produção.

- E-mail: `dev@email.com`
- Senha temporária: `191220023`
- Permissão exclusiva: importação de catálogo por XLSX, CSV ou PDF pesquisável.

Troque credenciais temporárias antes de usar o serviço em produção.

## Configuração

Use variáveis de ambiente no formato ASP.NET `__` para valores por ambiente. Veja `.env.example`.

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

Não commite arquivos de `App_Data`, bancos SQLite, tokens, senhas ou URLs reais de webhook.

## Funcionalidades

- Autenticação própria por cookie, senha e perfis.
- Perfil developer: acesso técnico total, atribuição de perfis e importação de catálogo.
- Perfil admin: produtos, funcionários, lembretes e acessos.
- Perfil padrão: consulta de estoque e registro de entradas/saídas.
- Developer pode ocultar telas por perfil para `Admin` e `User`; telas ocultas saem da navegação do respectivo nível.
- Alterações de perfil e visibilidade são sincronizadas automaticamente em sessões desktop e mobile.
- Importação Developer com prévia de colunas, seleção de campos, criação de itens novos e atualização de itens existentes.
- Lembretes de estoque crítico (`EstoqueAtual <= EstoqueMinimo`), editáveis pelo admin.
- Envio de lembrete por Power Automate HTTP quando configurado.
- Fallback por SMTP quando `Smtp__Host` estiver configurado.
- Sem Power Automate e sem SMTP, alertas ficam registrados em log local dentro de `App_Data`.
- Tema claro/escuro com preferencia salva no navegador.
- Navegação interna com documentação, releases e suporte básico PT/EN.

## Próxima Atualização

- Criar tela Developer editável para configurar campos do sistema e campos personalizados por nível de acesso.

## Validação

```bash
dotnet test
```
