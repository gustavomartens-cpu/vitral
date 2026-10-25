using Vitral.Core.Registro;

namespace Vitral.Core.Configuracao;

/// Iniciar com o Windows pelo valor "Vitral" em HKCU\...\Run (sem admin).
public sealed class Inicializacao(IRegistro registro)
{
    private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Nome = "Vitral";
    public const string Argumento = "--inicializacao";

    public bool Ativa() => !registro.Ler(Run, Nome, TipoValor.Texto).Ausente;

    public void Ativar(string caminhoExe) =>
        registro.Gravar(new ValorRegistro(Run, Nome, TipoValor.Texto, $"\"{caminhoExe}\" {Argumento}"));

    public void Desativar() => registro.Apagar(Run, Nome);
}
