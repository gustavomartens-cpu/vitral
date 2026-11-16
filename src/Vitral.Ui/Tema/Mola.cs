namespace Vitral.Ui.Tema;

/// Jeito da mola: <paramref name="Resposta"/> é o período, em segundos, da mola sem amortecimento (quanto menor, mais
/// rápida); <paramref name="Amortecimento"/> é a razão de amortecimento (1 = para sem passar do alvo; abaixo de 1, passa
/// um pouco e volta).
public readonly record struct JeitoMola(double Resposta, double Amortecimento)
{
    /// Abrir: repique leve (pesquisa dos notches §3: 0,42 s / 0,8).
    public static readonly JeitoMola Abrir = new(0.42, 0.8);

    /// Fechar: sem repique (0,45 s / 1,0).
    public static readonly JeitoMola Fechar = new(0.45, 1.0);

    /// Frequência angular (rad/s).
    public double Omega => 2 * Math.PI / Math.Max(0.01, Resposta);
}

/// Mola que guarda a velocidade (pesquisa dos notches §4 V1 e §6): valor, velocidade e alvo. Trocar o alvo no meio do
/// caminho muda o rumo sem tranco, porque a velocidade continua. Passo semi-implícito com sub-passos de até 4 ms e o
/// intervalo de cada quadro limitado a 50 ms (um quadro atrasado não faz a mola saltar).
public sealed class Mola(double valor)
{
    public const double PassoMaximo = 0.004, QuadroMaximo = 0.05;

    /// Perto o bastante para parar: distância e velocidade abaixo destes valores (em unidades de tela, px e px/s).
    public const double FolgaValor = 0.05, FolgaVelocidade = 1;

    public double Valor { get; private set; } = valor;
    public double Velocidade { get; private set; }
    public double Alvo { get; private set; } = valor;
    public JeitoMola Jeito { get; private set; } = JeitoMola.Fechar;

    /// Parada no alvo: o laço de quadros pode se desligar.
    public bool Parada => Valor == Alvo && Velocidade == 0;

    /// Novo alvo, mantendo o valor e a velocidade de agora.
    public void IrPara(double alvo, JeitoMola jeito)
    {
        Alvo = alvo;
        Jeito = jeito;
    }

    /// Vai direto ao valor, parada (movimento desligado, ou o estado inicial).
    public void Pular(double valor)
    {
        Valor = Alvo = valor;
        Velocidade = 0;
    }

    /// Pulso de atenção (V6): um empurrão que leva o valor cerca de <paramref name="amplitude"/> além do alvo e deixa a
    /// mola trazê-lo de volta. A velocidade inicial é a que, com o jeito atual, chega ao pico pedido na conta contínua
    /// (com o passo de 4 ms chega uns 10% abaixo, o que não se vê).
    public void Pulsar(double amplitude) => Velocidade += amplitude * Jeito.Omega * FatorPico(Jeito.Amortecimento);

    /// Para uma mola parada no alvo e empurrada com velocidade v₀, o pico é v₀ / (ω × fator). Com amortecimento ζ &lt; 1 o
    /// pico vem em t* = atan(√(1−ζ²)/ζ) / ωd e vale v₀/ω × e^(−ζωt*); com ζ = 1, v₀ / (ω·e). Acima de 1, perto de e.
    public static double FatorPico(double amortecimento)
    {
        double z = amortecimento;
        if (z >= 1) return Math.E;
        if (z <= 0) return 1;
        double r = Math.Sqrt(1 - z * z);
        return Math.Exp(z / r * Math.Atan(r / z));
    }

    /// Avança <paramref name="segundos"/> (limitado a 50 ms) em sub-passos de até 4 ms. Devolve true enquanto se move;
    /// perto o bastante do alvo, encosta nele e para.
    public bool Avancar(double segundos)
    {
        if (Parada) return false;
        double resta = Math.Clamp(segundos, 0, QuadroMaximo);
        double w = Jeito.Omega, k = w * w, c = 2 * Jeito.Amortecimento * w;
        while (resta > 1e-9)
        {
            double h = Math.Min(PassoMaximo, resta);
            Velocidade += (-k * (Valor - Alvo) - c * Velocidade) * h;
            Valor += Velocidade * h;
            resta -= h;
        }
        if (Math.Abs(Valor - Alvo) < FolgaValor && Math.Abs(Velocidade) < FolgaVelocidade)
        {
            Pular(Alvo);
            return false;
        }
        return true;
    }
}
