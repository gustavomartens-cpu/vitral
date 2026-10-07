// Teste de compilação: a projeção C++/WinRT de Windows.UI.Xaml e o xamlom.h compilam com llvm-mingw.
#include <windows.h>
#include <xamlom.h>
#undef GetCurrentTime // macro do winbase.h colide com Storyboard::GetCurrentTime do C++/WinRT
#include <winrt/Windows.UI.Xaml.Media.h>
#include <winrt/Windows.UI.Xaml.Shapes.h>

extern "C" __declspec(dllexport) unsigned VitralHelloAlpha()
{
    winrt::Windows::UI::Color cor{};
    cor.A = 7;
    return cor.A;
}

BOOL APIENTRY DllMain(HMODULE, DWORD, LPVOID) { return TRUE; }
