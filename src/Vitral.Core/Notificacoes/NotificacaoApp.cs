namespace Vitral.Core.Notificacoes;

/// Uma notificação de outro app, lida do Windows (spec das notificações §3).
public sealed record NotificacaoApp(uint Id, string AppId, string NomeApp, string? Titulo, string? Texto, DateTimeOffset Chegou);

/// As notificações de um app, a mais recente primeiro.
public sealed record GrupoNotificacoes(string AppId, string NomeApp, IReadOnlyList<NotificacaoApp> Itens);

/// Se o Vitral consegue ler as notificações (spec §2): sem identidade de pacote ou sem a permissão, não.
public enum EstadoNotificacoes { Ligado, SemIdentidade, SemPermissao }
