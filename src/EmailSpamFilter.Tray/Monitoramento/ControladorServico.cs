using System.ComponentModel;
using System.Diagnostics;
using System.ServiceProcess;
using Microsoft.Win32;

namespace EmailSpamFilter.Tray.Monitoramento;

public sealed class ControladorServico
{
    private readonly string _nomeServico;

    public ControladorServico(string nomeServico = "EmailSpamFilter")
    {
        _nomeServico = nomeServico;
    }

    public ServiceControllerStatus? ObterStatus()
    {
        using var servico = LocalizarServico();
        if (servico is null)
            return null;

        try
        {
            return servico.Status;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public void Iniciar()
    {
        ExecutarComFallback(
            acaoDireta: servico =>
            {
                if (servico.Status == ServiceControllerStatus.Stopped)
                    servico.Start();
            },
            comandoElevado: "Start-Service",
            parametrosComandoElevado: $"-Name '{EscaparAspasSimples(_nomeServico)}'");
    }

    public void Parar()
    {
        ExecutarComFallback(
            acaoDireta: servico =>
            {
                if (servico.Status == ServiceControllerStatus.Running)
                    servico.Stop();
            },
            comandoElevado: "Stop-Service",
            parametrosComandoElevado: $"-Name '{EscaparAspasSimples(_nomeServico)}' -Force");
    }

    public void Reiniciar()
    {
        ExecutarComFallback(
            acaoDireta: servico =>
            {
                if (servico.Status == ServiceControllerStatus.Running)
                    servico.Stop();

                servico.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                servico.Start();
            },
            comandoElevado: "Restart-Service",
            parametrosComandoElevado: $"-Name '{EscaparAspasSimples(_nomeServico)}' -Force");
    }

    public string? ObterCaminhoExecutavel()
    {
        const string raizServicos = @"SYSTEM\CurrentControlSet\Services";
        using var chaveServico = Registry.LocalMachine.OpenSubKey($"{raizServicos}\\{_nomeServico}");
        var comando = chaveServico?.GetValue("ImagePath") as string;
        if (string.IsNullOrWhiteSpace(comando))
            return null;

        return ExtrairCaminhoExecutavel(comando);
    }

    private ServiceController? LocalizarServico()
    {
        try
        {
            return ServiceController.GetServices()
                .FirstOrDefault(s => s.ServiceName == _nomeServico);
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    private void ExecutarComFallback(Action<ServiceController> acaoDireta, string comandoElevado, string parametrosComandoElevado)
    {
        using var servico = LocalizarServico();
        if (servico is null)
            return;

        try
        {
            acaoDireta(servico);
        }
        catch (Exception ex) when (DeveTentarElevacao(ex))
        {
            ExecutarComElevacao(comandoElevado, parametrosComandoElevado);
        }
    }

    private static bool DeveTentarElevacao(Exception ex)
    {
        if (ex is UnauthorizedAccessException)
            return true;

        if (ex is InvalidOperationException)
            return true;

        return ex is Win32Exception win32 && (win32.NativeErrorCode == 5 || win32.NativeErrorCode == 740);
    }

    private static void ExecutarComElevacao(string comandoPowerShell, string parametros)
    {
        var script = $"{comandoPowerShell} {parametros}";
        var argumentos = $"-NoProfile -WindowStyle Hidden -Command \"{script}\"";

        using var processo = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = argumentos,
            Verb = "runas",
            UseShellExecute = true
        });

        if (processo is null)
            throw new InvalidOperationException("Falha ao iniciar processo elevado para controlar o serviço.");

        processo.WaitForExit();
        if (processo.ExitCode != 0)
            throw new InvalidOperationException($"Falha ao executar comando elevado. Codigo de saida: {processo.ExitCode}.");
    }

    private static string EscaparAspasSimples(string texto)
    {
        return texto.Replace("'", "''", StringComparison.Ordinal);
    }

    private static string ExtrairCaminhoExecutavel(string comando)
    {
        var texto = comando.Trim();
        if (texto.StartsWith('"'))
        {
            var fim = texto.IndexOf('"', 1);
            if (fim > 1)
                return texto.Substring(1, fim - 1);
        }

        var indiceEspaco = texto.IndexOf(' ');
        if (indiceEspaco > 0)
            return texto[..indiceEspaco];

        return texto;
    }
}
