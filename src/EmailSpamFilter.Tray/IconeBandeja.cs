using System.Diagnostics;
using System.ComponentModel;
using EmailSpamFilter.Tray.Monitoramento;

namespace EmailSpamFilter.Tray;

public sealed class IconeBandeja : ApplicationContext
{
    private readonly NotifyIcon _icone;
    private readonly System.Windows.Forms.Timer _temporizador;
    private readonly ControladorServico _controladorServico;
    private LeitorSaudeJson _leitorSaude;
    private readonly AvaliadorEstado _avaliadorEstado;
    private string _caminhoSaudeAtual;

    public IconeBandeja()
    {
        var nomeServico = ObterVariavelOuPadrao("EMAILSPAMFILTER_NOME_SERVICO", "EmailSpamFilter");
        _controladorServico = new ControladorServico(nomeServico);
        _avaliadorEstado = new AvaliadorEstado();
        _caminhoSaudeAtual = ResolverCaminhoSaude();
        _leitorSaude = new LeitorSaudeJson(_caminhoSaudeAtual);

        _icone = new NotifyIcon
        {
            Visible = true,
            ContextMenuStrip = CriarMenu(),
            Text = "EmailSpamFilter: inicializando"
        };

        _temporizador = new System.Windows.Forms.Timer { Interval = 5000 };
        _temporizador.Tick += (_, _) => AtualizarEstado();
        _temporizador.Start();

        AtualizarEstado();
    }

    private void AtualizarEstado()
    {
        AtualizarLeitorSaudeSeNecessario();

        var statusServico = _controladorServico.ObterStatus();
        var saude = _leitorSaude.Ler();
        var estado = _avaliadorEstado.Avaliar(statusServico, saude);

        _icone.Icon = ObterIconePorEstado(estado);
        _icone.Text = LimitarTooltip(MontarTooltip(estado, saude?.FalhasConsecutivas ?? 0));
    }

    private void AtualizarLeitorSaudeSeNecessario()
    {
        var caminhoNovo = ResolverCaminhoSaude();
        if (string.Equals(caminhoNovo, _caminhoSaudeAtual, StringComparison.OrdinalIgnoreCase))
            return;

        _caminhoSaudeAtual = caminhoNovo;
        _leitorSaude = new LeitorSaudeJson(_caminhoSaudeAtual);
    }

    private string ResolverCaminhoSaude()
    {
        var caminhoPorVariavel = Environment.GetEnvironmentVariable("EMAILSPAMFILTER_SAUDE_PATH");
        if (!string.IsNullOrWhiteSpace(caminhoPorVariavel))
            return caminhoPorVariavel;

        var caminhoExecutavelServico = _controladorServico.ObterCaminhoExecutavel();
        if (!string.IsNullOrWhiteSpace(caminhoExecutavelServico))
        {
            var diretorioServico = Path.GetDirectoryName(caminhoExecutavelServico);
            if (!string.IsNullOrWhiteSpace(diretorioServico))
                return Path.Combine(diretorioServico, "saude.json");
        }

        return Path.Combine(AppContext.BaseDirectory, "saude.json");
    }

    private static string ObterVariavelOuPadrao(string nome, string valorPadrao)
    {
        var valor = Environment.GetEnvironmentVariable(nome);
        return string.IsNullOrWhiteSpace(valor) ? valorPadrao : valor;
    }

    private ContextMenuStrip CriarMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Iniciar serviço", null, (_, _) => ExecutarAcaoServico(_controladorServico.Iniciar));
        menu.Items.Add("Parar serviço", null, (_, _) => ExecutarAcaoServico(_controladorServico.Parar));
        menu.Items.Add("Reiniciar serviço", null, (_, _) => ExecutarAcaoServico(_controladorServico.Reiniciar));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Abrir pasta de logs", null, (_, _) => AbrirPastaLogs());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => EncerrarAplicacao());
        return menu;
    }

    private void ExecutarAcaoServico(Action acao)
    {
        try
        {
            acao();
            AtualizarEstado();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            _icone.ShowBalloonTip(
                3000,
                "EmailSpamFilter",
                "Acao cancelada. Para controlar o servico, confirme a solicitacao de permissao do Windows.",
                ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _icone.ShowBalloonTip(
                3000,
                "EmailSpamFilter",
                $"Falha ao executar ação no serviço: {ex.Message}",
                ToolTipIcon.Warning);
        }
    }

    private static Icon ObterIconePorEstado(EstadoMonitoramento estado)
    {
        return estado switch
        {
            EstadoMonitoramento.Saudavel => SystemIcons.Information,
            EstadoMonitoramento.Atencao => SystemIcons.Warning,
            EstadoMonitoramento.Critico => SystemIcons.Error,
            _ => SystemIcons.Application
        };
    }

    private static string MontarTooltip(EstadoMonitoramento estado, int falhasConsecutivas)
    {
        return estado switch
        {
            EstadoMonitoramento.Saudavel => "EmailSpamFilter: saudavel",
            EstadoMonitoramento.Atencao => "EmailSpamFilter: atencao",
            EstadoMonitoramento.Critico => $"EmailSpamFilter: critico ({falhasConsecutivas} falhas)",
            _ => "EmailSpamFilter: servico parado"
        };
    }

    private static string LimitarTooltip(string texto)
    {
        const int limite = 63;
        return texto.Length <= limite ? texto : texto[..limite];
    }

    private static void AbrirPastaLogs()
    {
        var caminhoLogs = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(caminhoLogs);

        var info = new ProcessStartInfo
        {
            FileName = caminhoLogs,
            UseShellExecute = true
        };

        Process.Start(info);
    }

    private void EncerrarAplicacao()
    {
        _temporizador.Stop();
        _icone.Visible = false;
        _icone.Dispose();
        _temporizador.Dispose();
        ExitThread();
    }
}
