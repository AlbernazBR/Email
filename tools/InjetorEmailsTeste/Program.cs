// Utilitário de teste: injeta 3 emails diretamente na pasta Junk via IMAP APPEND
// Reutiliza o cache de token OAuth2 gerado pelo EmailSpamFilter.Worker

using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using MimeKit;

const string email    = "IMAP_EMAIL_REMOVED";
const string clientId = "CLIENT_ID_REMOVED";
const string servidor = "outlook.office365.com";
const int    porta    = 993;

string[] escopos = ["https://outlook.office.com/IMAP.AccessAsUser.All", "offline_access"];

// ── 1. Token OAuth2 via cache em disco (mesmo cache do Worker) ───────────────
var app = PublicClientApplicationBuilder
    .Create(clientId)
    .WithAuthority(AadAuthorityAudience.PersonalMicrosoftAccount)
    .WithRedirectUri("http://localhost")
    .Build();

var storageProps = new StorageCreationPropertiesBuilder(
        "emailspamfilter_token_cache.bin",
        MsalCacheHelper.UserRootDirectory)
    .Build();

var cacheHelper = await MsalCacheHelper.CreateAsync(storageProps);
cacheHelper.RegisterCache(app.UserTokenCache);

var contas = await app.GetAccountsAsync();
var conta = contas.FirstOrDefault(c =>
    c.Username.Equals(email, StringComparison.OrdinalIgnoreCase));

string token;
if (conta is not null)
{
    var resultado = await app.AcquireTokenSilent(escopos, conta).ExecuteAsync();
    token = resultado.AccessToken;
    Console.WriteLine("Token obtido do cache.");
}
else
{
    Console.Error.WriteLine("Token não encontrado no cache. Execute o Worker primeiro para autenticar.");
    return 1;
}

// ── 2. Conectar ao IMAP ───────────────────────────────────────────────────────
using var client = new ImapClient();
await client.ConnectAsync(servidor, porta, true);
await client.AuthenticateAsync(new SaslMechanismOAuth2(email, token));

// ── 3. Abrir pasta Junk ───────────────────────────────────────────────────────
IMailFolder junk = ResolverJunk(client);
await junk.OpenAsync(FolderAccess.ReadWrite);

// ── 4. Criar 3 mensagens de teste com padrões da blacklist ───────────────────
var mensagensTeste = new[]
{
    CriarMensagem(
        de:      "Correios Falso <correios@correios-entrega123.com>",
        para:    email,
        assunto: "[TESTE] Encomenda retida — taxa alfandegária pendente",
        corpo:   "Sua encomenda foi retida. Acesse o link para pagar a taxa.\r\nCódigo: 099123456BR"),

    CriarMensagem(
        de:      "Promoções Banco <mailing.promos@banco-ofertas.com>",
        para:    email,
        assunto: "[TESTE] Oferta exclusiva para você — crédito pré-aprovado!",
        corpo:   "Clique aqui para aceitar sua oferta.\r\nTo unsubscribe, visit: https://example.com/unsub"),

    CriarMensagem(
        de:      "Bulk Sender <bulk@newsletter-tracker.com>",
        para:    email,
        assunto: "[TESTE] Newsletter semanal — não perca as novidades",
        corpo:   "Confira as novidades desta semana.\r\nX-Mailer: BulkMailer Pro 2.0"),
};

// ── 5. Fazer APPEND direto na Junk ───────────────────────────────────────────
foreach (var msg in mensagensTeste)
{
    await junk.AppendAsync(msg, MessageFlags.None);
    Console.WriteLine($"Injetado: {msg.Subject}");
}

await junk.CloseAsync(expunge: false);
await client.DisconnectAsync(true);

Console.WriteLine("\nPronto! 3 emails de teste inseridos na pasta Junk.");
return 0;

// ── Auxiliar ─────────────────────────────────────────────────────────────────
static IMailFolder ResolverJunk(ImapClient client)
{
    // Tenta via atributo especial primeiro
    foreach (var special in new[] { SpecialFolder.Junk, SpecialFolder.Trash })
    {
        try { return client.GetFolder(special); }
        catch { }
    }

    // Tenta por nome (Outlook/Hotmail usa nomes localizados)
    string[] nomes = ["Junk", "Junk Email", "Lixo Eletrônico", "Spam", "Bulk Mail"];

    var subpastas = client.GetFolder(client.PersonalNamespaces[0]).GetSubfolders(false).ToList();

    foreach (var nome in nomes)
    {
        var encontrada = subpastas.FirstOrDefault(f =>
            f.Name.Equals(nome, StringComparison.OrdinalIgnoreCase));
        if (encontrada is not null) return encontrada;
    }

    Console.Error.WriteLine("Pasta Junk não encontrada. Pastas disponíveis:");
    foreach (var f in subpastas) Console.Error.WriteLine($"  - {f.FullName}");
    return client.Inbox;
}

static MimeMessage CriarMensagem(string de, string para, string assunto, string corpo)
{
    var msg = new MimeMessage();
    msg.From.Add(MailboxAddress.Parse(de));
    msg.To.Add(MailboxAddress.Parse(para));
    msg.Subject = assunto;
    msg.Headers.Add("List-Unsubscribe", "<mailto:unsub@example.com>");
    msg.Body = new TextPart("plain") { Text = corpo };
    return msg;
}
