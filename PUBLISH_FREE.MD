# PUBLISH_FREE

## Objetivo

Publicar o projeto `generic-inventory` no Microsoft Azure usando exclusivamente o App Service Free `F1`, com CI/CD via GitHub Actions e proteções para impedir que os workflows do repositório promovam o App Service para um SKU pago.

> Importante: em uma assinatura Azure Pay-As-You-Go não existe um botão universal que transforme o Azure inteiro em `R$ 0` com hard cap. Este plano trava **a infraestrutura criada por estes workflows** em App Service `F1/Free` e recomenda reduzir a permissão do GitHub após o provisionamento. Outros recursos criados manualmente na assinatura continuam podendo gerar cobrança.

---

## Arquitetura FREE

```text
GitHub: DaviKdS/generic-inventory
        │
        ├── provision-free.yml  (manual)
        │       │
        │       ▼
        │   infra/main.bicep
        │       │
        │       ├── App Service Plan F1/Free
        │       └── Web App Linux .NET 8
        │
        └── deploy-free.yml
                │
                ├── restore
                ├── build
                ├── test
                ├── valida F1/Free
                └── deploy
```

O provisionamento automático **não cria Azure SQL, Storage Account, Application Insights, Log Analytics, Container Registry ou qualquer outro recurso opcional**.

---

## Custo-base deste plano

| Recurso | Configuração | Custo esperado |
|---|---|---:|
| Resource Group | Azure Resource Manager | R$ 0 |
| App Service Plan | F1 / Free | R$ 0 |
| Web App | dentro do F1 | R$ 0 |
| GitHub Actions | repositório público / runner padrão | R$ 0 |
| OIDC | GitHub + Microsoft Entra | R$ 0 |
| HTTPS `azurewebsites.net` | incluído | R$ 0 |
| Azure SQL | **não criado neste fluxo** | R$ 0 |
| Application Insights | **não criado** | R$ 0 |
| Storage Account | **não criado** | R$ 0 |
| **Total da infraestrutura criada pelo plano** | | **R$ 0/mês dentro dos limites F1** |

---

## Limites do App Service F1

- infraestrutura compartilhada;
- aproximadamente 60 minutos de CPU por dia;
- aproximadamente 1 GB de RAM;
- aproximadamente 1 GB de armazenamento;
- sem SLA de produção;
- sem `Always On`;
- sujeito a cold start;
- indicado para dev/test/homologação/demonstração;
- não indicado para produção crítica 24/7.

---

# 1. Arquivos usados

## `infra/main.bicep`

O SKU está fixado no código:

```bicep
var requiredSkuName = 'F1'
var requiredSkuTier = 'Free'
```

Não existe parâmetro `sku` no template.

Isso é intencional: quem executar o workflow não consegue informar `B1`, `B2`, `P1v3` etc. por parâmetro.

Recursos criados:

```text
Microsoft.Web/serverfarms
└── F1 / Free

Microsoft.Web/sites
└── Linux / .NET 8
```

---

## `.github/workflows/provision-free.yml`

Executado **somente manualmente** com `workflow_dispatch`.

Ele:

1. autentica no Azure via OIDC;
2. exige que o Resource Group já exista;
3. valida que o Bicep contém o lock `F1`;
4. procura tokens conhecidos de SKUs pagos no template e bloqueia se encontrar;
5. executa `az deployment group what-if`;
6. cria/atualiza apenas App Service Plan + Web App;
7. valida após o provisionamento que o plano é exatamente `F1`.

O workflow **não cria o Resource Group**, para evitar conceder ao GitHub permissão de criação em toda a assinatura.

---

## `.github/workflows/deploy-free.yml`

Executado em `push` para `main` ou manualmente.

Antes do deploy ele valida:

```text
Resource Group correto
        ↓
Web App correto
        ↓
App Service Plan correto
        ↓
SKU == F1
        ↓
Tier == Free
        ↓
Deploy permitido
```

Se detectar qualquer plano diferente, o deploy falha.

---

# 2. Parâmetros do GitHub

## Secrets

Criar em:

```text
GitHub
→ DaviKdS/generic-inventory
→ Settings
→ Secrets and variables
→ Actions
→ Secrets
```

Criar:

```text
AZURE_CLIENT_ID
AZURE_TENANT_ID
AZURE_SUBSCRIPTION_ID
```

## Variables

Na mesma área, em `Variables`, criar:

```text
AZURE_RESOURCE_GROUP=rg-generic-inventory-free
AZURE_LOCATION=brazilsouth
AZURE_APP_SERVICE_PLAN_NAME=asp-generic-inventory-free
AZURE_WEBAPP_NAME=<nome-global-unico>
```

Exemplo de nome global para o Web App:

```text
generic-inventory-davikds
```

O nome precisa estar disponível globalmente no Azure App Service.

---

# 3. Bootstrap do Azure — única configuração manual obrigatória

## Passo 1 — Criar apenas o Resource Group

No Azure Portal:

```text
Resource groups
→ Create
```

Usar:

```text
Resource group: rg-generic-inventory-free
Region: Brazil South
```

Criar Resource Group não adiciona compute pago por si só.

**Não crie App Service manualmente.** O workflow fará isso usando o Bicep F1-only.

---

# 4. Criar identidade OIDC GitHub → Azure

A autenticação recomendada é OIDC, sem senha permanente.

No Microsoft Entra ID:

```text
App registrations
→ New registration
```

Nome sugerido:

```text
github-generic-inventory-free
```

Depois anotar:

```text
Application (client) ID  → AZURE_CLIENT_ID
Directory (tenant) ID    → AZURE_TENANT_ID
```

Obter também o Subscription ID:

```text
Azure Portal
→ Subscriptions
→ sua subscription
→ Subscription ID
```

Esse valor será:

```text
AZURE_SUBSCRIPTION_ID
```

---

# 5. Criar a Federated Credential

Na App Registration:

```text
Certificates & secrets
→ Federated credentials
→ Add credential
→ GitHub Actions deploying Azure resources
```

Configurar:

```text
Organization: DaviKdS
Repository: generic-inventory
Entity type: Branch
Branch: main
```

O subject esperado será equivalente a:

```text
repo:DaviKdS/generic-inventory:ref:refs/heads/main
```

Nenhum `client secret` é necessário.

---

# 6. Permissão temporária para o PRIMEIRO provisionamento

Para criar App Service Plan + Web App dentro do Resource Group, a identidade precisa temporariamente de permissão de escrita no Resource Group.

No Resource Group:

```text
rg-generic-inventory-free
→ Access control (IAM)
→ Add role assignment
```

Atribuir temporariamente:

```text
Role: Contributor
Scope: somente rg-generic-inventory-free
Member: github-generic-inventory-free
```

**Não atribua Contributor na subscription inteira.**

O escopo deve terminar em:

```text
/resourceGroups/rg-generic-inventory-free
```

---

# 7. Executar o provisionamento

Depois que o PR estiver mergeado na `main`:

```text
GitHub
→ generic-inventory
→ Actions
→ Provision Azure FREE infrastructure
→ Run workflow
```

O workflow fará `what-if` e depois implantará:

```text
App Service Plan: F1 / Free
Web App: Linux / .NET 8
```

Ao final deve aparecer:

```text
Provisioned SKU: F1 (Free)
FREE infrastructure verified successfully.
```

---

# 8. LOCKDOWN após o primeiro provisionamento

Esta é a etapa mais importante para reduzir o risco financeiro.

Depois que o Web App existir:

## Remover a permissão temporária

No Resource Group:

```text
Access control (IAM)
→ Role assignments
```

Remover da identidade GitHub:

```text
Contributor
```

no escopo do Resource Group.

## Conceder apenas permissão de deploy no Web App

Abrir:

```text
App Service
→ generic-inventory...
→ Access control (IAM)
```

Adicionar:

```text
Role: Website Contributor
Member: github-generic-inventory-free
Scope: somente o Web App
```

Resultado:

```text
ANTES DO BOOTSTRAP
GitHub OIDC
└── Contributor somente no Resource Group

DEPOIS DO BOOTSTRAP
GitHub OIDC
└── Website Contributor somente no Web App
```

Com isso, o workflow normal de deploy continua funcionando, mas a identidade usada pelo GitHub deixa de ter permissão ampla para alterar o Resource Group.

> Se precisar executar `provision-free.yml` novamente no futuro, será necessário conceder novamente, temporariamente, a permissão necessária ao Resource Group.

---

# 9. Proteções contra custo já implementadas no GitHub

## IaC

```text
SKU não é parâmetro
SKU = F1 hardcoded
```

## Provision workflow

```text
workflow_dispatch somente
        ↓
Resource Group deve existir
        ↓
Bicep deve conter F1
        ↓
what-if
        ↓
deploy
        ↓
validação pós-deploy F1
```

## Deploy workflow

```text
nome do plano esperado
+
SKU F1
+
Tier Free
```

Qualquer divergência interrompe o deploy.

---

# 10. O que NÃO está sendo criado

Para manter o primeiro deploy com superfície de custo mínima, o plano FREE não provisiona automaticamente:

```text
Azure SQL Database
Storage Account
Application Insights
Log Analytics
Container Registry
Key Vault
Virtual Network
Private Endpoint
Power Automate
Microsoft 365
```

Esses itens devem ser avaliados separadamente antes de serem adicionados.

---

# 11. Banco de dados inicial

O projeto atualmente pode continuar utilizando SQLite para o primeiro teste de publicação.

Isso evita criar um serviço de banco Azure antes de confirmar a oferta gratuita disponível na sua subscription.

Depois, se o Azure Portal confirmar explicitamente a **Azure SQL Database Free Offer**, a migração pode ser feita separadamente.

Não permitir que um workflow selecione automaticamente um tier SQL pago.

---

# 12. Budget e alertas

Criar em:

```text
Azure Portal
→ Cost Management
→ Budgets
```

Sugestão inicial:

```text
Budget: R$ 10 ou R$ 50
Alertas: 50%, 75%, 90%, 100%
```

Budget é alerta, não hard cap.

Se sua modalidade de subscription oferecer **Spending Limit**, manter o Spending Limit habilitado fornece uma barreira adicional. Nem toda subscription Azure oferece esse recurso.

---

# 13. Como conferir se continua gratuito

No Azure Portal:

```text
App Service Plan
→ Overview / Scale up
```

Confirmar:

```text
SKU: F1
Tier: Free
```

Também conferir:

```text
Cost Management
→ Cost analysis
→ Resource group
→ rg-generic-inventory-free
```

O App Service criado por este plano deve permanecer no F1.

---

# 14. O que significa "travado no gratuito"

O repositório ficará travado tecnicamente desta forma:

```text
main.bicep
└── aceita somente F1

provision-free.yml
└── provisiona e verifica somente F1

deploy-free.yml
└── recusa qualquer plano que não seja F1/Free

OIDC após bootstrap
└── Website Contributor apenas no Web App
```

Isso impede que **estes workflows** promovam o App Service para B1/B2/Premium.

Não é possível afirmar `R$ 0 absoluto` para toda uma assinatura Pay-As-You-Go, pois recursos externos ao projeto, alterações manuais, impostos, marketplace ou outros serviços da subscription podem gerar cobrança.

---

# 15. Critério para sair do FREE

Migrar apenas de forma deliberada quando houver necessidade de:

- disponibilidade contínua;
- `Always On`;
- maior CPU/RAM;
- serviço background confiável;
- SLA;
- produção com uso contínuo.

Próximo nível documentado:

```text
PUBLISH_BASIC_B1.MD
```

---

## Uso recomendado

**DEV / TEST / HOMOLOGAÇÃO / DEMONSTRAÇÃO**
