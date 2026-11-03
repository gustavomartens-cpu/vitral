namespace Vitral.Core.Controles;

/// As regras do painel de controles que não dependem do Windows (spec dos controles §3 e §4).
public static class RegrasControles
{
    /// O tipo pela classe Bluetooth (clássico: classe maior e os 6 bits da menor) ou, no LE, que não traz classe, pelo
    /// nome.
    public static TipoAparelho TipoPelaClasse(int classeMaior, int classeMenor, bool le, string nome)
    {
        if (le) return PeloNome(nome);
        return classeMaior switch
        {
            4 => TipoAparelho.Audio,
            2 => TipoAparelho.Celular,
            5 when (classeMenor & 0x10) != 0 => TipoAparelho.Teclado,
            5 when (classeMenor & 0x20) != 0 => TipoAparelho.Mouse,
            5 when (classeMenor & 0x0F) is 1 or 2 => TipoAparelho.Controle, // joystick, gamepad
            _ => PeloNome(nome), // classe sem categoria (o 8BitDo): o nome ainda pode dizer
        };
    }

    private static TipoAparelho PeloNome(string nome)
    {
        if (Contem(nome, "keyboard", "teclado") || nome.Split(' ', '-', '_').Any(p => p.Equals("KB", StringComparison.OrdinalIgnoreCase)))
            return TipoAparelho.Teclado;
        if (Contem(nome, "mouse")) return TipoAparelho.Mouse;
        if (Contem(nome, "controller", "gamepad", "controle", "8bitdo", "xbox")) return TipoAparelho.Controle;
        return TipoAparelho.Outro;
    }

    /// A lista do painel: o mesmo aparelho visto duas vezes (clássico e LE, ou o LE repetido) vira um, pelo endereço
    /// Bluetooth; aparelhos diferentes com o mesmo nome continuam separados. O primeiro da lista (o clássico, que vem
    /// antes) dá o id, o nome e o tipo, se conhecido; conectado e áudio valem se qualquer um dos lados tiver. Conectados
    /// primeiro.
    public static IReadOnlyList<AparelhoBluetooth> Juntar(IEnumerable<(ulong Endereco, AparelhoBluetooth Aparelho)> aparelhos) =>
        aparelhos.GroupBy(a => a.Endereco).Select(g =>
            {
                var lados = g.Select(a => a.Aparelho).ToList();
                var tipo = lados.Select(a => a.Tipo).FirstOrDefault(t => t != TipoAparelho.Outro, TipoAparelho.Outro);
                return lados[0] with { Tipo = tipo, Conectado = lados.Any(a => a.Conectado), TemAudio = lados.Any(a => a.TemAudio) };
            })
            .OrderByDescending(a => a.Conectado).ThenBy(a => a.Nome, StringComparer.CurrentCultureIgnoreCase).ThenBy(a => a.Id).ToList();

    /// O id de um monitor externo: a saída de vídeo do Windows ("\\.\DISPLAY2") e a posição do monitor dentro dela. Não
    /// muda quando outro monitor é desligado (a posição geral na lista mudava).
    public static string IdTelaExterna(string saida, int indice) => $"ddc:{saida}:{indice}";

    private static bool Contem(string nome, params string[] partes) =>
        partes.Any(p => nome.Contains(p, StringComparison.OrdinalIgnoreCase));

    /// Desligar o Bluetooth derruba o teclado e o mouse Bluetooth: o botão avisa quando há um conectado.
    public static bool AvisarTeclado(IEnumerable<AparelhoBluetooth> aparelhos) =>
        aparelhos.Any(a => a.Conectado && a.Tipo is TipoAparelho.Teclado or TipoAparelho.Mouse);

    /// Modo avião ligado: o estado de cada rádio, para devolver depois (guardado no config).
    public static IReadOnlyDictionary<string, bool> Aviao(IReadOnlyDictionary<string, bool> radiosAgora) =>
        new Dictionary<string, bool>(radiosAgora);

    /// Modo avião desligado: os rádios a religar. Só os que estavam ligados antes e estão desligados agora; nunca desliga
    /// nada (o usuário pode ter ligado um pelo Win+A no meio).
    public static IReadOnlyList<string> DevolverAviao(IReadOnlyDictionary<string, bool> guardado, IReadOnlyDictionary<string, bool> agora) =>
        guardado.Where(r => r.Value && agora.TryGetValue(r.Key, out bool ligado) && !ligado).Select(r => r.Key).ToList();

    /// A luz noturna pelo blob do Windows (spec §4.4): byte 18 = 0x15 ligada, 0x13 desligada; outro formato = nulo.
    public static bool? LuzNoturna(byte[] blob) =>
        blob.Length < 25 ? null : blob[18] switch { 0x15 => true, 0x13 => false, _ => null };

    /// O blob com a luz noturna ligada ou desligada; nulo se o formato é desconhecido (o Vitral não grava nada).
    public static byte[]? LuzNoturnaComo(byte[] blob, bool ligar)
    {
        if (LuzNoturna(blob) is not { } ligada) return null;
        if (ligada == ligar) return blob;
        var d = blob.ToList();
        if (ligar) { d[18] = 0x15; d.InsertRange(23, [0x10, 0x00]); }
        else { d[18] = 0x13; d.RemoveRange(23, 2); }
        for (int i = 10; i < 15; i++) // o carimbo de tempo avança, senão o Windows ignora a mudança
            if (d[i] != 0xFF) { d[i]++; break; }
        return d.ToArray();
    }
}
