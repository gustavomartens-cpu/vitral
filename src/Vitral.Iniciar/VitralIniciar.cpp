// VitralIniciar.dll: o Menu Iniciar com o visual da doca do Vitral (spec do Iniciar §4.1).
// Carregada dentro do Iniciar (StartMenuExperienceHost) pelo Vitral via InitializeXamlDiagnosticsEx. Acha o fundo do
// Iniciar (Border "AcrylicBorder"), guarda o original, aplica as cores do arquivo de cores e as reaplica quando ele
// muda. Devolve o original com "ligado":false ou quando o Vitral (o host) morre. Nunca derruba o Iniciar: todo
// callback engole e registra os erros.
#include <windows.h>
#include <ocidl.h>
#include <xamlom.h>
#undef GetCurrentTime
#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.h>
#include <winrt/Windows.UI.Core.h>
#include <winrt/Windows.UI.Xaml.h>
#include <winrt/Windows.UI.Xaml.Controls.h>
#include <winrt/Windows.UI.Xaml.Media.h>

#include <atomic>
#include <cstdio>
#include <map>
#include <mutex>
#include <optional>
#include <string>
#include <thread>

namespace wux = winrt::Windows::UI::Xaml;
using winrt::Windows::UI::Color;

// {9C3E7A14-2B6D-4F1E-8A53-61D0C47B2E95}
static constexpr CLSID CLSID_VitralIniciar = { 0x9c3e7a14, 0x2b6d, 0x4f1e, { 0x8a, 0x53, 0x61, 0xd0, 0xc4, 0x7b, 0x2e, 0x95 } };

static HMODULE g_module = nullptr;
static std::wstring g_cores; // caminho do arquivo de cores (dos dados de inicialização)

// ---------- log: <pasta das cores>\logs\iniciar-<pid>.log, até 256 KB ----------

static std::mutex g_logMutex;

static void Log(const std::wstring& linha)
{
    std::lock_guard lock(g_logMutex);
    if (g_cores.empty()) return;
    std::wstring pasta = g_cores.substr(0, g_cores.find_last_of(L'\\')) + L"\\logs";
    std::wstring arquivo = pasta + L"\\iniciar-" + std::to_wstring(GetCurrentProcessId()) + L".log";
    WIN32_FILE_ATTRIBUTE_DATA info{};
    bool grande = GetFileAttributesExW(arquivo.c_str(), GetFileExInfoStandard, &info) && info.nFileSizeLow > 256 * 1024;
    FILE* f = _wfopen(arquivo.c_str(), grande ? L"w, ccs=UTF-8" : L"a, ccs=UTF-8");
    if (!f) return;
    SYSTEMTIME t; GetLocalTime(&t);
    fwprintf(f, L"%02d:%02d:%02d.%03d %ls\n", t.wHour, t.wMinute, t.wSecond, t.wMilliseconds, linha.c_str());
    fclose(f);
}

static std::wstring Parametro(const std::wstring& dados, const std::wstring& nome)
{
    size_t p = dados.find(nome + L"=");
    if (p == std::wstring::npos) return L"";
    p += nome.size() + 1;
    size_t fim = dados.find(L';', p);
    return dados.substr(p, fim == std::wstring::npos ? std::wstring::npos : fim - p);
}

// ---------- arquivo de cores (spec §3): {"ligado":..,"vidro":..,"tinta":"#AARRGGBB","borda":"#AARRGGBB","raio":n} ----------

struct Cores
{
    bool ligado = false;
    bool vidro = true;
    Color tinta{ 0xCC, 0x10, 0x12, 0x18 };
    Color borda{ 0x33, 0xFF, 0xFF, 0xFF };
    double raio = 8;
};

static std::optional<Color> CorDe(const std::string& json, const char* chave)
{
    size_t p = json.find(std::string("\"") + chave + "\":\"#");
    if (p == std::string::npos) return std::nullopt;
    p = json.find('#', p) + 1;
    if (p + 8 > json.size()) return std::nullopt;
    unsigned long v = strtoul(json.substr(p, 8).c_str(), nullptr, 16);
    return Color{ static_cast<uint8_t>(v >> 24), static_cast<uint8_t>(v >> 16), static_cast<uint8_t>(v >> 8), static_cast<uint8_t>(v) };
}

/// Nulo se o arquivo não pôde ser lido ou não parece um arquivo de cores: fica o último visual aplicado.
static std::optional<Cores> LerCores()
{
    std::string json;
    for (int tentativa = 0; tentativa < 5 && json.empty(); ++tentativa) // o Vitral troca o arquivo de uma vez: pode estar no meio
    {
        HANDLE h = CreateFileW(g_cores.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr,
            OPEN_EXISTING, 0, nullptr);
        if (h != INVALID_HANDLE_VALUE)
        {
            char buf[1024]{}; DWORD lidos = 0;
            if (ReadFile(h, buf, sizeof(buf) - 1, &lidos, nullptr)) json.assign(buf, lidos);
            CloseHandle(h);
        }
        if (json.empty()) Sleep(50);
    }
    if (json.find("\"ligado\"") == std::string::npos) return std::nullopt;
    Cores c;
    c.ligado = json.find("\"ligado\":true") != std::string::npos;
    if (!c.ligado) return c;
    c.vidro = json.find("\"vidro\":false") == std::string::npos;
    auto tinta = CorDe(json, "tinta");
    auto borda = CorDe(json, "borda");
    size_t r = json.find("\"raio\":");
    if (!tinta || !borda || r == std::string::npos) return std::nullopt;
    c.tinta = *tinta;
    c.borda = *borda;
    c.raio = strtod(json.c_str() + r + 7, nullptr);
    return c;
}

// ---------- o fundo pintado e o original guardado ----------

struct Fundo
{
    winrt::weak_ref<wux::Controls::Border> borda;
    wux::Media::Brush fundoOriginal{ nullptr };
    wux::Media::Brush bordaOriginal{ nullptr };
    wux::Thickness espessuraOriginal{};
    wux::CornerRadius cantosOriginais{};
};

static std::mutex g_estadoMutex;
static std::map<InstanceHandle, Fundo> g_fundos;
static std::atomic<bool> g_achouFundo{ false };

/// Na thread do Iniciar (dispatcher do elemento).
static void Pintar(const Fundo& f, const std::optional<Cores>& cores)
{
    auto b = f.borda.get();
    if (!b) return;
    if (!cores || !cores->ligado)
    {
        b.Background(f.fundoOriginal);
        b.BorderBrush(f.bordaOriginal);
        b.BorderThickness(f.espessuraOriginal);
        b.CornerRadius(f.cantosOriginais);
        return;
    }
    if (cores->vidro)
    {
        wux::Media::AcrylicBrush vidro;
        vidro.BackgroundSource(wux::Media::AcrylicBackgroundSource::HostBackdrop);
        vidro.TintColor(Color{ 255, cores->tinta.R, cores->tinta.G, cores->tinta.B });
        vidro.TintOpacity(cores->tinta.A / 255.0);
        vidro.FallbackColor(cores->tinta);
        b.Background(vidro);
    }
    else
    {
        b.Background(wux::Media::SolidColorBrush(Color{ 255, cores->tinta.R, cores->tinta.G, cores->tinta.B }));
    }
    b.BorderBrush(wux::Media::SolidColorBrush(cores->borda));
    b.BorderThickness(wux::Thickness{ 1, 1, 1, 1 });
    b.CornerRadius(wux::CornerRadius{ cores->raio, cores->raio, cores->raio, cores->raio });
}

/// De qualquer thread: manda cada fundo se pintar com as cores (nulo = devolver o original) na thread dele.
static void PintarTodos(std::optional<Cores> cores)
{
    std::map<InstanceHandle, Fundo> copia;
    { std::lock_guard lock(g_estadoMutex); copia = g_fundos; }
    for (auto& [h, f] : copia)
    {
        auto b = f.borda.get();
        if (!b) continue;
        Fundo alvo = f;
        b.Dispatcher().RunAsync(winrt::Windows::UI::Core::CoreDispatcherPriority::Normal, [alvo, cores]() {
            try { Pintar(alvo, cores); }
            catch (...) { Log(L"ERRO_PINTAR|" + std::to_wstring(static_cast<uint32_t>(winrt::to_hresult()))); }
        });
    }
}

static void CALLBACK HostMorreu(PVOID, BOOLEAN)
{
    Log(L"HOST_MORREU");
    PintarTodos(std::nullopt);
}

/// Espera mudanças na pasta do arquivo de cores e reaplica. Arquivo ilegível mantém o último visual.
static void VigiarCores()
{
    std::wstring pasta = g_cores.substr(0, g_cores.find_last_of(L'\\'));
    HANDLE aviso = FindFirstChangeNotificationW(pasta.c_str(), FALSE, FILE_NOTIFY_CHANGE_LAST_WRITE | FILE_NOTIFY_CHANGE_FILE_NAME);
    if (aviso == INVALID_HANDLE_VALUE) { Log(L"FALHA_VIGIAR_CORES=" + std::to_wstring(GetLastError())); return; }
    while (WaitForSingleObject(aviso, INFINITE) == WAIT_OBJECT_0)
    {
        Sleep(30); // a troca do arquivo gera mais de um aviso
        if (auto cores = LerCores())
        {
            Log(cores->ligado ? L"CORES" : L"DESLIGADO");
            PintarTodos(cores->ligado ? cores : std::nullopt);
        }
        else Log(L"CORES_ILEGIVEIS");
        if (!FindNextChangeNotification(aviso)) break;
    }
    FindCloseChangeNotification(aviso);
}

// ---------- árvore visual ----------

class Vigia final : public IVisualTreeServiceCallback2
{
public:
    explicit Vigia(winrt::com_ptr<IXamlDiagnostics> diag) : m_diag(std::move(diag)) {}

    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppv) override
    {
        if (!ppv) return E_POINTER;
        if (riid == __uuidof(IUnknown) || riid == __uuidof(IVisualTreeServiceCallback) || riid == __uuidof(IVisualTreeServiceCallback2))
        {
            *ppv = static_cast<IVisualTreeServiceCallback2*>(this); AddRef(); return S_OK;
        }
        *ppv = nullptr; return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return ++m_ref; }
    ULONG STDMETHODCALLTYPE Release() override { ULONG r = --m_ref; if (!r) delete this; return r; }

    HRESULT STDMETHODCALLTYPE OnVisualTreeChange(ParentChildRelation, VisualElement el, VisualMutationType tipo) override
    {
        try
        {
            if (tipo != Add) return S_OK;
            std::wstring t = el.Type ? el.Type : L"";
            std::wstring n = el.Name ? el.Name : L"";
            if (t == L"StartMenu.StartBlendedFlexFrame")
            {
                Log(L"MOLDURA");
                std::thread([] { Sleep(5000); if (!g_achouFundo) Log(L"SEM_FUNDO"); }).detach();
            }
            if (t != L"Windows.UI.Xaml.Controls.Border" || n != L"AcrylicBorder") return S_OK;

            winrt::Windows::Foundation::IInspectable insp;
            winrt::check_hresult(m_diag->GetIInspectableFromHandle(el.Handle, reinterpret_cast<::IInspectable**>(winrt::put_abi(insp))));
            auto b = insp.try_as<wux::Controls::Border>();
            if (!b) return S_OK;

            Fundo f;
            f.borda = winrt::make_weak(b);
            f.fundoOriginal = b.Background();
            f.bordaOriginal = b.BorderBrush();
            f.espessuraOriginal = b.BorderThickness();
            f.cantosOriginais = b.CornerRadius();
            { std::lock_guard lock(g_estadoMutex); g_fundos[el.Handle] = f; }
            g_achouFundo = true;
            auto cores = LerCores();
            Pintar(f, cores && cores->ligado ? cores : std::nullopt); // já estamos na thread do Iniciar
            Log(L"PINTADO|" + std::to_wstring(el.Handle));
        }
        catch (...)
        {
            Log(L"ERRO|" + std::to_wstring(static_cast<uint32_t>(winrt::to_hresult())));
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE OnElementStateChanged(InstanceHandle, VisualElementState, LPCWSTR) override { return S_OK; }

private:
    std::atomic<ULONG> m_ref{ 1 };
    winrt::com_ptr<IXamlDiagnostics> m_diag;
};

class TapSite final : public IObjectWithSite
{
public:
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppv) override
    {
        if (!ppv) return E_POINTER;
        if (riid == __uuidof(IUnknown) || riid == __uuidof(IObjectWithSite)) { *ppv = static_cast<IObjectWithSite*>(this); AddRef(); return S_OK; }
        *ppv = nullptr; return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return ++m_ref; }
    ULONG STDMETHODCALLTYPE Release() override { ULONG r = --m_ref; if (!r) delete this; return r; }

    HRESULT STDMETHODCALLTYPE SetSite(IUnknown* site) override
    {
        if (!site) return S_OK;
        try
        {
            // Uma conexão por processo: o handle fica aberto de propósito até o Iniciar morrer.
            std::wstring mutexNome = L"Local\\VitralIniciar-" + std::to_wstring(GetCurrentProcessId());
            HANDLE mtx = CreateMutexW(nullptr, FALSE, mutexNome.c_str());
            if (mtx && GetLastError() == ERROR_ALREADY_EXISTS) { CloseHandle(mtx); return S_OK; }

            winrt::com_ptr<IXamlDiagnostics> diag;
            winrt::check_hresult(site->QueryInterface(__uuidof(IXamlDiagnostics), diag.put_void()));
            BSTR dados = nullptr;
            std::wstring init;
            if (SUCCEEDED(diag->GetInitializationData(&dados)) && dados) { init = dados; SysFreeString(dados); }
            g_cores = Parametro(init, L"cores");
            Log(L"SETSITE|" + init);

            if (DWORD hostPid = wcstoul(Parametro(init, L"hostpid").c_str(), nullptr, 10))
            {
                HANDLE host = OpenProcess(SYNCHRONIZE, FALSE, hostPid);
                HANDLE espera = nullptr;
                if (!host || !RegisterWaitForSingleObject(&espera, host, HostMorreu, nullptr, INFINITE, WT_EXECUTEONLYONCE))
                    Log(L"FALHA_VIGIAR_HOST=" + std::to_wstring(GetLastError()));
            }
            if (!g_cores.empty()) std::thread(VigiarCores).detach();

            winrt::com_ptr<IVisualTreeService3> servico;
            winrt::check_hresult(diag->QueryInterface(__uuidof(IVisualTreeService3), servico.put_void()));
            auto* vigia = new Vigia(diag);
            // AdviseVisualTreeChange de outra thread, sem esperar: ele precisa da thread do Iniciar, que é esta.
            std::thread([servico, vigia] {
                HRESULT hr = servico->AdviseVisualTreeChange(vigia);
                vigia->Release();
                Log(L"ADVISE|hr=" + std::to_wstring(static_cast<uint32_t>(hr)));
            }).detach();
        }
        catch (...)
        {
            Log(L"ERRO_SETSITE|" + std::to_wstring(static_cast<uint32_t>(winrt::to_hresult())));
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE GetSite(REFIID, void** ppv) override { if (ppv) *ppv = nullptr; return E_FAIL; }

private:
    std::atomic<ULONG> m_ref{ 1 };
};

class Fabrica final : public IClassFactory
{
public:
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppv) override
    {
        if (!ppv) return E_POINTER;
        if (riid == __uuidof(IUnknown) || riid == __uuidof(IClassFactory)) { *ppv = static_cast<IClassFactory*>(this); return S_OK; }
        *ppv = nullptr; return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return 2; }
    ULONG STDMETHODCALLTYPE Release() override { return 1; }
    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* outer, REFIID riid, void** ppv) override
    {
        if (outer) return CLASS_E_NOAGGREGATION;
        auto* site = new TapSite();
        HRESULT hr = site->QueryInterface(riid, ppv);
        site->Release();
        return hr;
    }
    HRESULT STDMETHODCALLTYPE LockServer(BOOL) override { return S_OK; }
};

static Fabrica g_fabrica;

extern "C" HRESULT __stdcall DllGetClassObject(REFCLSID clsid, REFIID riid, void** ppv)
{
    if (clsid != CLSID_VitralIniciar) return CLASS_E_CLASSNOTAVAILABLE;
    return g_fabrica.QueryInterface(riid, ppv);
}

extern "C" HRESULT __stdcall DllCanUnloadNow() { return S_FALSE; } // nunca descarrega dentro do Iniciar

BOOL APIENTRY DllMain(HMODULE modulo, DWORD motivo, LPVOID)
{
    if (motivo == DLL_PROCESS_ATTACH) { g_module = modulo; DisableThreadLibraryCalls(modulo); }
    return TRUE;
}
