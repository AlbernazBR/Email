using EmailSpamFilter.Application.CasosDeUso;
using EmailSpamFilter.Application.Interfaces;
using EmailSpamFilter.Domain.Interfaces;
using EmailSpamFilter.Infrastructure.Configuracao;
using EmailSpamFilter.Infrastructure.Imap;
using EmailSpamFilter.Infrastructure.Persistencia;
using EmailSpamFilter.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// ── Windows Service ──────────────────────────────────────────────────────────
builder.Services.AddWindowsService(opts => opts.ServiceName = "EmailSpamFilter");

// ── Serilog ──────────────────────────────────────────────────────────────────
builder.Services.AddSerilog((_, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(builder.Configuration));

// ── Configurações ─────────────────────────────────────────────────────────────
builder.Services.Configure<ConfiguracoesImap>(
    builder.Configuration.GetSection("ConfiguracoesImap"));

builder.Services.Configure<ConfiguracoesFiltro>(
    builder.Configuration.GetSection("ConfiguracoesFiltro"));

// ── Infraestrutura ────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ProvedorTokenOAuth>();
builder.Services.AddSingleton<FabricaClienteImap>();
builder.Services.AddTransient<ILeitorEmail, LeitorEmailImap>();
builder.Services.AddTransient<IAcoesEmail, AcoesEmailImap>();
builder.Services.AddTransient<IRepositorioBloqueio, RepositorioBloqueioJson>();

// ── Application ───────────────────────────────────────────────────────────────
builder.Services.AddTransient<IProcessarCaixaEntradaCasoDeUso, ProcessarCaixaEntradaCasoDeUso>();

// ── Worker ────────────────────────────────────────────────────────────────────
builder.Services.AddHostedService<TrabalhadorFiltro>();

var host = builder.Build();
await host.RunAsync();
