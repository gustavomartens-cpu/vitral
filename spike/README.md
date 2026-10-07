# spike/: fase 0 (prova de viabilidade)

**Código descartável.** Serve só para responder à pergunta da fase 0: uma DLL nossa, carregada via XAML Diagnostics, deixa a barra de tarefas transparente neste Windows (25H2, 26200.9550)? Nada daqui vai para a fase 1 sem ser reescrito com testes.

Plano: `docs/superpowers/plans/2026-10-06-fase0-prova-de-viabilidade.md`

## Ordem

1. `pwsh tools/instalar-ferramentas.ps1`: instala .NET 10, llvm-mingw e cppwinrt em `G:\dev`. Pede confirmação antes de cada download.
2. `. tools/env.ps1`: ativa as ferramentas no terminal atual.
3. `pwsh tap/gerar-projecao.ps1`, depois `pwsh tap/build.ps1 -Fonte tap/VitralTapSpike.cpp -Saida out/VitralTapSpike.dll`.
4. `pwsh tools/preparar-pasta.ps1`: aplica a ACL de `ALL APPLICATION PACKAGES` em `out/`.
5. **Feche o TranslucentTB.** Depois rode o `Injector` (veja `injector/Program.cs`). Os resultados vão para `resultados/rotas.csv`.
6. `dotnet run --project ucpd-probe -c Release`: regrava os mesmos valores para ver se o UCPD bloqueia.

Logs da DLL: `%LOCALAPPDATA%\Vitral\spike\tap-<pid>.log`.

Em caso de problema na barra, rode `Stop-Process -Name explorer`. Ele volta sozinho e limpo.
