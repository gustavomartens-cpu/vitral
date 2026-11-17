# Notch fechado com atividades (R1, R3, R8)

Conversa de desenho com o Gustavo em 16/11/2026. Origem: bloco 3 de `docs/ideias-pendentes.md` e
`docs/pesquisa-notch-repos.md` §4 (R1, R3, R8). Prévia aprovada: `docs/superpowers/mockups/2026-11-16-notch-fechado.html`.

## 1. Por quê

Com o notch fechado, o Gustavo quer ver de relance duas coisas que hoje não aparecem: se o Claude está trabalhando (e
se parou esperando por ele) e o que está tocando. E quer o topo mais limpo: a música sai da ilha separada da esquerda e
passa a morar no notch.

## 2. O que fica

> **Mudou na execução (pedidos do Gustavo, 16/11/2026):** a capa fica à esquerda só sem o Claude; com ele à esquerda,
> a capa vai para a direita junto das barras. Sem som saindo do PC (música noutro aparelho, como o Echo Dot), as
> barras dançam num ritmo próprio (a análise de áudio do Spotify não está aberta a apps novos). Cada lado tem 52 px.
> O glifo ✳ virou ✶ (o ✳ sai como emoji no Windows). O logo do app do Claude perde a margem transparente.

### 2.1 Notch fechado (opção B)

```
 sem nada:            [   14:32   ]
 só o Claude:         [ ✻2  14:32      ]
 só a música:         [ ▣   14:32  ▮▯▮▮▯ ]
 Claude + música:     [ ✻2  14:32  ▮▯▮▮▯ ]
```

- **Lado esquerdo** do relógio, nesta ordem de prioridade (R8, ordem fixa no código, sem opção nas Configurações):
  1. Claude esperando você: ponto e número em âmbar (`Ambar` do tema);
  2. Claude rodando: spinner de glifos `· ✢ ✳ ✻ ✽ ✻ ✳ ✢` (~120 ms por glifo) na cor do Claude, com o número de sessões
     rodando ao lado;
  3. Claude terminou: check verde, some sozinho em ~3 s;
  4. capa da música (18 × 18, cantos 4).
- **Lado direito:** 5 barras do som que sai, tingidas pela cor média da capa (cinza claro sem capa). Música pausada: as
  barras ficam paradas baixas; sem sessão de mídia: lado direito vazio.
- Os dois lados têm **a mesma largura** quando algum deles tem conteúdo, para o relógio ficar sempre no centro. Sem
  nada nos dois lados, o notch volta à largura de hoje.
- O nome da música **não** aparece fechado.
- Mudar de largura usa a mola (`src/Vitral.Ui/Tema/Mola.cs`): abrir com `JeitoMola.Abrir`, fechar com `Fechar`.
- O número conta sessões; "esperando" ganha de "rodando" mesmo que só uma sessão espere e outras rodem (o número
  mostrado é o das que esperam).
- Avisos, fichas, perguntas e lembretes que já descem hoje continuam passando na frente de tudo, como hoje.
- `Movimento.Ligado` desligado: sem spinner girando (glifo fixo `✻`) e barras paradas.

### 2.2 Mouse em cima: o notch desce

Hoje o mouse parado 250 ms abre as laterais (Claude à esquerda, sino à direita). Fica igual, e com mídia o notch também
desce, **centralizado e já na largura das laterais** (`LarguraNormal + 2 × LarguraLateral`, a mesma do hover de hoje,
com "Notificações · N" cabendo inteiro):

```
 ┌─────────────────────────────────────────────────────┐
 │ ✻ Claude              22:05 seg, 16 nov   🔔 Notificações · 3 │
 │ ▣▣  Lo-fi beats to relax                ▮▯▮▮▯  ⏮ ⏸ ⏭  │
 │ ▣▣  Lofi Girl · Spotify                                │
 └─────────────────────────────────────────────────────┘
```

- Em cima, as laterais de hoje sem mudança (clique abre o Claude, as pendências, o calendário, o painel de
  notificações, como hoje).
- Embaixo, a linha da música: capa 46 × 46 (cantos 8), título, artista e app, barras, e os botões anterior,
  tocar/pausar e próxima (os da ilha de hoje). O clique no título faz o que o "Mais da mídia" da ilha fazia.
- A linha do tempo do painel de mídia de hoje (posição e arrastar) vem junto, fina, embaixo da linha da música
  (decisão minha: é função que existe hoje e não deve sumir).
- Sem mídia, o hover é o de hoje (só as laterais).
- O conteúdo da música entra como o resto do notch (V3: nasce do notch).

### 2.3 A ilha da mídia sai

- A ilha da mídia e o painel de mídia que desce dela deixam de existir na barra do topo.
- A opção "Mídia" do perfil (`IlhasBarra.Midia`) passa a ligar e desligar a música no notch (texto na tela ajustado).
  Perfis antigos continuam valendo sem migração.
- Barra única (`PosicaoIlhas.ComFundo`): a mídia não conta mais para mostrar a faixa de fundo.

## 3. Os sinais

### 3.1 Claude rodando (R1)

- Gancho novo **`UserPromptSubmit`** com `"async": true` (não atrasa o envio da mensagem), rodando o mesmo
  `Vitral.exe --aviso`. Vira um aviso calado de tipo novo (`Comecou`) que só marca a sessão como rodando; não desce.
- `Stop` (já existe) marca a sessão como terminada. `Notification` e `PermissionRequest` (já existem) marcam como
  esperando você; quando o pedido sai (respondido pelo notch ou resolvido no app), a sessão volta a rodando.
- `GanchosClaude` passa a ligar quatro eventos; `Atualizar` completa os ganchos de quem já tem os três de hoje.
- Ctrl+C não dispara o `Stop`. Uma sessão rodando cuja transcrição (`transcript_path`, que vem no
  `UserPromptSubmit`) termina com a marca de interrupção, ou não muda há 30 min, sai sozinha da contagem sem o check.
  Conferir a transcrição a cada ~10 s, só enquanto houver sessão rodando ou esperando.
- Turnos que o Gustavo não pediu (R4, `DeOutraOrigem`) não têm `UserPromptSubmit`, então não aparecem como rodando; o
  `Stop` deles só tira a sessão da contagem, sem o check.

Decisões minhas (revisão do desenho):

- **Quais `Notification` contam como esperando:** só as de pedido (permissão, pergunta, `elicitation`), pelo
  `notification_type`. O aviso de ocioso que vem depois do `Stop` não pinta de âmbar uma sessão que já terminou.
- **Saída da espera sem pedido no notch:** volta a rodando quando a transcrição volta a crescer, ou no próximo
  `Stop`/`UserPromptSubmit`.
- **Expiração:** a marca de interrupção no fim da transcrição (Ctrl+C) tira a sessão na hora; sem ela, só depois de 30 min
  sem a transcrição mudar (build ou teste longo não pode apagar o spinner com o Claude trabalhando).
- **Stop segurando a resposta pelo notch (até 19,5 min):** o turno acabou; conta como terminou (check), não como
  esperando.
- **Vitral aberto no meio de um turno:** a sessão só aparece no próximo evento. Limite aceito, sem reconstruir pelas
  transcrições.
- **Sem `session_id`:** a chave é a pasta (`cwd`).

### 3.2 Som que sai (R3)

- Captura do áudio que sai pelo **WASAPI em loopback** no aparelho de saída padrão, pela interop COM própria (a mesma
  linha de `CoreAudio.cs`, sem pacote novo). Formato do mix (float 32), downmix para mono.
- **5 bandas por Goertzel** (centros perto de 60, 250, 1 k, 4 k e 10 kHz) em janelas de ~20 ms, ~30 atualizações por
  segundo, com subida rápida e descida suave e ganho automático lento (as barras não somem em música baixa).
- A captura liga só com sessão de mídia tocando e o notch visível; desliga com a mídia pausada, tela cheia ou sem
  mídia. Trocar o aparelho de saída padrão reinicia a captura.
- Cor média da capa: média dos pixels da capa reduzida, clareada se ficar escura demais para o fundo preto.
- Técnica só: nada copiado dos projetos da pesquisa (vários são GPL).

## 4. Abordagens

Para as barras:

1. **Loopback WASAPI com interop própria + Goertzel** (escolhida). Bandas de verdade, sem dependência nova; a interop
   COM do áudio já é do Vitral. Mais trabalho (G).
2. **Medidor de pico** (`IAudioMeterInformation`) com 5 barras inventadas a partir de um nível só. Barato e leve, mas as
   barras não seguem o som de verdade (todas sobem e descem juntas).
3. **Pacote NAudio** para a captura. Menos interop, mas traz dependência nova para uma parte pequena.

A escolha foi minha, pela qualidade das barras e por não trazer pacote novo; o Gustavo deixou o como comigo.

Para a coexistência do Claude com a música, o Gustavo viu A (um de cada vez), B (dividido) e C (revezando) e escolheu
o **B**, com o notch descendo ao passar o mouse e a ilha da mídia retirada.

## 5. Fora do escopo

- V4 sombra e V5 janela do tamanho certo (bloco 1, depois).
- Ordem configurável nas Configurações (R8 virou ordem fixa).
- Nome da música no notch fechado.
- Cota do Claude (R9) e atalhos (R6).

## 6. Riscos

- Mais um processo `Vitral.exe` por mensagem enviada (assíncrono, não atrasa o Claude).
- Loopback custa um pouco de CPU: medir no Gerenciador com música tocando (meta: abaixo de 1 % num núcleo).
- Tela estreita (notebook): o hover com mídia tem a mesma largura do hover de hoje, então não colide mais do que hoje.

## 7. Pronto quando

Build e testes passam (regra da ordem dos lados, contagem de sessões com começo, fim, espera e expiração, Goertzel com
seno conhecido, ganchos com o quarto evento); o Gustavo viu funcionando; notas da versão e `ideias-pendentes.md`
atualizados; PR contra `main` avisando que depende da 0.18.0.
