# Controle de Estoque

Aplicação ASP.NET 8 para controle genérico de estoque com SPA estática em `wwwroot`, APIs JSON e SQLite local.

## Site publicado

Endereço fixo da aplicação no Azure App Service:

- **Produção / acesso público:** https://generic-inventory-davikds-free.azurewebsites.net

O ambiente está publicado no Azure App Service em plano **F1 / Free**, na região **Central US**, com deploy automático pela branch `main` via GitHub Actions.

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

- Site local: http://localhost:5045
- Site Azure: https://generic-inventory-davikds-free.azurewebsites.net
- Swagger: http://localhost:5045/api
- Banco local padrão: `App_Data/generic-inventory-dev.db`

No primeiro start, a aplicação cria o banco vazio e a conta administradora de bootstrap configurada em `appsettings.json` ou por variáveis de ambiente. Nenhum dado, imagem, ícone ou pacote legado é importado por padrão.

## Acesso Developer de Teste

O perfil `Developer` fica acima do `Administrador`, mas não existe uma credencial padrão no
repositório. Para provisioná-lo localmente, configure explicitamente um hash de senha fora do
Git com `Auth__Bootstrap__Developer__Enabled=true` e
`Auth__Bootstrap__Developer__PasswordHash`. Em produção, mantenha esse provisionamento desligado
até definir uma credencial própria no cofre de segredos.

O cadastro aberto também fica desligado por padrão na configuração base. Ative-o somente em
ambiente controlado, quando o fluxo de aprovação estiver configurado.

## Configuração do Azure pelo GitHub Actions

O workflow manual `.github/workflows/configure-azure-settings.yml` prepara as App Settings de
produção no App Service existente. Ele usa OIDC e os mesmos segredos `AZURE_CLIENT_ID`,
`AZURE_TENANT_ID` e `AZURE_SUBSCRIPTION_ID` usados pelo deploy. Execute-o em **Actions > Configure
Azure production settings > Run workflow** antes do primeiro deploy.

O workflow aplica HTTPS/produção, desliga Swagger e cadastro aberto, configura a URL pública e
mantém o banco SQLite em `App_Data`. Em uma instalação Azure nova, o Admin só é provisionado
quando os GitHub Secrets `AUTH_BOOTSTRAP_ADMIN_EMAIL` e `AUTH_BOOTSTRAP_ADMIN_PASSWORD_HASH`
existem. O Developer só é provisionado quando `AUTH_BOOTSTRAP_DEVELOPER_PASSWORD_HASH` existe.
Os valores de hash devem ser gerados pelo serviço, nunca ser senhas em texto puro. Depois de criar
as contas iniciais, remova esses Secrets de bootstrap e execute a Action novamente para manter o
bootstrap desligado.

Integrações opcionais podem ser fornecidas por secrets `APPROVAL_POWER_AUTOMATE_WEBHOOK_URL`,
`POWER_AUTOMATE_DAILY_WEBHOOK_URL`, `POWER_AUTOMATE_MOVEMENT_WEBHOOK_URL`,
`POWER_AUTOMATE_MANUAL_WEBHOOK_URL`, `POWER_AUTOMATE_SHARED_SECRET`, `SMTP_HOST`, `SMTP_PORT`,
`SMTP_ENABLE_SSL`, `SMTP_USER`, `SMTP_PASSWORD` e `SMTP_FROM`. Secrets não configurados não
substituem valores já existentes no Azure.

## Configuração

Use variáveis de ambiente no formato ASP.NET `__` para valores por ambiente. Veja `.env.example`.

Principais chaves:

- `Data__ConnectionString`
- `Auth__ApproverEmail`
- `Auth__Bootstrap__Email`
- `Auth__Bootstrap__PasswordHash` (somente hash fornecido por segredo)
- `Auth__Bootstrap__Developer__PasswordHash` (somente hash fornecido por segredo)
- `Auth__Bootstrap__Enabled`
- `Auth__AllowSelfRegistration`
- `SwaggerEnabled`
- `App__PublicBaseUrl`
- `Reminders__SchedulerEnabled`
- `PowerAutomateReminders__DailyWebhookUrl`
- `PowerAutomateReminders__MovementWebhookUrl`
- `PowerAutomateReminders__ManualWebhookUrl`
- `Smtp__Host`, `Smtp__User`, `Smtp__Password`

Não commite arquivos de `App_Data`, bancos SQLite, anexos do Codex, tokens, senhas, hashes de
bootstrap ou URLs reais de webhook. Em produção, o Swagger fica desligado por padrão e o cookie
exige HTTPS.

## Funcionalidades

- Autenticação própria por cookie, senha e perfis.
- Perfil developer: acesso técnico total, atribuição de perfis e importação de catálogo.
- Perfil admin: produtos, funcionários, lembretes e acessos.
- Perfil padrão: consulta de estoque e registro de entradas/saídas.
- Developer pode ocultar telas por perfil para `Admin` e `User`, incluindo `Acessos`; telas ocultas saem da navegação do respectivo nível.
- Developer pode definir ou substituir senhas diretamente, inclusive quando o usuário ainda dependeria de link de senha.
- Alterações de perfil e visibilidade são sincronizadas automaticamente em sessões desktop e mobile.
- Importação Developer com prévia de colunas, seleção de campos, criação de itens novos e atualização de itens existentes.
- Página de reservas de camas genérica com 24 camas iniciais editáveis, inclusão de novas camas, disponibilidade, ocupação, limpeza, manutenção, formulário segmentado em `/form`, identificação do responsável pela alteração, histórico operacional recolhível e códigos internos de reserva.
- O monitoramento de reservas permite campos operacionais e sensíveis configuráveis. Restrinja a coleta ao necessário, oculte campos por perfil em Acessos e siga as políticas de proteção de dados da organização.
- Lembretes de estoque crítico (`EstoqueAtual <= EstoqueMinimo`), editáveis pelo admin.
- Envio de lembrete por Power Automate HTTP quando configurado.
- Fallback por SMTP quando `Smtp__Host` estiver configurado.
- Sem Power Automate e sem SMTP, alertas ficam registrados em log local dentro de `App_Data`.
- Tema claro/escuro com preferência salva no navegador.
- Camada visual Plasma UI integrada via ESM, com superfícies liquid-glass em WebGL e fallback CSS para navegadores sem suporte; a biblioteca é pré-1.0 e depende de conexão para carregar o módulo visual.
- Navegação interna com documentação e releases em português.
- Revisão de textos visíveis em português e correção do provisionamento seguro do Developer.

## Próxima Atualização

- Criar tela Developer editável para configurar campos do sistema e campos personalizados por nível de acesso.

## Validação

```bash
dotnet test
```
