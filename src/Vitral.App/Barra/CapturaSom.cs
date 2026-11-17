using System.Runtime.InteropServices;
using System.Windows.Threading;
using Vitral.Ui.Barra;
using static Vitral.App.Barra.CoreAudio;

namespace Vitral.App.Barra;

/// O som que sai do PC, para as barras do notch fechado (spec do notch fechado §3.2, R3): captura em loopback do
/// aparelho de saída padrão pelo WASAPI (interop própria, sem pacote), mistura os canais em mono, separa janelas de
/// 20 ms e entrega as 5 barras de <see cref="BandasSom"/> na thread da interface, no máximo ~30 vezes por segundo.
/// Uma thread própria lê os pacotes; a troca do aparelho padrão (fone Bluetooth que conecta) é vista pela própria thread,
/// que confere o aparelho a cada segundo e reabre a captura nele. Nada sai da thread como exceção.
internal sealed class CapturaSom(Dispatcher ui) : IDisposable
{
    private const int CompartilhadoLoopback = 0x00020000; // AUDCLNT_STREAMFLAGS_LOOPBACK
    private const uint Silencioso = 0x2;                    // AUDCLNT_BUFFERFLAGS_SILENT
    private const long BufferHns = 1_000_000;               // 100 ms
    private const int IntervaloEnvioMs = 33, ConferirAparelhoMs = 1000, EsperaErroMs = 1000;

    private static readonly Guid IidAudioClient = new("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
    private static readonly Guid IidCaptureClient = new("C8ADBD64-E71E-48A0-A4DE-185C395CD317");
    private static readonly Guid SubtipoFloat = new("00000003-0000-0010-8000-00AA00389B71");

    private readonly BandasSom _bandas = new();
    private Thread? _thread;
    private volatile bool _ligada;
    private int _envioPendente;
    private string? _ultimoErro;

    /// As 5 barras (0 a 1), na thread da interface.
    public event Action<IReadOnlyList<double>>? Bandas;

    public void Ligar()
    {
        if (_ligada) return;
        _ligada = true;
        _thread = new Thread(Rodar) { IsBackground = true, Name = "Vitral.CapturaSom" };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    public void Desligar()
    {
        if (!_ligada) return;
        _ligada = false;
        _thread?.Join(500);
        _thread = null;
        _bandas.Zerar();
    }

    public void Dispose() => Desligar();

    private void Rodar()
    {
        while (_ligada)
        {
            try { CapturarAteTrocar(); }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                // Um log por tipo de falha, não um por tentativa (o aparelho pode ficar sumido por muito tempo).
                if (_ultimoErro != e.Message) { _ultimoErro = e.Message; LogBarra.Erro("CapturaSom", e); }
                Dormir(EsperaErroMs);
            }
        }
    }

    /// Captura no aparelho padrão até ele deixar de ser o padrão, a captura ser desligada ou dar erro.
    private void CapturarAteTrocar()
    {
        var enumerador = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        object? cliente = null, captura = null;
        IMMDevice? aparelho = null;
        try
        {
            Marshal.ThrowExceptionForHR(enumerador.GetDefaultAudioEndpoint(Saida, Multimidia, out aparelho));
            Marshal.ThrowExceptionForHR(aparelho.GetId(out var id));
            var iid = IidAudioClient;
            Marshal.ThrowExceptionForHR(aparelho.Activate(ref iid, ClsctxAll, IntPtr.Zero, out cliente));
            var audio = (IAudioClient)cliente;
            Marshal.ThrowExceptionForHR(audio.GetMixFormat(out var formato));
            int canais, taxa;
            try
            {
                if (!EhFloat32(formato, out canais, out taxa))
                    throw new NotSupportedException("O som do Windows não está em float de 32 bits; as barras ficam paradas.");
                var sessao = Guid.Empty;
                Marshal.ThrowExceptionForHR(audio.Initialize(0, CompartilhadoLoopback, BufferHns, 0, formato, ref sessao));
            }
            finally { Marshal.FreeCoTaskMem(formato); }
            var iidCaptura = IidCaptureClient;
            Marshal.ThrowExceptionForHR(audio.GetService(ref iidCaptura, out captura));
            var leitor = (IAudioCaptureClient)captura;
            Marshal.ThrowExceptionForHR(audio.Start());
            _ultimoErro = null;
            try { Ler(enumerador, id, leitor, canais, taxa); }
            finally { audio.Stop(); }
        }
        finally
        {
            foreach (var c in new[] { captura, cliente, aparelho, (object)enumerador })
                if (c is not null && Marshal.IsComObject(c)) Marshal.ReleaseComObject(c);
        }
    }

    private void Ler(IMMDeviceEnumerator enumerador, string id, IAudioCaptureClient leitor, int canais, int taxa)
    {
        int tamanhoJanela = taxa / 50; // 20 ms
        var janela = new float[tamanhoJanela];
        int cheio = 0;
        long ultimaJanela = Environment.TickCount64, ultimaConferencia = ultimaJanela;
        while (_ligada)
        {
            Thread.Sleep(10);
            Marshal.ThrowExceptionForHR(leitor.GetNextPacketSize(out var pacote));
            while (pacote > 0)
            {
                Marshal.ThrowExceptionForHR(leitor.GetBuffer(out var dados, out var quadros, out var bandeiras, out _, out _));
                try
                {
                    for (int q = 0; q < quadros; q++)
                    {
                        float soma = 0;
                        if ((bandeiras & Silencioso) == 0)
                            for (int c = 0; c < canais; c++)
                                soma += BitConverter.Int32BitsToSingle(Marshal.ReadInt32(dados, (q * canais + c) * 4));
                        janela[cheio++] = soma / canais;
                        if (cheio == tamanhoJanela) { Entregar(janela, taxa); cheio = 0; ultimaJanela = Environment.TickCount64; }
                    }
                }
                finally { leitor.ReleaseBuffer(quadros); }
                Marshal.ThrowExceptionForHR(leitor.GetNextPacketSize(out pacote));
            }
            // Nada tocando: o loopback não manda pacotes; as barras descem como num silêncio.
            var agora = Environment.TickCount64;
            if (agora - ultimaJanela > 40) { Array.Clear(janela); cheio = 0; Entregar(janela, taxa); ultimaJanela = agora; }
            if (agora - ultimaConferencia > ConferirAparelhoMs)
            {
                ultimaConferencia = agora;
                if (enumerador.GetDefaultAudioEndpoint(Saida, Multimidia, out var padrao) != 0) return;
                try { if (padrao.GetId(out var idPadrao) != 0 || idPadrao != id) return; }
                finally { Marshal.ReleaseComObject(padrao); }
            }
        }
    }

    private long _ultimoEnvio;

    private void Entregar(float[] janela, int taxa)
    {
        var valores = _bandas.Processar(janela, taxa);
        var agora = Environment.TickCount64;
        if (agora - _ultimoEnvio < IntervaloEnvioMs || Interlocked.Exchange(ref _envioPendente, 1) == 1) return;
        _ultimoEnvio = agora;
        ui.BeginInvoke(() =>
        {
            Interlocked.Exchange(ref _envioPendente, 0);
            if (_ligada) Bandas?.Invoke(valores);
        });
    }

    private void Dormir(int ms)
    {
        for (int t = 0; t < ms && _ligada; t += 50) Thread.Sleep(50);
    }

    /// WAVEFORMATEX (ou EXTENSIBLE) em float de 32 bits.
    private static bool EhFloat32(IntPtr formato, out int canais, out int taxa)
    {
        ushort tag = (ushort)Marshal.ReadInt16(formato, 0);
        canais = (ushort)Marshal.ReadInt16(formato, 2);
        taxa = Marshal.ReadInt32(formato, 4);
        ushort bits = (ushort)Marshal.ReadInt16(formato, 14);
        if (bits != 32 || canais == 0 || taxa <= 0) return false;
        if (tag == 3) return true; // WAVE_FORMAT_IEEE_FLOAT
        if (tag != 0xFFFE) return false; // WAVE_FORMAT_EXTENSIBLE: o subtipo fica depois dos 24 bytes
        var guid = new byte[16];
        Marshal.Copy(formato + 24, guid, 0, 16);
        return new Guid(guid) == SubtipoFloat;
    }
}

[ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioClient
{
    [PreserveSig] int Initialize(int modo, int bandeiras, long duracaoBuffer, long periodo, IntPtr formato, ref Guid sessao);
    [PreserveSig] int GetBufferSize(out uint quadros);
    [PreserveSig] int GetStreamLatency(out long latencia);
    [PreserveSig] int GetCurrentPadding(out uint quadros);
    [PreserveSig] int IsFormatSupported(int modo, IntPtr formato, out IntPtr parecido);
    [PreserveSig] int GetMixFormat(out IntPtr formato);
    [PreserveSig] int GetDevicePeriod(out long padrao, out long minimo);
    [PreserveSig] int Start();
    [PreserveSig] int Stop();
    [PreserveSig] int Reset();
    [PreserveSig] int SetEventHandle(IntPtr evento);
    [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object servico);
}

[ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioCaptureClient
{
    [PreserveSig] int GetBuffer(out IntPtr dados, out uint quadros, out uint bandeiras, out ulong posicao, out ulong posicaoQpc);
    [PreserveSig] int ReleaseBuffer(uint quadros);
    [PreserveSig] int GetNextPacketSize(out uint quadros);
}
