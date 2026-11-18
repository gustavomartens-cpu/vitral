namespace Vitral.Core.Perfis;

public enum FormatoDoca { Flutuante, Corrida }

public enum TamanhoIcones { Pequeno, Medio, Grande }

/// A cor dos ícones dos apps na doca (spec dos ícones em preto e branco, 17/11/2026). Enum para a etapa 2 crescer.
public enum CorIconesDoca { Originais, PretoEBranco }

public enum FormatoNotch { Encaixado, SemOrelhas, Ilha }

public enum TamanhoNotch { Compacto, Normal, Grande }

public enum DisposicaoIlhas { Separadas, BarraUnica }

public enum DistanciaIlhas { ComRespiro, Encostadas }

/// As formas (spec das formas §3): cada campo nulo é "não mexer" e vale a forma de hoje.
public sealed record FormasDoca(FormatoDoca? Formato = null, TamanhoIcones? Icones = null, bool? Ampliacao = null,
    CorIconesDoca? CorIcones = null);

public sealed record FormasNotch(FormatoNotch? Formato = null, TamanhoNotch? Tamanho = null);

public sealed record FormasIlhas(DisposicaoIlhas? Disposicao = null, DistanciaIlhas? Distancia = null);

public sealed record FormasPerfil(FormasDoca? Doca = null, FormasNotch? Notch = null, FormasIlhas? Ilhas = null);
