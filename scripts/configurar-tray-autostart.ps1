param(
    [Parameter(Mandatory = $true)]
    [string]$CaminhoPublicacaoTray,

    [string]$DiretorioInstalacaoTray = "C:\Services\EmailSpamFilterTray",
    [string]$NomeTarefa = "EmailSpamFilter.Tray",
    [string]$NomeServico = "EmailSpamFilter",
    [string]$CaminhoSaude = "",
    [string]$Usuario = ([System.Security.Principal.WindowsIdentity]::GetCurrent().Name),
    [switch]$ExecutarElevado,
    [switch]$IniciarAgora
)

$ErrorActionPreference = "Stop"

function Escrever-Log {
    param([string]$Mensagem)
    Write-Host "[TRAY] $Mensagem"
}

function Obter-CaminhoExecutavel {
    param(
        [string]$Diretorio,
        [string]$NomeEsperado
    )

    $caminhoEsperado = Join-Path $Diretorio $NomeEsperado
    if (Test-Path $caminhoEsperado) {
        return $caminhoEsperado
    }

    $exeEncontrado = Get-ChildItem -Path $Diretorio -Filter "*.exe" -File | Select-Object -First 1
    if ($null -eq $exeEncontrado) {
        throw "Nenhum executavel .exe encontrado em '$Diretorio'."
    }

    return $exeEncontrado.FullName
}

if (-not (Test-Path $CaminhoPublicacaoTray)) {
    throw "Diretorio de publicacao do tray nao encontrado: '$CaminhoPublicacaoTray'."
}

Escrever-Log "Publicacao origem tray: $CaminhoPublicacaoTray"
Escrever-Log "Diretorio instalacao tray: $DiretorioInstalacaoTray"

$tarefaExistente = Get-ScheduledTask -TaskName $NomeTarefa -ErrorAction SilentlyContinue
if ($null -ne $tarefaExistente) {
    Escrever-Log "Parando tarefa '$NomeTarefa' existente..."
    Stop-ScheduledTask -TaskName $NomeTarefa -ErrorAction SilentlyContinue
}

$processosTray = Get-Process -Name "EmailSpamFilter.Tray" -ErrorAction SilentlyContinue
if ($null -ne $processosTray) {
    Escrever-Log "Encerrando processo(s) do Tray em execucao..."
    $processosTray | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}

New-Item -ItemType Directory -Path $DiretorioInstalacaoTray -Force | Out-Null
Copy-Item -Path (Join-Path $CaminhoPublicacaoTray "*") -Destination $DiretorioInstalacaoTray -Recurse -Force

$caminhoExeTray = Obter-CaminhoExecutavel -Diretorio $DiretorioInstalacaoTray -NomeEsperado "EmailSpamFilter.Tray.exe"

# Define as variaveis de ambiente de forma persistente (escopo do usuario) para que o Tray
# as leia no logon. Assim evitamos o uso de 'cmd.exe', que exibia uma janela de console.
[Environment]::SetEnvironmentVariable("EMAILSPAMFILTER_NOME_SERVICO", $NomeServico, "User")
if (-not [string]::IsNullOrWhiteSpace($CaminhoSaude)) {
    [Environment]::SetEnvironmentVariable("EMAILSPAMFILTER_SAUDE_PATH", $CaminhoSaude, "User")
}

$diretorioTrabalho = Split-Path -Parent $caminhoExeTray
$acao = New-ScheduledTaskAction -Execute $caminhoExeTray -WorkingDirectory $diretorioTrabalho
$gatilho = New-ScheduledTaskTrigger -AtLogOn -User $Usuario
$runLevel = if ($ExecutarElevado.IsPresent) { "Highest" } else { "Limited" }
$principal = New-ScheduledTaskPrincipal -UserId $Usuario -LogonType Interactive -RunLevel $runLevel
$configuracoes = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable

Escrever-Log "Registrando tarefa '$NomeTarefa' para usuario '$Usuario'..."
Register-ScheduledTask `
    -TaskName $NomeTarefa `
    -Action $acao `
    -Trigger $gatilho `
    -Principal $principal `
    -Settings $configuracoes `
    -Description "Inicia indicador de bandeja do EmailSpamFilter no logon" `
    -Force `
    -ErrorAction Stop | Out-Null

if ($null -eq (Get-ScheduledTask -TaskName $NomeTarefa -ErrorAction SilentlyContinue)) {
    throw "Falha ao registrar a tarefa '$NomeTarefa'."
}

Escrever-Log "Tarefa registrada com sucesso. RunLevel: $runLevel"
Escrever-Log "Servico monitorado: $NomeServico"

if ($IniciarAgora.IsPresent) {
    Escrever-Log "Iniciando tarefa agora..."
    Start-ScheduledTask -TaskName $NomeTarefa -ErrorAction Stop
}

Escrever-Log "Configuracao concluida."
