// IniciarProva.dll: prova da fase 2 (DESCARTÁVEL), 02/11/2026.
// Carregada no Iniciar (StartMenuExperienceHost) via InitializeXamlDiagnosticsEx pela rota externa. Pinta o fundo do
// Iniciar (Border "AcrylicBorder") de âmbar com cantos de 20 px e desfaz tudo quando o processo host morre.
// Pergunta: dá para conectar e trocar um pincel do Iniciar redesenhado do 25H2 de forma repetível?
#include <windows.h>
#include <ocidl.h>
#include <xamlom.h>
#undef GetCurrentTime
#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.Core.h>
#include <winrt/Windows.UI.Xaml.h>
#include <winrt/Windows.UI.Xaml.Controls.h>
#include <winrt/Windows.UI.Xaml.Media.h>

#include <atomic>
#include <cstdio>
#include <map>
#include <mutex>
#include <string>
#include <thread>

namespace wux = winrt::Windows::UI::Xaml;

// {6B1C2E52-8F0A-4C3B-9A5E-3D7F1E2A9C41}: o mesmo da fase 0 (o injector passa o caminho da DLL).
static constexpr CLSID CLSID_TapSite = { 0x6b1c2e52, 0x8f0a, 0x4c3b, { 0x9a, 0x5e, 0x3d, 0x7f, 0x1e, 0x2a, 0x9c, 0x41 } };

static HMODULE g_module = nullptr;

static std::wstring PastaDaDll()
{
    wchar_t caminho[MAX_PATH]{};
    GetModuleFileNameW(g_module, caminho, MAX_PATH);
    std::wstring s(caminho);
    return s.substr(0, s.find_last_of(L'\\'));
}

static std::mutex g_logMutex;

static void Log(const std::wstring& linha)
{
    std::lock_guard lock(g_logMutex);
    std::wstring arquivo = PastaDaDll() + L"\\logs\\iniciar-" + std::to_wstring(GetCurrentProcessId()) + L".log";
    FILE* f = _wfopen(arquivo.c_str(), L"a, ccs=UTF-8");
    if (!f) { OutputDebugStringW((L"[IniciarProva] " + linha + L"\n").c_str()); return; }
    SYSTEMTIME t; GetLocalTime(&t);
    fwprintf(f, L"%02d:%02d:%02d.%03d %ls\n", t.wHour, t.wMinute, t.wSecond, t.wMilliseconds, linha.c_str());
    fclose(f);
}

static DWORD LerHost(const std::wstring& dados)
{
    size_t p = dados.find(L"hostpid=");
    return p == std::wstring::npos ? 0 : wcstoul(dados.c_str() + p + 8, nullptr, 10);
}

struct Alteracao
{
    winrt::weak_ref<wux::Controls::Border> borda;
    wux::Media::Brush fundoOriginal{ nullptr };
    wux::CornerRadius cantosOriginais{};
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
        auto borda = alt.borda.get();
        if (!borda) continue;
        ++n;
        Alteracao a = alt;
        borda.Dispatcher().RunAsync(winrt::Windows::UI::Core::CoreDispatcherPriority::Normal, [a]() {
            if (auto b = a.borda.get()) { b.Background(a.fundoOriginal); b.CornerRadius(a.cantosOriginais); }
            Log(L"DESFEITO");
        });
    }
    Log(L"RESTAURANDO=" + std::to_wstring(n));
}

static void CALLBACK HostMorreu(PVOID, BOOLEAN) { RestaurarTudo(); }

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
            if (t == L"StartMenu.StartBlendedFlexFrame") Log(L"MOLDURA");
            if (t != L"Windows.UI.Xaml.Controls.Border" || n != L"AcrylicBorder") return S_OK;

            winrt::Windows::Foundation::IInspectable insp;
            winrt::check_hresult(m_diag->GetIInspectableFromHandle(el.Handle, reinterpret_cast<::IInspectable**>(winrt::put_abi(insp))));
            auto borda = insp.try_as<wux::Controls::Border>();
            if (!borda) { Log(L"NAO_E_BORDA"); return S_OK; }

            Alteracao alt;
            alt.borda = winrt::make_weak(borda);
            alt.fundoOriginal = borda.Background();
            alt.cantosOriginais = borda.CornerRadius();
            borda.Background(wux::Media::SolidColorBrush(winrt::Windows::UI::Color{ 0xE6, 0x5A, 0x3A, 0x08 }));
            borda.CornerRadius(wux::CornerRadius{ 20, 20, 20, 20 });
            Log(L"PINTADO|" + std::to_wstring(el.Handle));
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
            std::wstring mutexNome = L"Local\\VitralTap-" + std::to_wstring(GetCurrentProcessId());
            HANDLE mtx = CreateMutexW(nullptr, FALSE, mutexNome.c_str());
            if (mtx && GetLastError() == ERROR_ALREADY_EXISTS) { CloseHandle(mtx); Log(L"JA_ANEXADO"); return S_OK; }

            winrt::com_ptr<IXamlDiagnostics> diag;
            winrt::check_hresult(site->QueryInterface(__uuidof(IXamlDiagnostics), diag.put_void()));
            BSTR dados = nullptr;
            std::wstring init;
            if (SUCCEEDED(diag->GetInitializationData(&dados)) && dados) { init = dados; SysFreeString(dados); }
            Log(L"SETSITE|init=" + init);

            if (DWORD hostPid = LerHost(init))
            {
                HANDLE host = OpenProcess(SYNCHRONIZE, FALSE, hostPid);
                HANDLE espera = nullptr;
                if (host && RegisterWaitForSingleObject(&espera, host, HostMorreu, nullptr, INFINITE, WT_EXECUTEONLYONCE))
                    Log(L"VIGIANDO_HOST=" + std::to_wstring(hostPid));
                else
                    Log(L"FALHA_VIGIAR_HOST=" + std::to_wstring(GetLastError()));
            }

            winrt::com_ptr<IVisualTreeService3> servico;
            winrt::check_hresult(diag->QueryInterface(__uuidof(IVisualTreeService3), servico.put_void()));
            auto* vigia = new Vigia(diag);
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
    if (clsid != CLSID_TapSite) return CLASS_E_CLASSNOTAVAILABLE;
    return g_fabrica.QueryInterface(riid, ppv);
}

extern "C" HRESULT __stdcall DllCanUnloadNow() { return S_FALSE; }

BOOL APIENTRY DllMain(HMODULE modulo, DWORD motivo, LPVOID)
{
    if (motivo == DLL_PROCESS_ATTACH) { g_module = modulo; DisableThreadLibraryCalls(modulo); }
    return TRUE;
}
