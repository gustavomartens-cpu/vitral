using Vitral.Core.Cores;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;

namespace Vitral.Core.Nativo;

/// Traduz os ajustes nativos de um perfil em valores do registro do usuário (spec §4.2, plano 1a, Tarefa 3).
public static class AjustesNativos
{
    private const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string Dwm = @"Software\Microsoft\Windows\DWM";
    private const string Accent = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    private const string Desktop = @"Control Panel\Desktop";
    private const string Advanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    /// Alfa que o Windows usa em ColorizationColor nesta máquina.
    private const byte AlfaColorizacao = 0xC4;

    /// Todo valor que o Vitral pode tocar. O retrato fotografa exatamente esta lista.
    public static IReadOnlyList<(string Chave, string Nome, TipoValor Tipo)> Controlados { get; } =
    [
        (Personalize, "SystemUsesLightTheme", TipoValor.Dword),
        (Personalize, "AppsUseLightTheme", TipoValor.Dword),
        (Personalize, "EnableTransparency", TipoValor.Dword),
        (Personalize, "ColorPrevalence", TipoValor.Dword),
        (Dwm, "ColorPrevalence", TipoValor.Dword),
        (Dwm, "AccentColor", TipoValor.Dword),
        (Dwm, "ColorizationColor", TipoValor.Dword),
        (Desktop, "AutoColorization", TipoValor.Dword),
        (Accent, "AccentPalette", TipoValor.Binario),
        (Accent, "AccentColorMenu", TipoValor.Dword),
        (Accent, "StartColorMenu", TipoValor.Dword),
        (Advanced, "TaskbarAl", TipoValor.Dword),
    ];

    public static IReadOnlyList<ValorRegistro> Calcular(AjustesNativosPerfil a)
    {
        var valores = new List<ValorRegistro>();
        void Dword(string chave, string nome, uint dado) => valores.Add(new(chave, nome, TipoValor.Dword, dado));
        static uint Bit(bool b) => b ? 1u : 0u;

        if (a.ModoSistema is { } sistema) Dword(Personalize, "SystemUsesLightTheme", Bit(sistema == Modo.Claro));
        if (a.ModoApps is { } apps) Dword(Personalize, "AppsUseLightTheme", Bit(apps == Modo.Claro));
        if (a.Transparencia is { } transparencia) Dword(Personalize, "EnableTransparency", Bit(transparencia));
        if (a.DestaqueNaBarraEIniciar is { } barra)
            Dword(Personalize, "ColorPrevalence", Bit(barra && a.ModoSistema != Modo.Claro)); // só existe no modo escuro
        if (a.DestaqueNosTitulos is { } titulos) Dword(Dwm, "ColorPrevalence", Bit(titulos));

        if (a.CorDestaque is { } hex)
        {
            var c = Cor.DeHex(hex);
            var paleta = Paleta(c);
            Dword(Dwm, "AccentColor", c.ParaAbgr());
            Dword(Dwm, "ColorizationColor", c.ParaArgb(AlfaColorizacao));
            Dword(Desktop, "AutoColorization", 0); // cor fixa desliga a automática
            valores.Add(new(Accent, "AccentPalette", TipoValor.Binario, paleta));
            Dword(Accent, "AccentColorMenu", Tom(paleta, 3).ParaAbgr());
            Dword(Accent, "StartColorMenu", Tom(paleta, 4).ParaAbgr());
        }
        else if (a.CorDestaqueAutomatica is { } automatica)
        {
            Dword(Desktop, "AutoColorization", Bit(automatica));
        }

        if (a.AlinhamentoBarra is { } alinhamento) Dword(Advanced, "TaskbarAl", Bit(alinhamento == Alinhamento.Centro));
        return valores;
    }

    /// 8 tons de 4 bytes (R,G,B,0xFF): 3 mais claros, a base (tom 4), 3 mais escuros e um último fixo.
    /// A gradação exata do Windows não é documentada; esta é uma aproximação.
    public static byte[] Paleta(Cor baseCor)
    {
        var branco = new Cor(255, 255, 255);
        var preto = new Cor(0, 0, 0);
        Cor[] tons =
        [
            baseCor.Misturar(branco, 0.6), baseCor.Misturar(branco, 0.4), baseCor.Misturar(branco, 0.2),
            baseCor,
            baseCor.Misturar(preto, 0.2), baseCor.Misturar(preto, 0.4), baseCor.Misturar(preto, 0.6),
        ];
        var bytes = new byte[32];
        for (int i = 0; i < tons.Length; i++)
        {
            bytes[i * 4] = tons[i].R; bytes[i * 4 + 1] = tons[i].G; bytes[i * 4 + 2] = tons[i].B; bytes[i * 4 + 3] = 0xFF;
        }
        new byte[] { 0x88, 0x17, 0x98, 0x00 }.CopyTo(bytes, 28); // valor que o Windows grava nesta máquina
        return bytes;
    }

    private static Cor Tom(byte[] paleta, int indice) => new(paleta[indice * 4], paleta[indice * 4 + 1], paleta[indice * 4 + 2]);
}
