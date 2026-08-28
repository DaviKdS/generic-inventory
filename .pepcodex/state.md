# PEP-Codex State

Objetivo: Aplicacao generica de controle de estoque em pasta separada.
Stack: ASP.NET 8, SPA estatica em wwwroot, SQLite/EF Core, xUnit.
Status: Codigo copiado para `_daviks`, renomeado para GenericInventory e neutralizado.
Ultima mudanca: Seed legado desligado; configs sem dados externos; UI sem logo, favicon, imagens, CDN ou icones.
Arquivos-chave: Program.cs, Data/, Products/, Movements/, Employees/, Reminders/, Auth/, wwwroot/.
Validacao: `dotnet test GenericInventory.sln --no-restore` passou 13/13; `node --check wwwroot/app.js` passou.
Bloqueios: Nenhum local. Publicacao depende de GitHub CLI autenticado com permissao no owner DaviKdS.
Proximo: Publicar o repositorio no GitHub.
Decisoes: App nasce com banco vazio; admin bootstrap usa `luizds979@gmail.com`; segredos ficam fora do codigo.
