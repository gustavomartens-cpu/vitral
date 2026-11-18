# Plano: pré-visualização do Spotify

Spec: `docs/superpowers/specs/2026-11-17-previa-spotify-design.md`.

1. **Guarda em disco** (`Vitral.Core/Barra/GuardaUltimaMusica.cs`, testes em `tests/Vitral.Core.Tests`):
   `UltimaMusica(Titulo, Artista, Capa, Escondida)`; `Ler()` devolve null sem arquivo ou com JSON quebrado; `Gravar()`
   escreve JSON + capa (apaga a capa velha quando não há capa). Nunca lança.
2. **Decorador** (`Vitral.Ui/Barra/FonteMidiaComPrevia.cs`, testes em `tests/Vitral.Ui.Tests`):
   - repassa o estado real; sessão do Spotify com título grava a última música (só quando muda); tocando zera o ×;
   - sem sessão + última + não escondida → `EstadoMidia(..., "Spotify", Tocando: abrindo, Capa, Previa: true)`;
   - `TocarPausar` na prévia chama `IAbridorSpotify.AbrirETocarAsync()`, marca abrindo, volta se der falso;
   - `TrazerApp` na prévia chama `Abrir()`; `Anterior/Proxima` na prévia não fazem nada;
   - `EsconderPrevia` grava escondida e publica null.
3. **Lados** (`LadosNotch`): `Lados.Previa`; o notch esconde as barras na prévia.
4. **Cartão** (`PainelMidiaViewModel`): `Previa` e `EsconderCommand`; sem progresso na prévia.
5. **App:** `AbridorSpotify` (URI `spotify:` → exe; espera a sessão GSMTC do Spotify e manda tocar); `ControleBarra`
   monta o decorador; `JanelaNotch.Midia` troca anterior/próxima por × na prévia; `JanelaNotch.Atividades` esconde as
   barras.
6. Build, testes, prova na máquina, revisão Opus, notas da versão.
