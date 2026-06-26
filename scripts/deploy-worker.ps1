param(
    [Parameter(Mandatory = $true)]
    [string]$CaminhoPublicacao,

    [string]$NomeServico = "EmailSpamFilter",
    [string]$NomeExibicao = "Email Spam Filter",
    [string]$DiretorioInstalacao = "C:\\Services\\EmailSpamFilter"
)

$ErrorActionPreference = "Stop"

function Escrever-Log {
    param([string]$Mensagem)
    Write-Host "[DEPLOY] $Mensagem"
}

function Obter-CaminhoExecutavel {
    param([string]$Diretorio)

    $caminhoExePadrao = Join-Path $Diretorio "EmailSpamFilter.Worker.exe"
    if (Test-Path $caminhoExePadrao) {
        return $caminhoExePadrao
    }

    $exeEncontrado = Get-ChildItem -Path $Diretorio -Filter "*.exe" -File | Select-Object -First 1
    if ($null -eq $exeEncontrado) {
        throw "Nenhum executavel .exe encontrado em '$Diretorio'."
    }

    return $exeEncontrado.FullName
}

if (-not (Test-Path $CaminhoPublicacao)) {
    throw "Diretorio de publicacao nao encontrado: '$CaminhoPublicacao'."
}

Escrever-Log "Publicacao origem: $CaminhoPublicacao"
Escrever-Log "Diretorio instalacao: $DiretorioInstalacao"

New-Item -ItemType Directory -Path $DiretorioInstalacao -Force | Out-Null

$servicoExistente = Get-Service -Name $NomeServico -ErrorAction SilentlyContinue

if ($null -ne $servicoExistente -and $servicoExistente.Status -ne "Stopped") {
    Escrever-Log "Parando servico '$NomeServico'..."
    Stop-Service -Name $NomeServico -Force
    $servicoExistente.WaitForStatus("Stopped", [TimeSpan]::FromSeconds(30))
}

Escrever-Log "Copiando arquivos publicados..."
Copy-Item -Path (Join-Path $CaminhoPublicacao "*") -Destination $DiretorioInstalacao -Recurse -Force

$caminhoExe = Obter-CaminhoExecutavel -Diretorio $DiretorioInstalacao
$binPath = '"' + $caminhoExe + '"'

if ($null -eq $servicoExistente) {
    Escrever-Log "Criando servico '$NomeServico'..."
    New-Service -Name $NomeServico -BinaryPathName $binPath -DisplayName $NomeExibicao -StartupType Automatic | Out-Null
}
else {
    Escrever-Log "Servico '$NomeServico' ja existe. Mantendo configuracao atual."
    Set-Service -Name $NomeServico -StartupType Automatic
}

Escrever-Log "Iniciando servico '$NomeServico'..."
Start-Service -Name $NomeServico

$statusFinal = (Get-Service -Name $NomeServico).Status
Escrever-Log "Deploy concluido. Status final do servico: $statusFinal"
