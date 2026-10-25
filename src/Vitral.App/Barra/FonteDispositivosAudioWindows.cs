using System.Runtime.InteropServices;
using System.Windows.Threading;
using Vitral.Ui.Barra;

namespace Vitral.App.Barra;

/// Saídas de áudio ativas e troca do padrão (spec v2 §5). A troca usa IPolicyConfig, interface não documentada:
/// se ela não estiver disponível, PodeTrocar fica false e o painel esconde a lista. Relê a cada aviso do Windows
/// (dispositivo ligado, desligado ou padrão trocado).
internal sealed class FonteDispositivosAudioWindows : IFonteDispositivosAudio, IDisposable
{
    private readonly Dispatcher _ui;
    private readonly IMMDeviceEnumerator? _enumerador;
    private readonly Aviso? _aviso;
    private readonly IPolicyConfig? _politica;

    public IReadOnlyList<DispositivoAudio> Saidas { get; private set; } = [];
    public bool PodeTrocar => _politica is not null;
    public event Action? Mudou;

    public FonteDispositivosAudioWindows(Dispatcher ui)
    {
        _ui = ui;
        try
        {
            _enumerador = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            _aviso = new Aviso(this);
            _enumerador.RegisterEndpointNotificationCallback(_aviso);
        }
        catch (Exception e) { LogBarra.Erro("Dispositivos.Iniciar", e); }
        try { _politica = (IPolicyConfig)new PolicyConfigClient(); }
        catch (Exception e) { LogBarra.Erro("Dispositivos.PolicyConfig", e); }
        Ler();
    }

    private void Ler()
    {
        var lista = new List<DispositivoAudio>();
        try
        {
            if (_enumerador is null) { Saidas = lista; return; }
            string? padrao = null;
            if (_enumerador.GetDefaultAudioEndpoint(CoreAudio.Saida, CoreAudio.Multimidia, out var d) == 0)
            {
                d.GetId(out padrao);
                Marshal.ReleaseComObject(d);
            }
            Marshal.ThrowExceptionForHR(_enumerador.EnumAudioEndpoints(CoreAudio.Saida, CoreAudio.DispositivoAtivo, out var colecao));
            try
            {
                colecao.GetCount(out uint quantidade);
                for (uint i = 0; i < quantidade; i++)
                {
                    if (colecao.Item(i, out var dispositivo) != 0) continue;
                    try
                    {
                        dispositivo.GetId(out string id);
                        lista.Add(new DispositivoAudio(id, Nome(dispositivo) ?? id, id == padrao));
                    }
                    finally { Marshal.ReleaseComObject(dispositivo); }
                }
            }
            finally { Marshal.ReleaseComObject(colecao); }
        }
        catch (Exception e) { LogBarra.Erro("Dispositivos.Ler", e); }
        Saidas = lista;
    }

    private static string? Nome(IMMDevice dispositivo)
    {
        if (dispositivo.OpenPropertyStore(CoreAudio.SomenteLeitura, out var propriedades) != 0) return null;
        try
        {
            var chave = CoreAudio.NomeAmigavel;
            if (propriedades.GetValue(ref chave, out var valor) != 0) return null;
            try { return valor.Tipo == 31 /* VT_LPWSTR */ ? Marshal.PtrToStringUni(valor.Ponteiro) : null; }
            finally { CoreAudio.PropVariantClear(ref valor); }
        }
        finally { Marshal.ReleaseComObject(propriedades); }
    }

    public void TornarPadrao(string id)
    {
        try
        {
            if (_politica is null) return;
            for (int papel = 0; papel <= 2; papel++) // console, multimídia, comunicações
                Marshal.ThrowExceptionForHR(_politica.SetDefaultEndpoint(id, papel));
        }
        catch (Exception e) { LogBarra.Erro("Dispositivos.TornarPadrao", e); }
    }

    private void AoAvisar() => _ui.BeginInvoke(() => { Ler(); Mudou?.Invoke(); });

    public void Dispose()
    {
        try
        {
            if (_enumerador is not null && _aviso is not null) _enumerador.UnregisterEndpointNotificationCallback(_aviso);
            if (_enumerador is not null) Marshal.ReleaseComObject(_enumerador);
            if (_politica is not null) Marshal.ReleaseComObject(_politica);
        }
        catch (Exception e) { LogBarra.Erro("Dispositivos.Dispose", e); }
    }

    private sealed class Aviso(FonteDispositivosAudioWindows dono) : IMMNotificationClient
    {
        public int OnDeviceStateChanged(IntPtr id, uint estado) { dono.AoAvisar(); return 0; }
        public int OnDeviceAdded(IntPtr id) { dono.AoAvisar(); return 0; }
        public int OnDeviceRemoved(IntPtr id) { dono.AoAvisar(); return 0; }
        public int OnDefaultDeviceChanged(int fluxo, int papel, IntPtr id)
        {
            if (fluxo == CoreAudio.Saida && papel == CoreAudio.Multimidia) dono.AoAvisar();
            return 0;
        }
        public int OnPropertyValueChanged(IntPtr id, IntPtr chave) => 0;
    }
}
