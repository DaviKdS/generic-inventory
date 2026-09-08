# Como adicionar um domínio fixo

Este projeto está publicado agora por um Quick Tunnel da Cloudflare:

```text
https://pharmaceutical-typing-look-breed.trycloudflare.com
```

Esse endereço é temporário. Para usar um domínio fixo, como `estoque.seudominio.com`, use um Cloudflare Tunnel nomeado.

## O que você precisa

- Um domínio comprado, por exemplo `seudominio.com`.
- O domínio adicionado na Cloudflare.
- O servidor local rodando este projeto em `http://localhost:5045`.
- `cloudflared` instalado na máquina que vai manter o site online.

Neste computador, o `cloudflared` já foi baixado em:

```powershell
C:\Users\davi_\.codex\tools\cloudflared.exe
```

## Passo 1: adicionar o domínio na Cloudflare

1. Acesse o painel da Cloudflare.
2. Clique em **Add a domain** ou **Add site**.
3. Informe seu domínio, por exemplo `seudominio.com`.
4. Escolha o plano desejado.
5. A Cloudflare vai mostrar dois nameservers.
6. No site onde você comprou o domínio, troque os nameservers pelos nameservers da Cloudflare.
7. Aguarde a propagação.

Sem esse passo, a Cloudflare não consegue criar um hostname fixo para o túnel.

## Passo 2: fazer login do cloudflared

No PowerShell:

```powershell
& "C:\Users\davi_\.codex\tools\cloudflared.exe" tunnel login
```

Esse comando abre o navegador. Entre na sua conta Cloudflare e selecione o domínio.

## Passo 3: criar um túnel nomeado

Use um nome simples para o túnel:

```powershell
& "C:\Users\davi_\.codex\tools\cloudflared.exe" tunnel create generic-inventory
```

Guarde o UUID que aparecer. Ele será parecido com:

```text
00000000-0000-0000-0000-000000000000
```

## Passo 4: criar a rota DNS

Exemplo usando o subdomínio `estoque.seudominio.com`:

```powershell
& "C:\Users\davi_\.codex\tools\cloudflared.exe" tunnel route dns generic-inventory estoque.seudominio.com
```

Isso cria um registro CNAME apontando o subdomínio para o túnel.

## Passo 5: criar o arquivo de configuração

Crie a pasta, se ela não existir:

```powershell
New-Item -ItemType Directory -Force "$env:USERPROFILE\.cloudflared"
```

Crie ou edite:

```text
C:\Users\davi_\.cloudflared\config.yml
```

Conteúdo do arquivo:

```yaml
tunnel: generic-inventory
credentials-file: C:\Users\davi_\.cloudflared\SEU-UUID-AQUI.json

ingress:
  - hostname: estoque.seudominio.com
    service: http://localhost:5045
  - service: http_status:404
```

Troque:

- `estoque.seudominio.com` pelo seu domínio/subdomínio real.
- `SEU-UUID-AQUI.json` pelo arquivo de credenciais criado no passo 3.

## Passo 6: rodar o servidor do site

Em um terminal na pasta do projeto:

```powershell
$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
$env:App__PublicBaseUrl = "https://estoque.seudominio.com"
& "$env:USERPROFILE\.dotnet\dotnet.exe" run --no-build --urls "http://0.0.0.0:5045"
```

## Passo 7: rodar o túnel fixo

Em outro terminal:

```powershell
& "C:\Users\davi_\.codex\tools\cloudflared.exe" tunnel run generic-inventory
```

Agora qualquer pessoa pode acessar:

```text
https://estoque.seudominio.com
```

## Opcional: instalar o túnel como serviço do Windows

Para não depender de terminal aberto:

```powershell
& "C:\Users\davi_\.codex\tools\cloudflared.exe" service install
```

Depois, inicie pelo Windows Services ou rode:

```powershell
Start-Service cloudflared
```

## Observações importantes

- O computador precisa ficar ligado para o site continuar online.
- O servidor ASP.NET também precisa estar rodando.
- Para produção real, troque a senha temporária `123`.
- O domínio fixo usa HTTPS pela Cloudflare.
- O Quick Tunnel `trycloudflare.com` pode mudar ou parar; o túnel nomeado é o caminho correto para domínio permanente.

## Fontes oficiais

- Cloudflare Tunnel setup: https://developers.cloudflare.com/tunnel/setup/
- Rotas/DNS do Cloudflare Tunnel: https://developers.cloudflare.com/tunnel/routing/
- Quick Tunnels temporários: https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/do-more-with-tunnels/trycloudflare/
