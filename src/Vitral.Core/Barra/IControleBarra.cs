using Vitral.Core.Perfis;

namespace Vitral.Core.Barra;

/// Liga, desliga ou ajusta a barra do topo. O App implementa com as janelas reais; os testes guardam as chamadas.
/// Contrato: nunca lança (a implementação real registra o erro e avisa pela bandeja).
public interface IControleBarra
{
    void Aplicar(BarraTopoPerfil estado);
}
