// VitralTapSpike.dll: TAP mínima da fase 0 (DESCARTÁVEL).
// Carregada pelo Windows via InitializeXamlDiagnosticsEx. Registra a árvore visual em log,
// deixa transparente o fundo da barra de tarefas (Rectangle "BackgroundFill" dentro de "TaskbarFrame")
// e restaura tudo quando o processo host morre.
#include <windows.h>
#include <ocidl.h>
#include <xamlom.h>
#undef GetCurrentTime // macro do winbase.h colide com C++/WinRT
#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.Core.h>
#include <winrt/Windows.UI.Xaml.h>
#include <winrt/Windows.UI.Xaml.Media.h>
#include <winrt/Windows.UI.Xaml.Shapes.h>

#include <atomic>
#include <cstdio>
#include <map>
#include <mutex>
#include <string>
#include <thread>

namespace wux = winrt::Windows::UI::Xaml;

// {6B1C2E52-8F0A-4C3B-9A5E-3D7F1E2A9C41}
static constexpr CLSID CLSID_TapSite = { 0x6b1c2e52, 0x8f0a, 0x4c3b, { 0x9a, 0x5e, 0x3d, 0x7f, 0x1e, 0x2a, 0x9c, 0x41 } };

static HMODULE g_module = nullptr;
static std::atomic<long> g_objetos{ 0 };

// ---------- caminhos e log ----------

static std::wstring PastaDaDll()
{
    wchar_t caminho[MAX_PATH]{};
    GetModuleFileNameW(g_module, caminho, MAX_PATH);
    std::wstring s(caminho);
    return s.substr(0, s.find_last_of(L'\\'));
}

static std::wstring CaminhoDaDll()
{
    wchar_t caminho[MAX_PATH]{};
    GetModuleFileNameW(g_module, caminho, MAX_PATH);
    return caminho;
}

static std::mutex g_logMutex;

static void Log(const std::wstring& linha)
{
    std::lock_guard lock(g_logMutex);
    std::wstring arquivo = PastaDaDll() + L"\\logs\\tap-" + std::to_wstring(GetCurrentProcessId()) + L".log";
    FILE* f = _wfopen(arquivo.c_str(), L"a, ccs=UTF-8");
    if (!f) { OutputDebugStringW((L"[VitralTap] " + linha + L"\n").c_str()); return; }
    SYSTEMTIME t; GetLocalTime(&t);
    fwprintf(f, L"%02d:%02d:%02d.%03d %ls\n", t.wHour, t.wMinute, t.wSecond, t.wMilliseconds, linha.c_str());
    fclose(f);
}

// ---------- dados de inicialização: "hostpid=<n>;modo=<transparente|log>" ----------

struct Config { DWORD hostPid = 0; bool transparente = false; };

static Config LerConfig(const std::wstring& dados)
{
    Config c;
    size_t p = dados.find(L"hostpid=");
    if (p != std::wstring::npos) c.hostPid = wcstoul(dados.c_str() + p + 8, nullptr, 10);
    c.transparente = dados.find(L"modo=transparente") != std::wstring::npos;
    return c;
}

static std::wstring LerArquivoInit()
{
    std::wstring arquivo = PastaDaDll() + L"\\init.txt";
    FILE* f = _wfopen(arquivo.c_str(), L"r, ccs=UTF-8");
    if (!f) return L"";
    wchar_t buf[256]{};
    fgetws(buf, 255, f);
    fclose(f);
    return buf;
}

// ---------- estado alterado (para restaurar) ----------

struct Alteracao
{
    winrt::weak_ref<wux::Shapes::Shape> forma;
    wux::Media::Brush fillOriginal{ nullptr };
    double opacidadeOriginal = 1.0;
    bool mudouFill = false;
};

static std::mutex g_estadoMutex;
static std::map<InstanceHandle, Alteracao> g_alteracoes;

static void RestaurarTudo()
{
    std::map<InstanceHandle, Alteracao> copia;
    { std::lock_guard lock(g_estadoMutex); copia.swap(g_alteracoes); }
    long n = 0;
    for (auto& [h, alt] : copia)
    {
        auto forma = alt.forma.get();
        if (!forma) continue;
        ++n;
        auto dispatcher = forma.Dispatcher();
        Alteracao a = alt;
        dispatcher.RunAsync(winrt::Windows::UI::Core::CoreDispatcherPriority::Normal, [a]() {
            if (auto s = a.forma.get())
            {
                if (a.mudouFill) s.Fill(a.fillOriginal); else s.Opacity(a.opacidadeOriginal);
            }
        });
    }
    Log(L"RESTAURADO=" + std::to_wstring(n));
}

static void CALLBACK HostMorreu(PVOID, BOOLEAN) { RestaurarTudo(); }

// ---------- watcher da árvore visual ----------

class VisualTreeWatcher final : public IVisualTreeServiceCallback2
{
public:
    VisualTreeWatcher(winrt::com_ptr<IXamlDiagnostics> diag, Config cfg) : m_diag(std::move(diag)), m_cfg(cfg) { ++g_objetos; }
    ~VisualTreeWatcher() { --g_objetos; }

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
            Log(L"ADD|" + t + L"|" + n + L"|" + std::to_wstring(el.Handle));

            if (t == L"Taskbar.TaskbarFrame") Log(L"FRAMES=" + std::to_wstring(++m_frames));

            if (!m_cfg.transparente || t != L"Windows.UI.Xaml.Shapes.Rectangle") return S_OK;
            if (n != L"BackgroundFill" && n != L"BackgroundStroke") return S_OK;

            winrt::Windows::Foundation::IInspectable insp;
            winrt::check_hresult(m_diag->GetIInspectableFromHandle(el.Handle, reinterpret_cast<::IInspectable**>(winrt::put_abi(insp))));
            auto forma = insp.try_as<wux::Shapes::Shape>();
            if (!forma || !TemAncestralTaskbarFrame(forma)) { Log(L"IGNORADO_SEM_TASKBARFRAME|" + n); return S_OK; }

            Alteracao alt;
            alt.forma = winrt::make_weak(forma);
            if (n == L"BackgroundFill")
            {
                alt.mudouFill = true;
                alt.fillOriginal = forma.Fill();
                forma.Fill(wux::Media::SolidColorBrush(winrt::Windows::UI::Color{ 0, 0, 0, 0 }));
                Log(L"FILL_APLICADO|" + std::to_wstring(el.Handle));
            }
            else
            {
                alt.opacidadeOriginal = forma.Opacity();
                forma.Opacity(0.0);
                Log(L"STROKE_APLICADO|" + std::to_wstring(el.Handle));
            }
            std::lock_guard lock(g_estadoMutex);
            g_alteracoes[el.Handle] = alt;
        }
        catch (...)
        {
            Log(L"ERRO|" + std::to_wstring(static_cast<uint32_t>(winrt::to_hresult())));
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE OnElementStateChanged(InstanceHandle, VisualElementState, LPCWSTR) override { return S_OK; }

private:
    static bool TemAncestralTaskbarFrame(const wux::DependencyObject& d)
    {
        auto atual = wux::Media::VisualTreeHelper::GetParent(d);
        while (atual)
        {
            if (auto fe = atual.try_as<wux::FrameworkElement>(); fe && fe.Name() == L"TaskbarFrame") return true;
            atual = wux::Media::VisualTreeHelper::GetParent(atual);
        }
        return false;
    }

    std::atomic<ULONG> m_ref{ 1 };
    winrt::com_ptr<IXamlDiagnostics> m_diag;
    Config m_cfg;
    long m_frames = 0;
};

// ---------- TapSite (IObjectWithSite) ----------

class TapSite final : public IObjectWithSite
{
public:
    TapSite() { ++g_objetos; }
    ~TapSite() { --g_objetos; }

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
            std::wstring mutexNome = L"Local\\VitralTap-" + std::to_wstring(GetCurrentProcessId());
            HANDLE mtx = CreateMutexW(nullptr, FALSE, mutexNome.c_str());
            if (mtx && GetLastError() == ERROR_ALREADY_EXISTS) { CloseHandle(mtx); Log(L"JA_ANEXADO"); return S_OK; }
            // o handle do mutex fica aberto de propósito: marca este processo como anexado até ele morrer

            winrt::com_ptr<IXamlDiagnostics> diag;
            winrt::check_hresult(site->QueryInterface(__uuidof(IXamlDiagnostics), diag.put_void()));

            BSTR dados = nullptr;
            std::wstring init;
            if (SUCCEEDED(diag->GetInitializationData(&dados)) && dados) { init = dados; SysFreeString(dados); }
            if (init.empty()) init = LerArquivoInit();
            Config cfg = LerConfig(init);
            Log(L"SETSITE|init=" + init);

            if (cfg.hostPid)
            {
                HANDLE host = OpenProcess(SYNCHRONIZE, FALSE, cfg.hostPid);
                HANDLE espera = nullptr;
                if (host && RegisterWaitForSingleObject(&espera, host, HostMorreu, nullptr, INFINITE, WT_EXECUTEONLYONCE))
                    Log(L"VIGIANDO_HOST=" + std::to_wstring(cfg.hostPid));
                else
                    Log(L"FALHA_VIGIAR_HOST=" + std::to_wstring(GetLastError()));
            }

            winrt::com_ptr<IVisualTreeService3> servico;
            winrt::check_hresult(diag->QueryInterface(__uuidof(IVisualTreeService3), servico.put_void()));
            auto* watcher = new VisualTreeWatcher(diag, cfg);

            // AdviseVisualTreeChange chamado de outra thread (padrão TranslucentTB/Windhawk: evita travar)
            HRESULT hr = E_FAIL;
            std::thread([&] { hr = servico->AdviseVisualTreeChange(watcher); }).join();
            watcher->Release();
            Log(L"ADVISE|hr=" + std::to_wstring(static_cast<uint32_t>(hr)));

            std::wstring eventoNome = L"Local\\VitralTapReady-" + std::to_wstring(GetCurrentProcessId());
            if (HANDLE ev = CreateEventW(nullptr, TRUE, FALSE, eventoNome.c_str())) { SetEvent(ev); CloseHandle(ev); }
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

// ---------- fábrica COM ----------

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
    if (clsid != CLSID_TapSite) return CLASS_E_CLASSNOTAVAILABLE;
    return g_fabrica.QueryInterface(riid, ppv);
}

extern "C" HRESULT __stdcall DllCanUnloadNow() { return S_FALSE; } // nunca descarrega dentro do Explorer

// ---------- rota interna: hook que carrega a DLL no alvo e chama a API de dentro ----------

using PFN_Init = HRESULT(WINAPI*)(PCWSTR, DWORD, PCWSTR, PCWSTR, CLSID, PCWSTR);
static std::atomic_flag g_hookIniciado = ATOMIC_FLAG_INIT;

static void IniciarDeDentro()
{
    HMODULE xaml = LoadLibraryExW(L"Windows.UI.Xaml.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
    auto init = xaml ? reinterpret_cast<PFN_Init>(GetProcAddress(xaml, "InitializeXamlDiagnosticsEx")) : nullptr;
    if (!init) { Log(L"INTERNA|sem InitializeXamlDiagnosticsEx"); return; }
    std::wstring dll = CaminhoDaDll();
    std::wstring dados = LerArquivoInit();
    for (int n = 1; n <= 60; ++n)
    {
        HRESULT hr = E_FAIL;
        std::wstring endpoint = L"VisualDiagConnection" + std::to_wstring(n);
        std::thread([&] { hr = init(endpoint.c_str(), GetCurrentProcessId(), nullptr, dll.c_str(), CLSID_TapSite, dados.c_str()); }).join();
        Log(L"INTERNA|tentativa=" + std::to_wstring(n) + L"|hr=" + std::to_wstring(static_cast<uint32_t>(hr)));
        if (SUCCEEDED(hr)) return;
        Sleep(500);
    }
}

extern "C" LRESULT CALLBACK VitralHookProc(int code, WPARAM wParam, LPARAM lParam)
{
    if (!g_hookIniciado.test_and_set())
    {
        HMODULE fixo = nullptr;
        GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN,
                           reinterpret_cast<LPCWSTR>(&VitralHookProc), &fixo);
        std::thread(IniciarDeDentro).detach();
    }
    return CallNextHookEx(nullptr, code, wParam, lParam);
}

BOOL APIENTRY DllMain(HMODULE modulo, DWORD motivo, LPVOID)
{
    if (motivo == DLL_PROCESS_ATTACH) { g_module = modulo; DisableThreadLibraryCalls(modulo); }
    return TRUE;
}
