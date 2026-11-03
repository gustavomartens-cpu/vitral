using Vitral.Core.Configuracao;
using Vitral.Core.Controles;

namespace Vitral.Core.Tests;

public sealed class ControlesTests
{
    // A luz noturna desligada como estava na máquina do Gustavo (03/11/2026).
    private static readonly byte[] LuzDesligada =
    [
        0x43, 0x42, 0x01, 0x00, 0x0A, 0x02, 0x01, 0x00, 0x2A, 0x06, 0xFB, 0xD0, 0x91, 0xD5, 0x06, 0x2A, 0x2B, 0x0E, 0x13, 0x43,
        0x42, 0x01, 0x00, 0xD0, 0x0A, 0x02, 0xC6, 0x14, 0xE2, 0xA0, 0xEE, 0xA5, 0xE8, 0xC5, 0xD0, 0xEE, 0x01, 0x00, 0x00, 0x00, 0x00,
    ];

    [Theory]
    [InlineData(4, 0, false, "JBL TUNE230NC TWS", TipoAparelho.Audio)]
    [InlineData(5, 0x10, false, "Teclado", TipoAparelho.Teclado)]
    [InlineData(5, 0x20, false, "Mouse", TipoAparelho.Mouse)]
    [InlineData(5, 0x02, false, "Gamepad", TipoAparelho.Controle)]
    [InlineData(2, 0, false, "S23 FE de Gustavo", TipoAparelho.Celular)]
    [InlineData(0, 0, true, "BT5.0 KB", TipoAparelho.Teclado)]
    [InlineData(0, 0, true, "MX Master Mouse", TipoAparelho.Mouse)]
    [InlineData(0, 0, true, "Xbox Wireless Controller", TipoAparelho.Controle)]
    [InlineData(0, 0, true, "8BitDo Ultimate 2C Wireless", TipoAparelho.Outro)]
    public void Tipo_pela_classe(int maior, int menor, bool le, string nome, TipoAparelho tipo) =>
        Assert.Equal(tipo, RegrasControles.TipoPelaClasse(maior, menor, le, nome));

    [Fact]
    public void Aviso_so_com_teclado_ou_mouse_conectado()
    {
        var fone = new AparelhoBluetooth("1", "JBL", TipoAparelho.Audio, true, true);
        var teclado = new AparelhoBluetooth("2", "BT5.0 KB", TipoAparelho.Teclado, false, false);
        Assert.False(RegrasControles.AvisarTeclado([fone, teclado]));
        Assert.True(RegrasControles.AvisarTeclado([fone, teclado with { Conectado = true }]));
        Assert.True(RegrasControles.AvisarTeclado([new AparelhoBluetooth("3", "Mouse", TipoAparelho.Mouse, true, false)]));
    }

    [Fact]
    public void Aviao_guarda_o_estado_de_cada_radio()
    {
        var guardado = RegrasControles.Aviao(new Dictionary<string, bool> { ["Bluetooth"] = true, ["WiFi"] = false });
        Assert.Equal(new Dictionary<string, bool> { ["Bluetooth"] = true, ["WiFi"] = false }, guardado);
    }

    [Fact]
    public void Aviao_guardado_sobrevive_e_devolve()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "vitral-controles-" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = new ServicoConfig(Path.Combine(raiz, "config.json"));
            var guardado = RegrasControles.Aviao(new Dictionary<string, bool> { ["Bluetooth"] = true, ["WiFi"] = true });
            config.Gravar(config.Ler() with { RadiosAntesDoAviao = guardado });
            var relido = new ServicoConfig(Path.Combine(raiz, "config.json")).Ler().RadiosAntesDoAviao!;
            var ligar = RegrasControles.DevolverAviao(relido, new Dictionary<string, bool> { ["Bluetooth"] = false, ["WiFi"] = false });
            Assert.Equal(["Bluetooth", "WiFi"], ligar.Order());
        }
        finally { if (Directory.Exists(raiz)) Directory.Delete(raiz, true); }
    }

    [Fact]
    public void Desligar_aviao_nao_desliga_o_que_ja_esta_ligado()
    {
        // O Wi-Fi estava desligado antes do modo avião; o usuário ligou o Bluetooth pelo Win+A no meio.
        var ligar = RegrasControles.DevolverAviao(
            new Dictionary<string, bool> { ["Bluetooth"] = true, ["WiFi"] = false },
            new Dictionary<string, bool> { ["Bluetooth"] = true, ["WiFi"] = false });
        Assert.Empty(ligar);
    }

    [Fact]
    public void Luz_noturna_liga_e_desliga()
    {
        Assert.False(RegrasControles.LuzNoturna(LuzDesligada));
        var ligada = RegrasControles.LuzNoturnaComo(LuzDesligada, ligar: true)!;
        Assert.True(RegrasControles.LuzNoturna(ligada));
        Assert.Equal(LuzDesligada.Length + 2, ligada.Length);
        Assert.Equal([0x10, 0x00], ligada[23..25]);
        Assert.Equal(0xFC, ligada[10]); // o carimbo avançou
        var desligada = RegrasControles.LuzNoturnaComo(ligada, ligar: false)!;
        Assert.False(RegrasControles.LuzNoturna(desligada));
        Assert.Equal(LuzDesligada.Length, desligada.Length);
        Assert.Same(LuzDesligada, RegrasControles.LuzNoturnaComo(LuzDesligada, ligar: false)); // já estava: nada muda
    }

    [Fact]
    public void Luz_noturna_formato_desconhecido()
    {
        Assert.Null(RegrasControles.LuzNoturna([0x43, 0x42, 0x01]));
        var outro = (byte[])LuzDesligada.Clone();
        outro[18] = 0x14;
        Assert.Null(RegrasControles.LuzNoturna(outro));
        Assert.Null(RegrasControles.LuzNoturnaComo(outro, ligar: true));
    }
}
