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
- No ambiente de testes, o bootstrap restaura esse acesso Developer para a senha temporária padrão ao iniciar, corrigindo contas locais antigas com senha divergente.

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
- Developer pode ocultar telas por perfil para `Admin` e `User`, incluindo `Acessos`; telas ocultas saem da navegação do respectivo nível.
- Developer pode definir ou substituir senhas diretamente, inclusive quando o usuário ainda dependeria de link de senha.
- Alterações de perfil e visibilidade são sincronizadas automaticamente em sessões desktop e mobile.
- Importação Developer com prévia de colunas, seleção de campos, criação de itens novos e atualização de itens existentes.
- Página de reservas de quartos genérica com 24 quartos iniciais, mapa visual por área, inclusão de novos quartos, disponibilidade, ocupação, limpeza, manutenção, histórico operacional e códigos internos de reserva.
- O monitoramento de reservas evita dados pessoais: não registre nomes de hóspedes, documentos, telefones, endereços ou dados sensíveis.
- Lembretes de estoque crítico (`EstoqueAtual <= EstoqueMinimo`), editáveis pelo admin.
- Envio de lembrete por Power Automate HTTP quando configurado.
- Fallback por SMTP quando `Smtp__Host` estiver configurado.
- Sem Power Automate e sem SMTP, alertas ficam registrados em log local dentro de `App_Data`.
- Tema claro/escuro com preferência salva no navegador.
- Navegação interna com documentação, releases e suporte básico PT/EN.
- Revisão de textos visíveis em português e correção do login Developer padrão.

## Próxima Atualização

- Criar tela Developer editável para configurar campos do sistema e campos personalizados por nível de acesso.

## Validação

```bash
dotnet test
```
