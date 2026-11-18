# Pré-visualização do Spotify no notch

Pedido do Gustavo (17/11/2026, decidido na sessão que abriu esta). Base: o notch fechado com a música (0.19.0,
`docs/superpowers/specs/2026-11-16-notch-fechado-atividades-design.md`).

## 1. Por quê

Como o widget do Spotify no iPhone: com o Spotify **fechado**, o notch continua mostrando a última música que tocou
nele, com um play que abre o Spotify e retoma a música. Hoje, ao fechar o Spotify, a música some do notch.

## 2. O que fica

- **Quando aparece:** não há nenhuma sessão de mídia no Windows (o `IFonteMidia` real dá `null`), existe uma última
  música do Spotify guardada e ela não foi escondida com o ×. Se outro player (navegador) estiver com sessão, ele
  aparece como hoje; a prévia só ocupa o lugar vazio.
- **Só o Spotify:** a música é guardada só quando a sessão vem do Spotify (`NomeApp == "Spotify"`, que cobre o app da
  Store e o `Spotify.exe`), com título.
- **Notch fechado:** a capa à esquerda (ou à direita, com o Claude à esquerda, como hoje), **sem as barras**.
- **Mouse em cima:** a linha da música desce como hoje, com capa, título e "artista · Spotify", sem a linha do tempo e
  sem anterior/próxima; os botões são **play** e **×**. Clique no título abre o Spotify (sem tocar).
- **Play:** abre o Spotify pelo URI `spotify:` (registrado pelo app da Store e pelo instalador comum; se falhar, tenta
  `%APPDATA%\Spotify\Spotify.exe`) e espera até 20 s a sessão do Spotify aparecer nos controles de mídia do Windows
  (GSMTC); quando aparece, manda tocar (`TryPlayAsync`), tentando de novo até ela tocar ou o prazo acabar. O Spotify
  restaura sozinho a última faixa pausada; o Windows não expõe o ID da faixa, então "retomar a última" é isso.
  **Abre minimizado** (Gustavo, 17/11/2026): se foi o play que abriu o Spotify, a janela é minimizada quando aparece
  e vigiada por 5 s (o Spotify a restaura sozinho ao terminar de carregar). Já aberto, ou pelo clique no título, a
  janela fica como está. O × ou um comando na sessão real durante a abertura cancelam o play.
  Enquanto abre, o botão mostra pausa (resposta imediata ao clique); se der o prazo sem tocar, volta ao play.
- **×:** esconde a prévia até o Spotify **tocar** de novo (tocando, não só aberto). Guardado em disco: sobrevive a
  reiniciar o Vitral. Quando o Spotify toca, o × é esquecido e tudo volta como hoje.
- **Persistência:** `%LOCALAPPDATA%\Vitral\ultima-musica.json` (título, artista, escondida) e
  `%LOCALAPPDATA%\Vitral\ultima-capa.bin` (os bytes da capa). Grava só quando título, artista ou capa mudam.
- **A opção "Mídia" do perfil** continua desligando toda a música do notch, prévia incluída.

## 3. Abordagens

1. **Decorador da fonte de mídia (escolhida).** Um `FonteMidiaComPrevia` em `Vitral.Ui` embrulha a fonte do Windows:
   repassa o estado real quando há sessão e, sem sessão, publica um `EstadoMidia` com `Previa = true`. Guarda a
   última música por um `IGuardaUltimaMusica` (arquivo, em `Vitral.Core`) e abre o Spotify por um `IAbridorSpotify`
   (App). O notch e o cartão só leem `Previa`. Toda a regra fica testável sem WPF nem Windows.
2. **Lógica dentro do `FonteMidiaWindows`.** Menos peças, mas a regra (quando mostrar, ×, gravar) ficaria presa ao
   GSMTC e sem testes.
3. **Uma fonte separada só da prévia, decidida no notch.** Duplica a lógica de capa e cartão no notch e no VM.

## 4. Minhas decisões (corrigíveis)

- Sem chave nova no perfil: o × resolve o "tirar por enquanto" e a opção "Mídia" já desliga tudo.
- Notch fechado na prévia: capa sem barras (o lado direito fica vazio, com a largura de sempre para o relógio seguir
  no centro).
- O `IFonteMidia` ganha `EsconderPrevia()` com corpo vazio por padrão (as falsas dos testes não mudam).
- Spotify aberto sem sessão (acabou de abrir): a prévia continua até a sessão aparecer.

## 5. Fora

- Outros players; tocar uma faixa específica; a prévia com o Spotify aberto e pausado (aí vale a sessão real, como
  hoje).
