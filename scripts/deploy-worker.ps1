param(
    [Parameter(Mandatory = $true)]
    [string]$CaminhoPublicacao,

    [string]$NomeServico = "EmailSpamFilter",
    [string]$NomeExibicao = "Email Spam Filter",
    [string]$DiretorioInstalacao = "C:\Services\EmailSpamFilter",
    [string]$CaminhoPublicacaoTray = "",
    [string]$DiretorioInstalacaoTray = "C:\Services\EmailSpamFilterTray",
    [string]$NomeTarefaTray = "EmailSpamFilter.Tray",
    [string]$UsuarioTray = ([System.Security.Principal.WindowsIdentity]::GetCurrent().Name),
    [string]$CaminhoSaudeTray = "",
    [switch]$ConfigurarTrayAutoStart,
    [switch]$IniciarTrayAgora
)

$ErrorActionPreference = "Stop"

function Testar-Administrador {
    $identidade = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identidade)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Escapar-ValorArgumento {
    param([string]$Valor)
    return "'" + ($Valor -replace "'", "''") + "'"
}

function Reiniciar-ComoAdministrador {
    param([hashtable]$ParametrosAtuais)

    $argumentos = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", (Escapar-ValorArgumento -Valor $PSCommandPath)
    )

    foreach ($par in $ParametrosAtuais.GetEnumerator()) {
        if ($par.Value -is [System.Management.Automation.SwitchParameter]) {
            if ($par.Value.IsPresent) {
                $argumentos += "-$($par.Key)"
            }
            continue
        }

        if ($null -ne $par.Value) {
            $argumentos += "-$($par.Key)"
            $argumentos += (Escapar-ValorArgumento -Valor ([string]$par.Value))
        }
    }

    $linhaArgumentos = $argumentos -join " "
    Start-Process -FilePath "powershell.exe" -Verb RunAs -ArgumentList $linhaArgumentos | Out-Null
}

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

function Configurar-TrayAutoStart {
    param(
        [string]$NomeServico,
        [string]$CaminhoPublicacaoTray,
        [string]$DiretorioInstalacaoTray,
        [string]$NomeTarefaTray,
        [string]$UsuarioTray,
        [string]$CaminhoSaudeTray,
        [bool]$IniciarTrayAgora
    )

    $scriptTray = Join-Path $PSScriptRoot "configurar-tray-autostart.ps1"

    if (-not (Test-Path $scriptTray)) {
        throw "Script de configuracao do Tray nao encontrado: '$scriptTray'."
    }

    $parametrosTray = @{
        CaminhoPublicacaoTray = $CaminhoPublicacaoTray
        DiretorioInstalacaoTray = $DiretorioInstalacaoTray
        NomeTarefa = $NomeTarefaTray
        NomeServico = $NomeServico
        Usuario = $UsuarioTray
    }

    if (-not [string]::IsNullOrWhiteSpace($CaminhoSaudeTray)) {
        $parametrosTray.CaminhoSaude = $CaminhoSaudeTray
    }

    if ($IniciarTrayAgora) {
        $parametrosTray.IniciarAgora = $true
    }

    & $scriptTray @parametrosTray
}

if (-not (Test-Path $CaminhoPublicacao)) {
    throw "Diretorio de publicacao nao encontrado: '$CaminhoPublicacao'."
}

if (-not (Testar-Administrador)) {
    Escrever-Log "Sessao sem privilegios administrativos. Solicitando elevacao UAC..."
    Reiniciar-ComoAdministrador -ParametrosAtuais $PSBoundParameters
    return
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

$deveConfigurarTray = $ConfigurarTrayAutoStart -or -not [string]::IsNullOrWhiteSpace($CaminhoPublicacaoTray)
if ($deveConfigurarTray) {
    if ([string]::IsNullOrWhiteSpace($CaminhoPublicacaoTray)) {
        throw "Para configurar o Tray, informe -CaminhoPublicacaoTray."
    }

    Escrever-Log "Configurando inicializacao automatica do Tray..."
    Configurar-TrayAutoStart `
        -NomeServico $NomeServico `
        -CaminhoPublicacaoTray $CaminhoPublicacaoTray `
        -DiretorioInstalacaoTray $DiretorioInstalacaoTray `
        -NomeTarefaTray $NomeTarefaTray `
        -UsuarioTray $UsuarioTray `
        -CaminhoSaudeTray $CaminhoSaudeTray `
        -IniciarTrayAgora $IniciarTrayAgora.IsPresent
    Escrever-Log "Tray configurado com sucesso."
}
