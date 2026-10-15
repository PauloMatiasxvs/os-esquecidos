# Envia o jogo para a pagina do itch.io com o butler (a ferramenta oficial do itch).
# Uso, na raiz do repositorio, depois de criar a pagina "os-esquecidos" no site:
#   .\docs\itch\publicar.ps1 -Usuario seu_usuario
param(
    [Parameter(Mandatory = $true)][string]$Usuario,
    [string]$Versao = "0.2.0",
    [string]$Projeto = "os-esquecidos"
)

$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$zip = Join-Path $raiz "dist\OsEsquecidos-v$Versao-windows.zip"
if (-not (Test-Path $zip)) { throw "Nao achei $zip. Gere o executavel e o zip antes." }

$butler = Get-Command butler -ErrorAction SilentlyContinue
if ($null -eq $butler) {
    # Baixa do endereco oficial do itch (broth.itch.zone) para uma pasta ao lado do jogo.
    $pasta = Join-Path $raiz "dist\butler"
    New-Item -ItemType Directory -Force $pasta | Out-Null
    $arquivo = Join-Path $pasta "butler.zip"
    Write-Host "Baixando o butler do itch.io..."
    Invoke-WebRequest "https://broth.itch.zone/butler/windows-amd64/LATEST/archive/default" -OutFile $arquivo
    Expand-Archive $arquivo -DestinationPath $pasta -Force
    $exe = Join-Path $pasta "butler.exe"
} else {
    $exe = $butler.Source
}

# Na primeira vez abre o navegador para voce autorizar o butler na sua conta.
& $exe login
& $exe push $zip "$Usuario/$($Projeto):windows" --userversion $Versao
& $exe status "$Usuario/$Projeto"
