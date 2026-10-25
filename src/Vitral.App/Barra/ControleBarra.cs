using System.Windows;
using System.Windows.Controls;
using Vitral.Core.Barra;
using Vitral.Core.Perfis;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

/// A barra do topo de verdade: a reserva e as três ilhas. Nunca lança (contrato de <see cref="IControleBarra"/>):
/// erro vai para o log e, se a reserva falhar, as ilhas aparecem mesmo assim com um aviso.
internal sealed class ControleBarra(Action<string> avisar) : IControleBarra
{
    private ReservaTopo? _reserva;
    private JanelaIlha? _midia, _relogio, _status;
    private BarraTopoPerfil _estado = BarraTopoPerfil.Desligada;

    public void Aplicar(BarraTopoPerfil estado)
    {
        try
        {
            _estado = estado;
            if (estado.Visivel) Abrir(estado.IlhasEfetivas);
            else Fechar();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            LogBarra.Erro("ControleBarra.Aplicar", e);
            avisar("A barra do topo não pôde ser ajustada. Detalhes no log.");
        }
    }

    private void Abrir(IlhasBarra ilhas)
    {
        if (_reserva is null)
        {
            _reserva = new ReservaTopo();
            _reserva.Recriar += AoRecriar;
        }
        if (!_reserva.Registrar()) avisar("Não foi possível reservar o espaço da barra do topo.");

        _midia = Ajustar(_midia, ilhas.Midia, "Mídia");
        _relogio = Ajustar(_relogio, ilhas.Relogio, "Relógio");
        _status = Ajustar(_status, ilhas.Status, "Status");
        Posicionar();
    }

    private JanelaIlha? Ajustar(JanelaIlha? janela, bool ligada, string nome)
    {
        if (!ligada)
        {
            janela?.Close();
            return null;
        }
        if (janela is not null) return janela;
        var nova = new JanelaIlha(nome) { Conteudo = new TextBlock { Text = nome, VerticalAlignment = VerticalAlignment.Center } };
        nova.SizeChanged += (_, _) => Posicionar();
        nova.Show();
        return nova;
    }

    /// Recoloca as ilhas no monitor principal atual (DIPs).
    public void Posicionar()
    {
        var monitor = new Retangulo(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
        var (m, r, s) = PosicaoIlhas.Calcular(monitor, _midia?.ActualWidth ?? 0, _relogio?.ActualWidth ?? 0, _status?.ActualWidth ?? 0);
        Colocar(_midia, m);
        Colocar(_relogio, r);
        Colocar(_status, s);
    }

    private static void Colocar(JanelaIlha? janela, Retangulo r)
    {
        if (janela is null) return;
        janela.Left = r.X;
        janela.Top = r.Y;
    }

    private void AoRecriar()
    {
        if (!_estado.Visivel || _reserva is null) return;
        _reserva.Registrar();
        Posicionar();
    }

    /// Fecha as ilhas e libera a reserva (perfil desligou a barra ou o Vitral está saindo).
    public void Fechar()
    {
        _midia?.Close();
        _relogio?.Close();
        _status?.Close();
        _midia = _relogio = _status = null;
        if (_reserva is not null)
        {
            _reserva.Recriar -= AoRecriar;
            _reserva.Dispose();
            _reserva = null;
        }
    }
}
