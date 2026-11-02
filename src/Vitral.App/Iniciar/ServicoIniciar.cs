using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Windows.Threading;
using Vitral.App.Tema;
using Vitral.Core.Iniciar;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;
using Vitral.Core.Servicos;
using Vitral.Core.Windows;

namespace Vitral.App.Iniciar;

/// O Iniciar com o visual da doca (spec do Iniciar §4.2): instala a DLL numa pasta que o Iniciar (isolado) consegue
/// ler, grava o arquivo de cores, vigia o processo do Iniciar e conecta. O host da DLL é o próprio Vitral: se ele sair
/// ou cair, o Iniciar volta ao visual do Windows sozinho.
internal sealed class ServicoIniciar : IControleIniciar, IDisposable
{
    private static readonly Guid Clsid = new("9C3E7A14-2B6D-4F1E-8A53-61D0C47B2E95");
    private static readonly SecurityIdentifier PacotesDeApp = new("S-1-15-2-1"); // ALL APPLICATION PACKAGES

    private readonly IRegistro _registro;
    private readonly INotificador _notificador;
    private readonly string _raiz = Path.Combine(Pastas.Raiz, "iniciar");
    private readonly string _cores;
    private readonly string? _dll;
    private readonly DispatcherTimer _vigia = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly CancellationTokenSource _fim = new();
    private ModoIniciar _modo = ModoIniciar.Windows;
    private int? _pidConectado;
    private bool _conectando;

    /// O motivo do visual atual; nulo = modo Windows (a linha de estado some).
    public MotivoIniciar? Estado { get; private set; }

    public event Action<MotivoIniciar?>? EstadoMudou;

    public ServicoIniciar(IRegistro registro, INotificador notificador)
    {
        _registro = registro;
        _notificador = notificador;
        _cores = Path.Combine(_raiz, "cores", "cores.json");
        _dll = Instalar();
        _vigia.Tick += (_, _) => Conferir();
        ServicoAparencia.Instancia.Mudou += GravarCores;
    }

    public void Aplicar(ModoIniciar modo)
    {
        _modo = modo;
        GravarCores();
        if (modo == ModoIniciar.Vitral) { _vigia.Start(); Conferir(); }
        else { _vigia.Stop(); Publicar(null); }
    }

    /// A reserva (spec §4.3): a opção nativa "cor de destaque no Iniciar e na barra".
    public void UsarDestaqueDoWindows()
    {
        try
        {
            foreach (var v in AjustesNativos.Calcular(new AjustesNativosPerfil(null, null, null, null, null, true, null, null)))
                _registro.Gravar(v);
            _notificador.AvisarMudancaDeTema();
        }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("Iniciar.Destaque", e); }
    }

    /// A DLL do pacote vai para iniciar\&lt;versão-hash&gt;: o Iniciar segura o arquivo carregado, então cada versão tem a
    /// sua pasta, e as antigas saem quando ninguém mais as usa. Nulo se a DLL não veio no pacote.
    private string? Instalar()
    {
        try
        {
            string origem = Path.Combine(AppContext.BaseDirectory, "Iniciar", "VitralIniciar.dll");
            if (!File.Exists(origem)) return null;
            string versao = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(origem)))[..12];
            string pasta = Path.Combine(_raiz, versao);
            string destino = Path.Combine(pasta, "VitralIniciar.dll");
            Directory.CreateDirectory(pasta);
            if (!File.Exists(destino)) File.Copy(origem, destino);
            Permitir(pasta, FileSystemRights.ReadAndExecute);
            string logs = Path.Combine(Path.GetDirectoryName(_cores)!, "logs");
            Directory.CreateDirectory(logs);
            Permitir(Path.GetDirectoryName(_cores)!, FileSystemRights.Modify);
            foreach (var antiga in RegrasIniciar.PastasAntigas(Directory.GetDirectories(_raiz).Where(p => !p.EndsWith("cores", StringComparison.OrdinalIgnoreCase)), versao))
                try { Directory.Delete(antiga, recursive: true); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // o Iniciar ainda a usa
            return destino;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Barra.LogBarra.Erro("Iniciar.Instalar", e);
            return null;
        }
    }

    private static void Permitir(string pasta, FileSystemRights direitos)
    {
        var dir = new DirectoryInfo(pasta);
        var acl = dir.GetAccessControl();
        acl.AddAccessRule(new FileSystemAccessRule(PacotesDeApp, direitos,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        dir.SetAccessControl(acl);
    }

    /// Gravado de uma vez (temporário + troca): a DLL nunca lê um arquivo pela metade.
    private void GravarCores()
    {
        try
        {
            string json = ValoresIniciar.De(ServicoAparencia.Instancia.Valores(ParteAparencia.Doca)).ParaJson(_modo == ModoIniciar.Vitral);
            if (File.Exists(_cores) && File.ReadAllText(_cores) == json) return;
            Directory.CreateDirectory(Path.GetDirectoryName(_cores)!);
            string temporario = _cores + ".tmp";
            File.WriteAllText(temporario, json);
            File.Move(temporario, _cores, overwrite: true);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("Iniciar.Cores", e); }
    }

    private void Conferir()
    {
        if (_modo != ModoIniciar.Vitral || _conectando) return;
        if (_dll is null) { Publicar(MotivoIniciar.SemComponente); return; }
        int? pid = PidDe("StartMenuExperienceHost");
        bool windhawk = PidDe("windhawk") is not null;
        if (windhawk && pid != _pidConectado) { Publicar(MotivoIniciar.Windhawk); return; }
        if (_pidConectado is { } conectado && pid == conectado)
        {
            if (LerLog(conectado).Contains("SEM_FUNDO")) Publicar(MotivoIniciar.WindowsMudou);
            return;
        }
        if (!RegrasIniciar.DeveConectar(_modo, pid, _pidConectado, windhawk)) return;
        _conectando = true;
        uint alvo = (uint)pid!.Value;
        string dados = $"hostpid={Environment.ProcessId};cores={_cores}";
        Task.Run(() =>
        {
            int antes = LerLog((int)alvo).Length; // o log é por processo: o de uma conexão anterior não conta
            int hr = ConexaoXaml.Conectar(alvo, _dll, Clsid, dados, _fim.Token);
            // Aceito pelo Windows não basta: a DLL pode recusar (vaga de outra conexão). Vale o "ADVISE" novo no log dela.
            bool entrou() { string log = LerLog((int)alvo); return log.Length > antes && log[antes..].Contains("ADVISE|hr=0"); }
            for (int i = 0; i < 25 && !entrou(); i++) Thread.Sleep(200);
            return entrou() ? 0 : hr < 0 ? hr : unchecked((int)0x800700B7); // ERROR_ALREADY_EXISTS
        }).ContinueWith(t =>
        {
            _conectando = false;
            int hr = t.IsCompletedSuccessfully ? t.Result : -1;
            if (hr < 0)
            {
                Barra.LogBarra.Info($"Iniciar: conexão recusada (0x{hr:X8})");
                Publicar(MotivoIniciar.Recusou);
                _pidConectado = (int)alvo; // não insiste no mesmo processo; um Iniciar novo tenta de novo
                return;
            }
            _pidConectado = (int)alvo;
            Publicar(MotivoIniciar.Nenhum);
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private string LerLog(int pid)
    {
        string log = Path.Combine(Path.GetDirectoryName(_cores)!, "logs", $"iniciar-{pid}.log");
        try
        {
            if (!File.Exists(log)) return "";
            using var fs = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return new StreamReader(fs).ReadToEnd();
        }
        catch (IOException) { return ""; }
    }

    private static int? PidDe(string nome)
    {
        var processos = Process.GetProcessesByName(nome);
        try { return processos.FirstOrDefault()?.Id; }
        finally { foreach (var p in processos) p.Dispose(); }
    }

    /// Uma linha de log por mudança de motivo, não a cada 5 s.
    private void Publicar(MotivoIniciar? motivo)
    {
        if (motivo == Estado) return;
        Estado = motivo;
        if (motivo is { } m && m != MotivoIniciar.Nenhum) Barra.LogBarra.Info(RegrasIniciar.Texto(m));
        EstadoMudou?.Invoke(motivo);
    }

    public void Dispose()
    {
        _vigia.Stop();
        _fim.Cancel();
        ServicoAparencia.Instancia.Mudou -= GravarCores;
    }
}
