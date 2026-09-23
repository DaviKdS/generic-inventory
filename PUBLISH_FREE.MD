# PUBLISH_FREE

## Objetivo
Publicar o projeto `generic-inventory` no Microsoft Azure com o menor custo possível, priorizando desenvolvimento, testes, homologação e demonstração.

## Arquitetura
```text
GitHub
  │
  ├─ GitHub Actions
  ├─ OIDC
  │
  ▼
Azure App Service F1
  │
  ▼
Azure SQL Database Free
```

## Custo estimado
| Recurso | Plano | Custo |
|---|---|---:|
| Azure App Service | F1 Free | R$ 0/mês |
| Azure SQL Database | Free Offer | R$ 0 dentro da cota |
| GitHub Actions | Repositório público | R$ 0 |
| OIDC | Microsoft Entra + GitHub | R$ 0 |
| HTTPS padrão Azure | Incluído | R$ 0 |
| Domínio `azurewebsites.net` | Incluído | R$ 0 |
| Cost Management | Incluído | R$ 0 |
| **Total estimado** |  | **R$ 0/mês** |

## Limites principais
### App Service F1
- Infraestrutura compartilhada.
- Aproximadamente 60 minutos de CPU por dia.
- 1 GB de RAM.
- 1 GB de armazenamento.
- Sem SLA de produção.
- Sem `Always On`.
- A aplicação pode sofrer cold start.
- Não indicado para serviços background críticos.
- Não indicado para produção 24/7.

### Azure SQL Free
- Até 100.000 vCore-seconds por mês.
- Até 32 GB de armazenamento.
- Até 32 GB de backup.
- Pode ser configurado para pausar ao atingir a cota gratuita.

## Vantagens
- Custo zero.
- Ideal para validar a arquitetura.
- Permite testar CI/CD completo.
- Permite abandonar SQLite sem aumentar o custo.
- HTTPS incluído.
- GitHub Actions pode continuar gratuito em repositório público.
- Excelente para protótipo, homologação e demonstração.

## Desvantagens
- Sem disponibilidade garantida.
- Sem `Always On`.
- Cold start.
- Recursos de CPU e memória muito limitados.
- Não recomendado para produção.
- Processos background podem ser interrompidos.

## Banco recomendado
Evitar manter SQLite como banco principal no Azure quando já for possível utilizar Azure SQL Free.

```text
Microsoft.EntityFrameworkCore.Sqlite
        ↓
Microsoft.EntityFrameworkCore.SqlServer
```

Connection string no Azure App Service:
```text
ConnectionStrings__Default
```

## Pipeline CI/CD
```text
git push / merge em main
        ↓
checkout
        ↓
dotnet restore
        ↓
dotnet build
        ↓
dotnet test
        ↓
dotnet publish
        ↓
Azure Login OIDC
        ↓
Deploy App Service
```

Arquivo recomendado:
```text
.github/workflows/deploy-free.yml
```

Secrets:
```text
AZURE_CLIENT_ID
AZURE_TENANT_ID
AZURE_SUBSCRIPTION_ID
```

Variáveis do repositório:
```text
AZURE_WEBAPP_NAME
AZURE_RESOURCE_GROUP
```

## Guard de custo do workflow
O workflow incluído neste plano:

- nunca cria App Service;
- nunca cria App Service Plan;
- nunca faz upgrade de SKU;
- nunca cria Azure SQL;
- falha se o Web App não existir;
- falha se o plano do App Service não for exatamente `F1`.

Isso evita que o próprio pipeline provisione ou promova infraestrutura paga.

## Azure SQL: proteção de custo
Selecionar:
```text
Auto-pause database until next month
```
quando a cota gratuita for atingida.

## Budget recomendado
```text
R$ 50/mês
```
Alertas: `50%`, `75%`, `90%`, `100%`.

> Budget é mecanismo de alerta; não deve ser tratado como bloqueio automático de cobrança.

## Critério para sair do FREE
Migrar quando houver necessidade de disponibilidade contínua, `Always On`, mais CPU/RAM, serviços background confiáveis ou entrada em produção.

Próximo nível: `PUBLISH_BASIC_B1.MD`.

## Uso recomendado
**DEV / TEST / HOMOLOGAÇÃO / DEMONSTRAÇÃO**
