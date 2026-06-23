# EmailSpamFilter — Instruções para IA (Claude, Copilot, etc.)

Este arquivo define os padrões obrigatórios de código deste projeto.
Sempre siga estas regras ao gerar ou modificar código.

---

## Objetivo

Windows Service em **.NET 10** que conecta ao Outlook via **IMAP (MailKit)**,
lê emails não lidos, analisa remetente / assunto / headers SMTP contra uma
blacklist em JSON e deleta ou move os spams automaticamente.

**Sem Azure. Sem Power Automate. Sem custo.**

> **Convenção:** todas as classes, propriedades, métodos, interfaces e enums estão nomeados em português.

---

## Estrutura de Pastas

```
EmailSpamFilter.sln
│
├── src/
│   ├── EmailSpamFilter.Domain/          # Núcleo — zero dependências externas
│   ├── EmailSpamFilter.Application/     # Casos de uso — depende só do Domain
│   ├── EmailSpamFilter.Infrastructure/  # IMAP (MailKit) + JSON — depende de Application
│   └── EmailSpamFilter.Worker/          # Host Windows Service — composição raiz
│
└── tests/
    ├── EmailSpamFilter.Domain.Tests/
    ├── EmailSpamFilter.Application.Tests/
    └── EmailSpamFilter.Infrastructure.Tests/
```

---

## Camada Domain (`EmailSpamFilter.Domain`)

> Zero referências a NuGets externos. Apenas lógica de negócio pura.

### Value Objects

| Classe | Responsabilidade |
|---|---|
| `EnderecoEmail` | Encapsula e valida um endereço de e-mail. Lança `ExcecaoDominio` se inválido. |
| `PadraoSpam` | Representa um padrão de texto (string + tipo). Imutável. |
| `ResultadoFiltro` | Resultado da análise: `EhSpam`, `RegraCorrespondente`, `CampoCorrespondente`. |

```csharp
// Classe rica, imutável, com fábrica estática
public sealed class EnderecoEmail
{
    public string Valor { get; }

    private EnderecoEmail(string valor) => Valor = valor;

    public static EnderecoEmail Criar(string bruto)
    {
        if (string.IsNullOrWhiteSpace(bruto) || !bruto.Contains('@'))
            throw new EnderecoEmailInvalidoException(bruto);
        return new(bruto.Trim().ToLowerInvariant());
    }

    public bool Contem(PadraoSpam padrao) => ...
}
```

### Entidades

| Classe | Responsabilidade |
|---|---|
| `MensagemEmail` | Representa o e-mail recebido. Expõe `Remetente`, `Assunto`, `Cabecalhos`, `Corpo`. Método `Analisar(regras)` → `ResultadoFiltro`. |
| `RegraSpam` | Uma regra de detecção: padrão + escopo (`Global`, `ApenasRemetente`, `ApenasCabecalho`). Método `Corresponde(mensagem)` → `bool`. |

```csharp
// Entidade rica com comportamento
public sealed class MensagemEmail
{
    public UniqueId UidImap { get; }
    public EnderecoEmail Remetente { get; }
    public string Assunto { get; }
    public IReadOnlyList<CabecalhoEmail> Cabecalhos { get; }
    public string Corpo { get; }

    // Comportamento no domínio — nenhuma lógica fora desta classe
    public ResultadoFiltro Analisar(IEnumerable<RegraSpam> regras)
    {
        foreach (var regra in regras)
            if (regra.Corresponde(this))
                return ResultadoFiltro.Spam(regra);

        return ResultadoFiltro.Limpo();
    }
}
```

### Aggregate Root

| Classe | Responsabilidade |
|---|---|
| `RegrasBloqueio` | Aggregate root que agrupa `RegraSpam`s. Métodos `AdicionarRegra`, `RemoverRegra`, `ObterRegrasAtivas`. Garante invariantes (sem duplicatas, sem padrão vazio). |

### Interfaces (Ports — DIP)

| Interface | Contrato |
|---|---|
| `ILeitorEmail` | `Task<IReadOnlyList<MensagemEmail>> ObterNaoLidosAsync(int max, CancellationToken)` |
| `IAcoesEmail` | `Task DeletarAsync(MensagemEmail, CancellationToken)` / `Task MoverParaLixoAsync(...)` |
| `IRepositorioBloqueio` | `Task<RegrasBloqueio> CarregarAsync(CancellationToken)` |

### Exceções de Domínio

| Exceção | Quando |
|---|---|
| `ExcecaoDominio` | Base para erros de regra de negócio |
| `EnderecoEmailInvalidoException` | Endereço de e-mail inválido ao criar `EnderecoEmail` |
| `PadraoSpamInvalidoException` | Padrão vazio ou nulo ao criar `RegraSpam` |

---

## Camada Application (`EmailSpamFilter.Application`)

> Depende apenas do Domain. Coordena o fluxo, não contém lógica de negócio.

### Caso de Uso

**`ProcessarCaixaEntradaCasoDeUso`**

```
Entradas:
  - ILeitorEmail            (injetado)
  - IAcoesEmail             (injetado)
  - IRepositorioBloqueio    (injetado)
  - OpcoesProcessamento     (MaximoEmailsPorCiclo, Acao)

Fluxo:
  1. Carrega RegrasBloqueio do repositório
  2. Busca emails não lidos (limitado por MaximoEmailsPorCiclo)
  3. Para cada MensagemEmail → mensagem.Analisar(regras)
  4. Se ResultadoFiltro.EhSpam → chama IAcoesEmail (Deletar ou MoverParaLixo)
  5. Retorna ResultadoProcessamento (Processados, QuantidadeSpam, Erros)
```

### DTOs

| Classe | Uso |
|---|---|
| `OpcoesProcessamento` | Parâmetros do caso de uso (ação, máximo por ciclo) |
| `ResultadoProcessamento` | Resultado da execução (contadores, lista de erros) |

### Interfaces Application-level

| Interface | Uso |
|---|---|
| `IProcessarCaixaEntradaCasoDeUso` | Contrato público do caso de uso (testável via mock) |

---

## Camada Infrastructure (`EmailSpamFilter.Infrastructure`)

> Implementa os ports do Domain. Depende de MailKit e System.Text.Json.

### IMAP (MailKit)

| Classe | Responsabilidade |
|---|---|
| `LeitorEmailImap` | Implementa `ILeitorEmail`. Conecta ao Outlook IMAP, busca UIDs não lidos, mapeia `MimeMessage` → `MensagemEmail`. |
| `AcoesEmailImap` | Implementa `IAcoesEmail`. Deleta (flag + expunge) ou move para Lixo. |
| `FabricaClienteImap` | Cria e autentica `ImapClient`. Isola a criação para facilitar testes. |
| `MapeadorMensagemEmail` | Converte `MimeMessage` (MailKit) → `MensagemEmail` (Domain). |

**Configuração IMAP (`ConfiguracoesImap`):**
```json
{
  "ConfiguracoesImap": {
    "Servidor": "outlook.office365.com",
    "Porta": 993,
    "UsarSsl": true,
    "Email": "",   // definir via user-secrets ou env var
    "Senha": ""    // definir via user-secrets ou env var — NUNCA no appsettings.json
  }
}
```

### Blacklist (JSON)

| Classe | Responsabilidade |
|---|---|
| `RepositorioBloqueioJson` | Implementa `IRepositorioBloqueio`. Lê `bloqueio.json`, desserializa, converte em `RegrasBloqueio`. Recarrega automaticamente se o arquivo mudar (hot-reload). |

**Formato `bloqueio.json`:**
```json
{
  "padroes": [
    "campanhasbradesco",
    "unsubscribe",
    "bulk",
    "mailer",
    "tracking"
  ],
  "padroesPorRemetente": [
    "campanhasbradesco",
    "mailing."
  ],
  "padroesPorCabecalho": [
    "list-unsubscribe",
    "x-mailer",
    "campanhasbradesco"
  ]
}
```

### Configurações

| Classe | Responsabilidade |
|---|---|
| `ConfiguracoesImap` | `Servidor`, `Porta`, `UsarSsl`, `Email`, `Senha` |
| `ConfiguracoesFiltro` | `IntervaloSegundos`, `ArquivoBloqueio`, `Acao` (Deletar/MoverParaLixo), `MaximoEmailsPorCiclo` |

---

## Camada Worker (`EmailSpamFilter.Worker`)

> Ponto de entrada. Composição de dependências. Sem lógica de negócio.

### `TrabalhadorFiltro : BackgroundService`

- Executa `ProcessarCaixaEntradaCasoDeUso` a cada `ConfiguracoesFiltro.IntervaloSegundos`
- Loga resultado de cada ciclo via `ILogger`
- Responde corretamente a `CancellationToken` (parada graciosa do serviço)

### `Program.cs`

- Registra `AddWindowsService()`
- Liga `IOptions<ConfiguracoesImap>` e `IOptions<ConfiguracoesFiltro>` via `appsettings.json`
- Credenciais via **dotnet user-secrets** (dev) / **variáveis de ambiente** (produção)
- Registra Serilog (console + arquivo rotativo)

---

## Testes (`tests/`)

**Stack:** xUnit + FluentAssertions + NSubstitute

### `EmailSpamFilter.Domain.Tests`

| Teste | O que valida |
|---|---|
| `EnderecoEmail_DeveLancarExcecao_QuandoInvalido` | `ExcecaoDominio` para endereço sem `@` |
| `EnderecoEmail_DeveSerIgual_QuandoMesmoValor` | Igualdade por valor (Value Object) |
| `RegraSpam_Corresponde_DeveRetornarVerdadeiro_QuandoPadraoNoRemetente` | Correspondência no campo Remetente |
| `RegraSpam_Corresponde_DeveRetornarFalso_QuandoEscopoErrado` | `ApenasRemetente` não pega em Assunto |
| `MensagemEmail_Analisar_DeveRetornarSpam_QuandoRegraCorresponde` | Lógica de análise do aggregate |
| `RegrasBloqueio_DeveLancarExcecao_QuandoRegraDuplicada` | Invariante do aggregate root |

### `EmailSpamFilter.Application.Tests`

| Teste | O que valida |
|---|---|
| `ProcessarCaixaEntrada_DeveDeletar_QuandoEmailEhSpam` | `IAcoesEmail.DeletarAsync` é chamado |
| `ProcessarCaixaEntrada_NaoDeveDeletar_QuandoEmailEhLimpo` | Nenhuma ação quando não é spam |
| `ProcessarCaixaEntrada_DeveRespeitar_MaximoEmailsPorCiclo` | Limite de emails por ciclo |
| `ProcessarCaixaEntrada_DeveRetornar_ContagensCorretas` | `ResultadoProcessamento.QuantidadeSpam` correto |
| `ProcessarCaixaEntrada_DeveContinuar_QuandoUmEmailFalha` | Resiliência: erro em um email não para o ciclo |

### `EmailSpamFilter.Infrastructure.Tests`

| Teste | O que valida |
|---|---|
| `RepositorioBloqueioJson_DeveCarregar_TodosOsPadroes` | Desserialização correta do JSON |
| `RepositorioBloqueioJson_DeveRecarregar_QuandoArquivoMuda` | Hot-reload da blacklist |
| `RepositorioBloqueioJson_DeveRetornarVazio_QuandoArquivoNaoEncontrado` | Não lança exceção |

---

## Enums

| Enum | Valores |
|---|---|
| `EscopoCorrespondencia` | `Global`, `ApenasRemetente`, `ApenasCabecalho` |
| `AcaoFiltro` | `Deletar`, `MoverParaLixo` |

---

## Segurança

| Prática | Detalhe |
|---|---|
| Credenciais fora do código | `Email` e `Senha` somente via `user-secrets` ou env vars |
| `.gitignore` bloqueia secrets | `appsettings.Development.json`, `secrets.json`, `*.user` ignorados |
| App Password | Recomendado quando 2FA está ativo na conta Microsoft |
| Sem eval / injeção de comando | Blacklist é lida como texto puro, nunca executada |

---

## Tecnologias

| Componente | Pacote / Versão |
|---|---|
| Runtime | .NET 10 |
| IMAP client | MailKit 4.x |
| Windows Service | Microsoft.Extensions.Hosting.WindowsServices 10.x |
| Logging | Serilog (Console + File sinks) |
| Testes | xUnit 2.x + FluentAssertions 7.x + NSubstitute 5.x |

---

## Arquivos a criar

```
EmailSpamFilter.sln

src/EmailSpamFilter.Domain/
  Domain.csproj
  Excecoes/ExcecaoDominio.cs
  Excecoes/EnderecoEmailInvalidoException.cs
  Excecoes/PadraoSpamInvalidoException.cs
  ObjetosDeValor/EnderecoEmail.cs
  ObjetosDeValor/PadraoSpam.cs
  ObjetosDeValor/ResultadoFiltro.cs
  Entidades/RegraSpam.cs
  Entidades/MensagemEmail.cs
  Entidades/CabecalhoEmail.cs
  Agregados/RegrasBloqueio.cs
  Enums/EscopoCorrespondencia.cs
  Enums/AcaoFiltro.cs
  Interfaces/ILeitorEmail.cs
  Interfaces/IAcoesEmail.cs
  Interfaces/IRepositorioBloqueio.cs

src/EmailSpamFilter.Application/
  Application.csproj
  Interfaces/IProcessarCaixaEntradaCasoDeUso.cs
  CasosDeUso/ProcessarCaixaEntradaCasoDeUso.cs
  DTOs/OpcoesProcessamento.cs
  DTOs/ResultadoProcessamento.cs

src/EmailSpamFilter.Infrastructure/
  Infrastructure.csproj
  Imap/LeitorEmailImap.cs
  Imap/AcoesEmailImap.cs
  Imap/FabricaClienteImap.cs
  Imap/MapeadorMensagemEmail.cs
  Persistencia/RepositorioBloqueioJson.cs
  Configuracao/ConfiguracoesImap.cs
  Configuracao/ConfiguracoesFiltro.cs

src/EmailSpamFilter.Worker/
  Worker.csproj
  Program.cs
  TrabalhadorFiltro.cs
  appsettings.json
  appsettings.Development.json  (gitignored — credenciais de dev)
  bloqueio.json

tests/EmailSpamFilter.Domain.Tests/
  Domain.Tests.csproj
  ObjetosDeValor/EnderecoEmailTestes.cs
  ObjetosDeValor/PadraoSpamTestes.cs
  Entidades/RegraSpamTestes.cs
  Entidades/MensagemEmailTestes.cs
  Agregados/RegrasBloqueioTestes.cs

tests/EmailSpamFilter.Application.Tests/
  Application.Tests.csproj
  CasosDeUso/ProcessarCaixaEntradaTestes.cs

tests/EmailSpamFilter.Infrastructure.Tests/
  Infrastructure.Tests.csproj
  Persistencia/RepositorioBloqueioJsonTestes.cs

.gitignore
```

---

## Dependências entre projetos

```
Worker → Application → Domain
Worker → Infrastructure → Application → Domain

Testes → respectiva camada + NSubstitute (sem dependência circular)
```

---

## Como executar (após aprovação)

```powershell
# Restaurar dependências
dotnet restore

# Rodar testes
dotnet test

# Configurar credenciais (nunca no git)
cd src/EmailSpamFilter.Worker
dotnet user-secrets set "ConfiguracoesImap:Email" "seuemail@outlook.com"
dotnet user-secrets set "ConfiguracoesImap:Senha" "sua-app-password"

# Rodar localmente
dotnet run --project src/EmailSpamFilter.Worker

# Instalar como Windows Service
sc create EmailSpamFilter binPath="C:\caminho\EmailSpamFilter.Worker.exe"
sc start EmailSpamFilter
```

---

## Padrões de Código Obrigatórios

### Linguagem e Nomenclatura

- **Idioma do código:** Português (classes, métodos, propriedades, variáveis, testes)
- **Idioma dos comentários:** Português
- **Idioma dos logs:** Português

```csharp
// ✅ Correto
public sealed class ProcessarCaixaEntradaCasoDeUso { }
public async Task<ResultadoProcessamento> ExecutarAsync(...) { }

// ❌ Errado
public sealed class ProcessInboxUseCase { }
public async Task<ProcessingResult> ExecuteAsync(...) { }
```

---

### Exceções de Domínio

Toda exceção de domínio **deve** terminar com o sufixo `Exception`.

```csharp
// ✅ Correto
public class ExcecaoDominioException : Exception { }
public sealed class EnderecoEmailInvalidoException : ExcecaoDominioException { }
public sealed class PadraoSpamInvalidoException : ExcecaoDominioException { }

// ❌ Errado — SonarQube: "Make this class name end with 'Exception'"
public class ExcecaoDominio : Exception { }
```

---

### Loops — Prefira LINQ

Nunca use `foreach` apenas para encontrar o primeiro elemento correspondente.

```csharp
// ✅ Correto — LINQ com FirstOrDefault
var regraAtivada = regras.FirstOrDefault(r => r.Corresponde(this));
if (regraAtivada is not null)
    return ResultadoFiltro.Spam(regraAtivada.Padrao.ToString(), ...);

var impostor = impostoras?.FirstOrDefault(i => i.Corresponde(this));
if (impostor is not null)
    return ResultadoFiltro.Spam($"Impostor:{impostor.Palavra}", "Remetente");

// ❌ Errado — SonarQube: "Loops should be simplified using the 'Where' LINQ method"
foreach (var regra in regras)
{
    if (regra.Corresponde(this))
        return ResultadoFiltro.Spam(regra.Padrao.ToString(), ...);
}
```

---

### Complexidade Cognitiva

Mantenha a complexidade cognitiva de cada método **abaixo de 15**.
Se um método ultrapassa esse limite, extraia métodos privados com nomes descritivos.

```csharp
// ✅ Correto — método público simples, lógica extraída
public async Task<ResultadoProcessamento> ExecutarAsync(OpcoesProcessamento opcoes, CancellationToken ct)
{
    foreach (var mensagem in mensagens)
    {
        try { ClassificarMensagem(mensagem, ..., spamParaDeletar, spamParaMover); }
        catch (Exception ex) { erros.Add(...); }
    }

    if (!opcoes.ModoSimulacao)
        await ExecutarAcoesLoteAsync(spamParaDeletar, spamParaMover, erros, ct);

    return new ResultadoProcessamento { ... };
}

private void ClassificarMensagem(...) { /* lógica de classificação */ }
private async Task ExecutarAcoesLoteAsync(...) { /* lógica de lote */ }

// ❌ Errado — SonarQube: "Refactor this method to reduce its Cognitive Complexity from 25 to 15"
public async Task<ResultadoProcessamento> ExecutarAsync(...)
{
    // 70 linhas com ifs e foreachs aninhados...
}
```

---

### Catch Vazio — Nunca deixe silencioso

Todo bloco `catch` deve ter ação ou comentário justificado.

```csharp
// ✅ Correto — justifica o catch vazio
try { return client.GetFolder(SpecialFolder.Junk); }
catch (NotSupportedException) { /* servidor não suporta SpecialFolder — continua para fallback */ }

// ✅ Correto — registra o erro
catch (Exception ex) { erros.Add($"UID {mensagem.UidImap}: {ex.Message}"); }

// ❌ Errado — SonarQube: "Either remove or fill this block of code"
catch { }
```

---

### Async/Await

Sempre use a versão `async` de métodos de I/O dentro de métodos assíncronos.

```csharp
// ✅ Correto
await Console.Out.WriteLineAsync("mensagem");

// ❌ Errado — SonarQube: "Await WriteLineAsync instead"
Console.WriteLine("mensagem");  // dentro de método async
```

---

### Caminhos e URIs — Sem Hardcode

Nunca coloque caminhos absolutos ou URIs no código-fonte. Use configuração ou parâmetros.

```csharp
// ✅ Correto — vem de configuração
var caminho = _configuracoes.ArquivoBloqueio;

// ❌ Errado — SonarQube: "Refactor your code not to use hardcoded absolute paths or URIs"
var caminho = @"C:\EmailSpamFilter\bloqueio.json";
```

---

### Segurança — Credenciais

Nunca coloque credenciais no código ou em `appsettings.json` versionado.

```csharp
// ✅ Correto — via user-secrets (dev) ou variável de ambiente (prod)
// dotnet user-secrets set "ConfiguracoesImap:Email" "usuario@outlook.com"
// dotnet user-secrets set "ConfiguracoesImap:Senha" "app-password"

// ❌ Proibido
const string email = "usuario@outlook.com";
const string senha = "minha-senha";
```

---

### Expressões Regulares

Sempre defina **timeout** em Regex compilados para evitar ReDoS.
Use `GeneratedRegex` quando possível (.NET 7+).

```csharp
// ✅ Correto — com timeout
var regex = new Regex(padrao, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

// ✅ Melhor ainda — source generator (.NET 7+)
[GeneratedRegex(@"^(?<nome>[^<]+)<(?<email>[^>]+)>$")]
private static partial Regex EmailComNomeRegex();

// ❌ Errado — sem timeout, vulnerável a ReDoS
var regex = new Regex(padrao, RegexOptions.Compiled);
```

---

### Testes

- Stack: **xUnit + FluentAssertions + NSubstitute**
- Nome: `Metodo_DeveResultado_QuandoCondicao`
- Um assert por teste via `.Should()`
- Testes de integração: `[Trait("Category", "Integracao")]`
- CI exclui integração: `--filter "Category!=Integracao"`

```csharp
// ✅ Padrão correto
[Fact]
public void Criar_DeveLancarExcecao_QuandoEmailSemArroba()
{
    var acao = () => EnderecoEmail.Criar("emailsemarroba");
    acao.Should().Throw<EnderecoEmailInvalidoException>();
}

[Fact]
[Trait("Category", "Integracao")]
public async Task Conectar_DeveAutenticar_QuandoCredenciaisCorretas()
{
    // teste real contra servidor IMAP
}
```

---

### CI/CD — GitHub Actions + SonarCloud

O workflow `.github/workflows/sonarcloud.yml` roda a cada `push` em `main`:

1. Build da solution `EmailSpamFilter.slnx`
2. Testes unitários com cobertura OpenCover (via coverlet)
3. Análise SonarCloud — organização: `albernazbr`, projeto: `AlbernazBR_Email`

**Requisito:** secret `SONAR_TOKEN` configurado no repositório GitHub.

Antes de fazer push, valide localmente:

```powershell
dotnet build EmailSpamFilter.slnx
dotnet test --filter "Category!=Integracao"
```

---

### Checklist antes de gerar código

- [ ] Nomes em português?
- [ ] Exceções terminam com `Exception`?
- [ ] Loops de busca usam `FirstOrDefault` em vez de `foreach`?
- [ ] Complexidade cognitiva < 15?
- [ ] Nenhum `catch {}` vazio sem comentário?
- [ ] Sem caminho absoluto ou credencial hardcoded?
- [ ] Regex tem timeout definido?
- [ ] Testes de integração marcados com `[Trait("Category", "Integracao")]`?
