# Link web fixo sem depender do PC

O link `trycloudflare.com` e temporario: ele depende do computador ligado. Para ter um link fixo e global, hospede o projeto em nuvem.

## Opcao recomendada: Azure App Service Free

Use quando quiser manter um app ASP.NET Core com URL fixa do tipo:

```text
https://nome-do-app.azurewebsites.net
```

Passos:

1. Crie uma conta gratuita em `https://azure.microsoft.com/free/`.
2. No portal Azure, crie um recurso `App Service`.
3. Escolha runtime `.NET 8`.
4. Escolha plano `Free F1`, quando disponivel.
5. Em `Deployment Center`, conecte o GitHub ao repositorio `DaviKdS/generic-inventory`.
6. Defina a variavel de ambiente:

```text
App__PublicBaseUrl=https://nome-do-app.azurewebsites.net
```

7. Depois do primeiro deploy, acesse a URL fixa gerada pelo Azure.

## Opcao simples com Docker: Render

O projeto tambem recebeu `Dockerfile` e `render.yaml`, entao pode ser publicado no Render com URL fixa do tipo:

```text
https://generic-inventory.onrender.com
```

Passos:

1. Crie conta em `https://render.com/`.
2. Escolha `New > Blueprint` ou `New > Web Service`.
3. Conecte o repositorio do GitHub.
4. Use o `render.yaml` do projeto ou selecione ambiente Docker.
5. Defina `App__PublicBaseUrl` com a URL final do Render.

Observacao: hospedagens gratuitas podem hibernar por inatividade e podem ter armazenamento limitado. Para dados reais, configure backup ou banco persistente antes de usar em producao.

## O que foi preparado no projeto

- `Program.cs` agora respeita a variavel `PORT` exigida por hospedagens cloud.
- `Dockerfile` publica o app em .NET 8.
- `render.yaml` facilita deploy no Render.
- `.dockerignore` evita enviar banco local, App_Data e arquivos de build para a imagem.
