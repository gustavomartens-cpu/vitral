using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using Vitral.Core.Configuracao;

namespace Vitral.App;

/// A tarefa "Vitral" no Agendador de Tarefas, pelo schtasks: abre no login do usuário atual, sem esperar a fila do Run,
/// sem limite de tempo e também na bateria. Não precisa de admin (testado em 06/11).
internal sealed class TarefaDeLoginWindows : ITarefaDeLogin
{
    private const string Nome = "Vitral";

    public bool Existe() => Schtasks("/Query", "/TN", Nome) == 0;

    public bool Criar(string caminhoExe, string argumentos)
    {
        string arquivo = Path.Combine(Path.GetTempPath(), "vitral-tarefa-login.xml");
        try
        {
            File.WriteAllText(arquivo, Xml(caminhoExe, argumentos), System.Text.Encoding.Unicode);
            int saida = Schtasks("/Create", "/TN", Nome, "/XML", arquivo, "/F");
            if (saida != 0) Barra.LogBarra.Info($"Tarefa de login não criada (schtasks {saida}); fica o Run");
            return saida == 0;
        }
        finally
        {
            try { File.Delete(arquivo); } catch (IOException) { /* sobra no Temp, sem efeito */ }
        }
    }

    public void Apagar()
    {
        if (Existe()) Schtasks("/Delete", "/TN", Nome, "/F");
    }

    private static string Xml(string caminhoExe, string argumentos)
    {
        string usuario = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name);
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo><Description>Abre o Vitral ao entrar no Windows.</Description></RegistrationInfo>
              <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>{usuario}</UserId></LogonTrigger></Triggers>
              <Principals><Principal id="Author"><UserId>{usuario}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>5</Priority>
              </Settings>
              <Actions Context="Author"><Exec><Command>{SecurityElement.Escape(caminhoExe)}</Command><Arguments>{SecurityElement.Escape(argumentos)}</Arguments></Exec></Actions>
            </Task>
            """;
    }

    /// Código de saída do schtasks (-1 se nem abriu).
    private static int Schtasks(params string[] argumentos)
    {
        try
        {
            var info = new ProcessStartInfo("schtasks.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var a in argumentos) info.ArgumentList.Add(a);
            using var p = Process.Start(info)!;
            p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            if (!p.WaitForExit(15_000)) return -1;
            return p.ExitCode;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Barra.LogBarra.Erro("TarefaDeLogin", e);
            return -1;
        }
    }
}
