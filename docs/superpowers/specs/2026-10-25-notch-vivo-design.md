# Notch vivo: orelhas e avisos do Claude

Data: 25/10/2026. O notch do relógio ganha cantos curvos ("orelhas") emendando na borda de cima e passa a mostrar os
avisos do Claude Code, levando ao Claude com um clique.

## 1. Objetivo e escopo

O Gustavo quer o notch "vivo": o lugar onde chegam os avisos. A ideia original (`docs/ideias-pendentes.md`, "Notch
vivo") tem três partes independentes, e esta fase faz duas delas:

| Parte | Nesta fase? |
|-------|-------------|
| Orelhas: cantos curvos ligando o notch à borda | sim |
| Avisos do Claude Code no notch (terminou / esperando você) e clique levando ao Claude | sim |
| Notificações de todos os apps no notch | não: exige identidade de pacote; fase própria, começando por um teste de viabilidade |

**Responder pelo notch não entra.** O Claude Code não aceita resposta vinda de fora da sessão, e o Gustavo preferiu
que o clique leve ao Claude, onde ele responde. Chat próprio pela API da Anthropic também não entra.

## 2. Escolhas do Gustavo

As páginas ficam em `.superpowers/brainstorm/3934-1792969374/content/`.

| Item | Escolha |
|------|---------|
| Escopo | Orelhas + avisos do Claude (a 3ª parte fica para outra fase) |
| Ação do aviso | Avisar e, no clique, trazer o Claude para a frente |
| Visual | `notch-aviso.html`, A: notch preto sólido com orelhas, que se alarga no aviso ("ilha dinâmica") |
| Caminho do aviso | Canal privado (*named pipe*) + o próprio `Vitral.exe --aviso` como mensageiro |

## 3. O notch

### 3.1 Janela própria

A ilha do relógio deixa a `JanelaIlha` (vidro acrílico, sem transparência possível) e passa para uma janela nova,
`JanelaNotch`, em `src/Vitral.App/Barra/JanelaNotch.cs`:

- `AllowsTransparency=True`, sem bordas, `Topmost`, `WS_EX_TOOLWINDOW` e `WS_EX_NOACTIVATE`, como as ilhas;
- o formato é um `Path` desenhado (ver §3.2). As áreas transparentes deixam o clique passar;
- a janela tem o tamanho do estado de aviso (§3.3) mais as orelhas e fica centrada no topo do monitor principal,
  começando `NotchSobra` (10 px) acima da borda, como hoje. Só o desenho muda de tamanho dentro dela.

A reserva de espaço do topo (46 px) não muda. O aviso cresce por cima das janelas por alguns segundos.

### 3.2 Formato

| Elemento | Valor |
|----------|-------|
| Corpo | preto sólido `#FF0B0C10` |
| Cantos de baixo | raio de 16 px no normal e de 22 px no aviso |
| Orelhas | uma de cada lado, no topo: quarto de círculo côncavo de 16 px, também `#FF0B0C10` |

Cada orelha é o quadrado de 16 px menos o quarto de círculo. O lado curvo fica para fora e para baixo, e o reto
encosta no corpo e na borda.

A geometria vem de uma função pura, `FormaNotch.Caminho(largura, altura, raio, orelha) → string` (minilinguagem de
`Path`, em DIPs, com origem no canto de cima da orelha esquerda). Ela fica em `src/Vitral.Ui/Barra/FormaNotch.cs`,
com testes.

### 3.3 Estados

| Estado | Altura visível | Largura do corpo | Conteúdo | Raio |
|--------|----------------|------------------|----------|------|
| Normal | 34 px | a do relógio (como hoje) | hora e data | 16 |
| Aviso | 52 px | o conteúdo, no máximo 360 px | ícone do Claude, título, linha de baixo, "abrir ›" | 22 |
| Lembrete | 34 px | a do relógio + 14 px | hora, data e um ponto âmbar de 6 px com brilho | 16 |

**Conteúdo do aviso:**

- **Ícone:** quadrado de 22 px, cantos de 6, cor `#FFD97757`, com um "C" branco em negrito.
- **Título:**
  - "Claude terminou a tarefa" no `Stop`;
  - "Claude está esperando você" no `Notification`.
- **Linha de baixo:** `«sessão» · «texto»`, em `TextoSecundario` e corpo 11, cortada com reticências.
- **"abrir ›":** em `Ambar`, corpo 11.

**Transições:**

- normal ↔ aviso e lembrete ↔ aviso: 320 ms, com `CurvaMola`, animando largura, altura e raio do corpo;
- o conteúdo troca com um fade de `DuracaoMedia`;
- com "Efeitos de animação" desligado no Windows, a troca é direta (chave geral do tema).

**Regras de tempo:**

- O aviso fica **6 s**. Depois recolhe para **lembrete** se não foi aberto, ou para **normal** se foi.
- Um aviso novo substitui o atual e reinicia os 6 s.
- Com o mouse sobre o notch, o aviso não recolhe; os 6 s recomeçam quando o mouse sai.

**Cliques:**

| Estado | Clique |
|--------|--------|
| Aviso | abre o Claude (§4.5) e recolhe para normal |
| Lembrete | abre o calendário, como hoje, e volta ao normal (limpa o ponto) |
| Normal | abre o calendário, como hoje |

A máquina de estados é pura: `EstadoNotch` em `src/Vitral.Ui/Barra/EstadoNotch.cs`, com as entradas `Chegou(aviso)`,
`Passou(tempo)`, `MouseEntrou/Saiu` e `Clicou` e as saídas estado, aviso atual e ação (`AbrirClaude`,
`AbrirCalendario`, nenhuma). Tem testes.

### 3.4 Laterais ao passar o mouse

Acrescentadas em 25/10, a pedido do Gustavo, depois da primeira conferência na tela.

**Quando abrem e fecham:**

- Com o mouse parado **250 ms** sobre o notch, no estado normal ou de lembrete, ele se alarga para os dois lados. Usa a
  mesma mola de 320 ms, e o relógio continua no meio.
- No estado de aviso, o mouse só segura o aviso, como em §3.3.
- Tirando o mouse, as laterais recolhem.

**O que cada parte mostra e faz:**

| Parte | Largura | Mostra | Clique |
|-------|---------|--------|--------|
| Esquerda | 130 px | ícone do Claude e, se houver aviso pendente, "terminou" ou "esperando" e a sessão; sem pendência, só "Claude" | traz o Claude (§4.5) e limpa o lembrete |
| Meio | a do relógio | o relógio | calendário, como hoje |
| Direita | 130 px | sino (glifo `E7E7`) e "Notificações" | abre a central de notificações do Windows (Win+N, o mesmo atalho de antes do notch) |

O corpo com as laterais tem a largura do relógio mais 260 px. Por isso a janela passa a ter 440 px de corpo no máximo
(`NotchMedidas.LarguraMaxCorpo`), mais as orelhas.

**Notificações de verdade no lado direito** ficam para a fase das notificações: ler as notificações exige identidade de
pacote (§1). Até lá, o lado direito é só o atalho para a central do Windows, que continua abrindo no canto dela.

**`EstadoNotch` ganha:**

- `bool Laterais`, que liga com o mouse parado 250 ms (pelo `Tique`) fora do aviso e desliga em `MouseSaiu`;
- `bool Pendente`, que é true do aviso até ele ser aberto, pelo clique no aviso ou na lateral esquerda;
- `Clicou(ParteNotch parte)`, com `enum ParteNotch { Meio, Esquerda, Direita }`:
  - a Esquerda devolve `AbrirClaude` e limpa a pendência;
  - a Direita devolve `AbrirNotificacoes` (novo valor de `AcaoNotch`);
  - o Meio mantém §3.3.

## 4. O caminho do aviso

### 4.1 Ganchos do Claude Code

O Vitral escreve no `%USERPROFILE%\.claude\settings.json` (o arquivo de configuração do usuário do Claude Code), dentro
de `hooks`:

```json
"Stop":         [{ "hooks": [{ "type": "command", "command": "\"<caminho>\\Vitral.exe\" --aviso" }] }],
"Notification": [{ "hooks": [{ "type": "command", "command": "\"<caminho>\\Vitral.exe\" --aviso" }] }]
```

- `<caminho>` é a pasta do `Vitral.exe` que está rodando quando a chave é ligada (`Environment.ProcessPath`). Na
  instalação, é `%LOCALAPPDATA%\VitralApp\current`, que não muda de lugar nas atualizações.
- Os nossos itens são reconhecidos pelo trecho `Vitral.exe" --aviso` no comando.

### 4.2 Ligar e desligar (`GanchosClaude`)

Funções puras sobre o texto do `settings.json`, em `src/Vitral.Core/Claude/GanchosClaude.cs`, com testes:

- `EstaLigado(texto) → bool`;
- `Ligar(texto, comando) → string`:
  - acrescenta os dois itens;
  - não duplica se já existirem e troca o caminho se ele mudou;
  - preserva o resto do arquivo: outros ganchos, outras chaves, a ordem;
  - texto vazio vira `{"hooks":{...}}`;
- `Desligar(texto) → string`: remove só os nossos itens e apaga `Stop`/`Notification` e `hooks` se ficarem vazios;
- JSON com defeito → `FormatException`, e nada é gravado.

**Quem grava** (`ServicoGanchosClaude`, no App):

- lê o arquivo, guarda uma cópia em `settings.json.vitral-bak` e grava num temporário que depois troca de lugar;
- o estado da chave é sempre lido do arquivo (`EstaLigado`), não guardado à parte;
- arquivo inexistente: `Ligar` cria a pasta e o arquivo.

### 4.3 Mensageiro (`Vitral.exe --aviso`)

No `Program.Main`, **antes** do `VelopackApp` e do WPF: se `args[0] == "--aviso"`, roda `Mensageiro.Executar()` e sai
com código 0.

1. Lê a entrada padrão até 64 KB, com limite de 2 s.
2. Monta o aviso com `AvisoClaude.DeGancho(json, lerFimDoArquivo)`, em `src/Vitral.Core/Claude/AvisoClaude.cs`, puro
   e com testes:
   - **Campos lidos:** `hook_event_name`, `cwd`, `message`, `transcript_path`.
   - **Tipo:**
     - `Stop` vira `Terminou`;
     - `Notification` vira `Esperando`;
     - outro evento devolve null, e nada é enviado.
   - **`Sessao`:** o nome da última pasta de `cwd`, ou "Claude" se vier vazio.
   - **`Texto`:**
     - no `Notification`, o `message`;
     - no `Stop`, a última mensagem do assistente com texto. Ela vem dos últimos 64 KB do `transcript_path` (JSONL):
       a linha mais recente com `"type":"assistant"` e conteúdo de texto, o primeiro bloco `text`;
     - se faltar, fica vazio.
   - **Limpeza do texto:** `Encurtar(texto, 80)`. Junta espaços e quebras de linha numa linha só, tira marcação
     Markdown simples (`*`, `` ` ``, `#` no começo) e corta com "…".
3. Envia pelo canal (§4.4) e espera no máximo 300 ms para conectar. Sem Vitral rodando, sai sem erro.
4. Qualquer exceção é engolida; o código de saída é sempre 0.

### 4.4 Canal

- **Nome:** `Vitral-Avisos-<SID do usuário>`.
- **Servidor:** `NamedPipeServerStream` no Vitral, com `PipeSecurity`, só para o usuário atual. Aceita uma conexão por
  vez, em laço, numa tarefa em segundo plano.
- **Mensagem:** uma linha JSON `{"tipo":"Terminou|Esperando","sessao":"…","texto":"…"}` de no máximo 4 KB. Linha
  maior ou inválida é descartada.
- **Entrega:** o aviso passa para a thread da interface (`Dispatcher.BeginInvoke`) e vai para o `EstadoNotch`. Com a
  barra do topo desligada, ele é descartado.
- A tarefa para no `Sair()`.

### 4.5 Abrir o Claude

Ao clicar no aviso, o Vitral procura um processo `claude` (o app do Claude) com janela principal visível:

- se a janela estiver minimizada, `ShowWindow(SW_RESTORE)`;
- depois, `SetForegroundWindow`. O Windows permite porque a última entrada foi o clique no notch;
- sem o app aberto, nada acontece. Não há aviso de erro: o notch só recolhe.

### 4.6 A chave na janela

A caixa "Avisos do Claude no notch" fica na barra lateral da janela principal, logo abaixo de "Iniciar com o Windows",
no mesmo estilo. Ela não faz parte de perfil: é uma preferência do app.

- **Marcar:** `ServicoGanchosClaude.Ligar()`.
- **Desmarcar:** `Desligar()`.
- **Em caso de falha:** a caixa volta ao estado anterior e aparece o aviso "Não foi possível alterar os avisos do
  Claude: <motivo>". Arquivo com defeito tem a sua mensagem: "O settings.json do Claude está com defeito; corrija-o
  antes."
- **Ao abrir a janela:** a caixa reflete `EstaLigado`.
- **ViewModel:** `PrincipalViewModel.AvisosClaude` (bool) e a interface `IGanchosClaude`, para os testes.

## 5. Testes

**Com teste escrito antes:**

- `AvisoClaude.DeGancho`:
  - `Stop` com transcrição;
  - `Notification` com mensagem;
  - evento desconhecido devolve null;
  - JSON vazio ou com defeito devolve null;
  - `cwd` ausente vira "Claude";
  - transcrição ilegível deixa o texto vazio.
- Última mensagem do assistente: várias linhas, a última sem texto (só ferramenta) e linha cortada no começo do trecho
  de 64 KB.
- `Encurtar`: quebras de linha, Markdown e corte em 80 com "…".
- `GanchosClaude`:
  - ligar num arquivo vazio, num arquivo com outros ganchos (preservados) e num arquivo já ligado (sem duplicar);
  - caminho novo substitui o antigo;
  - desligar tira só os nossos e limpa os vazios;
  - JSON com defeito lança `FormatException`.
- `FormaNotch.Caminho`: começa e termina nas orelhas, tem a altura e a largura pedidas, e o raio aparece nos arcos.
- `EstadoNotch`:
  - aviso, 6 s e lembrete;
  - aviso, clique e normal, com a ação `AbrirClaude`;
  - aviso novo reinicia os 6 s;
  - com o mouse em cima não recolhe;
  - clique no lembrete abre o calendário.
- `PrincipalViewModel.AvisosClaude`: marcar liga, falha volta ao estado anterior e avisa.

**Na tela, com o Gustavo:**

- o notch com as orelhas emendando na borda;
- um aviso de verdade do Claude Code: este próprio Claude terminando uma resposta;
- o clique trazendo o Claude;
- o lembrete;
- o calendário;
- a chave ligando e desligando os ganchos, conferindo o `settings.json`.

**Medida:** o tempo do mensageiro, do início do `Vitral.exe --aviso` até a saída. Se a média passar de 150 ms, fica
registrado e o mensageiro vira um executável mínimo à parte (§7).

## 6. Riscos

- **Formato do Claude Code:** o `settings.json` e o que os ganchos recebem pertencem ao Claude Code e podem mudar. Os
  testes fixam o formato de hoje, e o mensageiro nunca falha para o Claude.
- **Tempo do mensageiro:** é o `Vitral.exe` inteiro carregando o .NET. A medida do §5 decide se ele fica assim.
- **Janela transparente:** o notch deixa de ter vidro acrílico, por escolha (as orelhas pedem preto sólido). A janela
  com camadas é pequena e não afeta o desempenho.

## 7. Fora desta fase

- Notificações de outros apps, que exigem identidade de pacote.
- Responder ou conversar pelo notch.
- Executável mínimo para o mensageiro, só se a medida do §5 pedir.
