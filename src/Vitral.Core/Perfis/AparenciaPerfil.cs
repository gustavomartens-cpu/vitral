namespace Vitral.Core.Perfis;

public enum TemaAparencia { Automatico, VidroEscuro, PretoSolido, Claro, VidroColorido, Grafite, Cristal }

public enum MaterialParte { Vidro, Solido }

public enum CantosParte { Retos, Suaves, Redondos }

/// O "personalizar" (spec da aparência §3): cada campo nulo vale o que vem de baixo (o tema, depois o geral).
/// <param name="Transparencia">0 (opaco) a 100 (invisível); fora disso é recortada.</param>
/// <param name="CorFundo">"#RRGGBB"; inválida é ignorada.</param>
public sealed record AjustesAparencia(MaterialParte? Material = null, string? CorFundo = null, int? Transparencia = null,
    string? CorDestaque = null, CantosParte? Cantos = null);

/// A aparência da doca, do notch e das ilhas (com os painéis): o tema, o personalizar geral e, por parte, o "diferenciar".
public sealed record AparenciaPerfil(TemaAparencia Tema, AjustesAparencia? Geral = null,
    AjustesAparencia? Doca = null, AjustesAparencia? Notch = null, AjustesAparencia? Ilhas = null);
