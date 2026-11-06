using Vitral.Core.Registro;

namespace Vitral.Core.Configuracao;

/// A tarefa agendada que abre o Vitral ao entrar no Windows (Agendador de Tarefas), separada para teste.
public interface ITarefaDeLogin
{
    bool Existe();

    /// Cria ou substitui. Falso se o Windows recusar.
    bool Criar(string caminhoExe, string argumentos);

    void Apagar();
}

/// Iniciar com o Windows por uma tarefa agendada no login, do próprio usuário (sem admin). Até a 0.17 era o valor
/// "Vitral" em HKCU\...\Run, mas o Windows roda essa lista em fila, esperando até 30 s por programa: o Vitral abria
/// quase 2 minutos depois de entrar (06/11). O Run fica só de reserva, se o Windows recusar a tarefa.
public sealed class Inicializacao(IRegistro registro, ITarefaDeLogin tarefa)
{
    private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Nome = "Vitral";
    public const string Argumento = "--inicializacao";

    public bool Ativa() => tarefa.Existe() || !registro.Ler(Run, Nome, TipoValor.Texto).Ausente;

    public void Ativar(string caminhoExe)
    {
        if (tarefa.Criar(caminhoExe, Argumento)) registro.Apagar(Run, Nome);
        else registro.Gravar(new ValorRegistro(Run, Nome, TipoValor.Texto, $"\"{caminhoExe}\" {Argumento}"));
    }

    public void Desativar()
    {
        tarefa.Apagar();
        registro.Apagar(Run, Nome);
    }

    /// Quem ligou antes da tarefa tem o valor no Run: passa para a tarefa, com o mesmo exe que o Run abria (não o
    /// deste processo, que pode ser um build de teste).
    public void Migrar()
    {
        if (tarefa.Existe() || registro.Ler(Run, Nome, TipoValor.Texto).Dado is not string comando) return;
        if (CaminhoDoComando(comando) is { } exe) Ativar(exe);
    }

    private static string? CaminhoDoComando(string comando)
    {
        comando = comando.Trim();
        if (!comando.StartsWith('"')) return comando.Split(' ')[0] is { Length: > 0 } s ? s : null;
        int fim = comando.IndexOf('"', 1);
        return fim > 1 ? comando[1..fim] : null;
    }
}
