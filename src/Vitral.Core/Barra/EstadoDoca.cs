using Vitral.Core.Configuracao;
using Vitral.Core.Perfis;

namespace Vitral.Core.Barra;

/// Estado aplicado da doca (config.json) e a troca com a barra do Windows (spec da doca §4).
/// Ao ligar, guarda o "ocultar automaticamente" do Windows e o liga (libera a área da barra); ao desligar, encerrar ou
/// abrir com a doca desligada depois de um travamento, mostra a barra do Windows e devolve o que estava guardado.
public sealed class EstadoDoca(IControleDoca controle, IBarraDoWindows barraWindows, ServicoConfig config)
{
    public BarraBaixoPerfil Atual() => config.Ler().BarraBaixo ?? BarraBaixoPerfil.Desligada;

    /// Nulo é "não mexer".
    public void Aplicar(BarraBaixoPerfil? estado)
    {
        if (estado is null) return;
        bool estava = Atual().Visivel;
        if (estado.Visivel) Ligar(guardar: !estava);
        else if (estava) Desligar();
        else controle.Aplicar(estado);
        config.Gravar(config.Ler() with { BarraBaixo = estado });
    }

    /// Ao abrir o App: liga se estava ligada; senão garante a barra do Windows (e desfaz o que um travamento deixou).
    public void AoIniciar()
    {
        if (Atual().Visivel) { Ligar(guardar: config.Ler().OcultarAutomaticoAntesDaDoca is null); return; }
        barraWindows.Mostrar();
        Restaurar();
    }

    /// Ao fechar o App: a doca some com ele, então a barra do Windows volta; o perfil continua com a doca.
    public void Encerrar()
    {
        if (!Atual().Visivel) return;
        Desligar();
    }

    private void Ligar(bool guardar)
    {
        if (guardar)
        {
            config.Gravar(config.Ler() with { OcultarAutomaticoAntesDaDoca = barraWindows.OcultarAutomatico() ?? false });
            barraWindows.DefinirOcultarAutomatico(true);
        }
        barraWindows.Esconder();
        controle.Aplicar(new BarraBaixoPerfil(true));
    }

    private void Desligar()
    {
        controle.Aplicar(BarraBaixoPerfil.Desligada);
        barraWindows.Mostrar();
        Restaurar();
    }

    private void Restaurar()
    {
        if (config.Ler().OcultarAutomaticoAntesDaDoca is not { } antes) return;
        barraWindows.DefinirOcultarAutomatico(antes);
        config.Gravar(config.Ler() with { OcultarAutomaticoAntesDaDoca = null });
    }
}
