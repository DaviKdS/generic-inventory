param(
    [int]$Porta = 5045,
    [switch]$Build,
    [switch]$AbrirNavegador
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$PublicServer = Join-Path $ProjectRoot "public_server.py"

if (!(Test-Path -LiteralPath $PublicServer)) {
    throw "Arquivo public_server.py não encontrado em: $ProjectRoot"
}

Set-Location $ProjectRoot

$arguments = @($PublicServer, "--port", $Porta)

if ($Build) {
    $arguments += "--build"
}

if (!$AbrirNavegador) {
    $arguments += "--no-browser"
}

Write-Host "Iniciando servidor público..." -ForegroundColor Cyan
Write-Host "Pasta: $ProjectRoot"
Write-Host "Porta local: $Porta"
Write-Host ""

try {
    & python @arguments
}
catch {
    Write-Host ""
    Write-Host "Não foi possível iniciar o servidor." -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Read-Host "Pressione Enter para sair"
    exit 1
}
