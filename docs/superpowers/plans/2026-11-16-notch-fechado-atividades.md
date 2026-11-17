# Notch fechado com atividades — plano de implementação

> **Para agentes:** SUB-SKILL OBRIGATÓRIA: superpowers:subagent-driven-development (recomendada) ou
> superpowers:executing-plans, tarefa por tarefa. Os passos usam `- [ ]`.

**Objetivo:** com o notch fechado, mostrar o Claude trabalhando (spinner e número à esquerda do relógio) e o som que sai
(barras à direita, capa à esquerda sem Claude). Com o mouse em cima, o notch desce com a música. A ilha da mídia sai.

**Arquitetura:** as regras puras ficam em `Vitral.Core`/`Vitral.Ui` com testes: o sinal novo do gancho, a contagem das
sessões (`AtividadeClaude`), a escolha dos lados (`LadosNotch`), as bandas por Goertzel (`BandasSom`) e a cor da capa.
O `Vitral.App` liga tudo: a captura em loopback (`CapturaSom`, interop COM própria), os lados e o cartão no
`JanelaNotch`, e o `ControleBarra` sem a ilha da mídia.

**Tecnologia:** WPF .NET 10, CommunityToolkit.Mvvm, interop COM do WASAPI escrita à mão (sem pacote novo).

**Spec:** `docs/superpowers/specs/2026-11-16-notch-fechado-atividades-design.md`. Prévia:
`docs/superpowers/mockups/2026-11-16-notch-fechado.html`.

## Restrições gerais

- Código, comentários, nomes e documentos em português.
- Nenhum pacote novo. Nada copiado dos projetos da pesquisa (vários são GPL): só a técnica.
- Largura do notch muda pela mola (`src/Vitral.Ui/Tema/Mola.cs`): abrir `JeitoMola.Abrir`, fechar `JeitoMola.Fechar`.
- `Movimento.Ligado` desligado: glifo fixo `✻` e barras paradas.
- Spinner: glifos `· ✢ ✳ ✻ ✽ ✻ ✳ ✢`, ~120 ms cada, na cor do Claude (`CorClaude` de `JanelaNotch`).
- Esperando = `Ambar` do tema; check terminou some em 3 s; expiração sem mudar a transcrição: 30 min.
- Barras: 5 bandas, centros 60, 250, 1000, 4000 e 10000 Hz; janelas de ~20 ms; ~30 atualizações/s.
- Gancho `UserPromptSubmit` com `"async": true`.
- Testes e build: `powershell.exe -NoProfile -Command '. tools/env.ps1; dotnet build --nologo -v q; dotnet test --nologo -v q --no-build'`
  (feche o Vitral de desenvolvimento antes). Um filtro: acrescente `--filter "FullyQualifiedName~NomeDaClasse"` ao test.

## Foco da revisão

1. Ctrl+C no meio do turno: o spinner some na conferência seguinte (marca de interrupção), sem check.
2. Duas sessões na mesma pasta com `session_id` diferentes: contam 2; sem `session_id`, a pasta é a chave.
3. Aparelho de saída padrão trocado durante a música (fone Bluetooth conecta): a captura reinicia sozinha, sem exceção
   no log a cada quadro.
4. A sessão de mídia some com o notch descido: o cartão recolhe para o hover sem mídia, sem ficar com a linha vazia.
5. Notch escondido (tela cheia): a captura para; avisos `Comecou`/`Terminou` continuam contando, e o notch mostra o
   estado certo quando volta.

---

### Tarefa 1: Gancho `UserPromptSubmit` e aviso `Comecou`

**Arquivos:**
- Modificar: `src/Vitral.Core/Claude/GanchosClaude.cs`, `src/Vitral.Core/Claude/AvisoClaude.cs`,
  `src/Vitral.Core/Claude/TurnoClaude.cs`, `src/Vitral.App/Claude/Mensageiro.cs`
- Testes: `tests/Vitral.Core.Tests/GanchosClaudeTests.cs`, `tests/Vitral.Core.Tests/AvisoClaudeTests.cs`

**Interfaces:**
- Produz: `TipoAviso.Comecou`; `AvisoClaude.Transcricao` (string?, o `transcript_path`); `AvisoClaude.TipoNotificacao`
  (string?, o `notification_type`); `AvisoClaude.ChaveSessao` (`SessaoId ?? Sessao`); `TurnoClaude.Interrompido(string
  trechoFim) -> bool`; `GanchosClaude.Eventos` com `"UserPromptSubmit"`.

- [ ] **Passo 1: testes que falham**

```csharp
[Fact] public void Ligar_poe_UserPromptSubmit_assincrono()
{
    var json = JsonNode.Parse(GanchosClaude.Ligar("{}", "\"C:\\\\V\\\\Vitral.exe\" --aviso"))!;
    var item = json["hooks"]!["UserPromptSubmit"]![0]!["hooks"]![0]!;
    Assert.Equal(true, (bool)item["async"]!);
    Assert.True(GanchosClaude.EstaLigado(json.ToJsonString()));
}
[Fact] public void Atualizar_completa_quem_tem_os_tres_de_antes() // os 3 eventos de hoje, em dia, sem o UserPromptSubmit
{   var antigo = /* Ligar de hoje sem a entrada UserPromptSubmit */;
    Assert.Contains("UserPromptSubmit", GanchosClaude.Atualizar(antigo)); }
[Fact] public void Desligar_tira_o_UserPromptSubmit() { /* Ligar → Desligar → sem "UserPromptSubmit" */ }

[Fact] public void DeGancho_UserPromptSubmit_vira_Comecou_sem_ler_a_transcricao()
{
    var a = AvisoClaude.DeGancho("""{"hook_event_name":"UserPromptSubmit","cwd":"C:\\p\\vitral","session_id":"s1","transcript_path":"C:\\t.jsonl","prompt":"oi"}""",
        _ => throw new InvalidOperationException("não lê"));
    Assert.Equal(TipoAviso.Comecou, a!.Tipo);
    Assert.Equal("C:\\t.jsonl", a.Transcricao);
    Assert.Equal("s1", a.ChaveSessao);
    Assert.Equal("", a.Texto);
}
[Fact] public void DeGancho_Notification_guarda_o_tipo() // "notification_type":"idle_prompt" → TipoNotificacao == "idle_prompt"
[Fact] public void Linha_de_ida_e_volta_leva_Transcricao_e_TipoNotificacao()
[Fact] public void Interrompido_ve_a_marca_no_fim() // última linha user com texto "[Request interrupted by user]" → true;
                                                    // marca seguida de resposta do assistente → false
```

- [ ] **Passo 2:** rodar com `--filter "FullyQualifiedName~GanchosClaudeTests|FullyQualifiedName~AvisoClaudeTests"`; falham.
- [ ] **Passo 3: implementar.**
  - `GanchosClaude`: `Eventos` ganha `"UserPromptSubmit"`, ligado com `item["async"] = true`. `EstaLigado` exige os
    quatro. `Atualizar` também devolve `Ligar(texto, comando)` quando os três de hoje são nossos e em dia e falta o
    `UserPromptSubmit` (mantendo o caso do tempo antigo da permissão).
  - `AvisoClaude.DeGancho`: `"UserPromptSubmit"` → `Comecou`, sem chamar `lerFimDoArquivo`; `Transcricao` vem de
    `transcript_path` em todos os tipos; `TipoNotificacao` de `notification_type`. `Linha` ganha os dois campos (com
    padrão nulo, para linhas antigas).
  - `TurnoClaude.Interrompido`: a última linha JSON de `type` `user` ou `assistant` no trecho é de usuário com texto
    começando por `[Request interrupted by user`.
  - `Mensageiro`: `Comecou` vai pelo canal como os outros avisos, prazo 1,5 s, sem ler janelas nem transcrição.
- [ ] **Passo 4:** os mesmos testes passam; a suíte inteira passa.
- [ ] **Passo 5:** commit `feat: gancho UserPromptSubmit e aviso Comecou (R1)`.

### Tarefa 2: Contagem das sessões e escolha dos lados

**Arquivos:**
- Criar: `src/Vitral.Ui/Barra/AtividadeClaude.cs`, `src/Vitral.Ui/Barra/LadosNotch.cs`
- Teste: `tests/Vitral.Ui.Tests/AtividadeClaudeTests.cs`

**Interfaces:**
- Consome: Tarefa 1 (`TipoAviso.Comecou`, `ChaveSessao`, `Transcricao`, `TipoNotificacao`, `DeOutraOrigem`).
- Produz:

```csharp
public readonly record struct ResumoAtividade(int Rodando, int Esperando, bool Terminou);
public readonly record struct LeituraTranscricao(DateTime UltimaEscrita, bool Interrompida);

public sealed class AtividadeClaude(Func<DateTime> agora)
{
    public const double SegundosCheck = 3, MinutosSemMudar = 30, FolgaRetomou = 2;
    public void Chegou(AvisoClaude aviso);
    public void PedidoAbriu(string chave);
    public void PedidoSaiu(string chave);
    public void Conferir(Func<string, LeituraTranscricao?> ler); // também apaga o check vencido
    public ResumoAtividade Resumo { get; }
    public bool PrecisaConferir { get; } // há sessão rodando/esperando ou check aceso
    public event Action? Mudou;
}

public enum LadoEsquerdo { Nada, Esperando, Rodando, Terminou, Capa }
public readonly record struct Lados(LadoEsquerdo Esquerdo, int Numero, bool Barras)
{ public bool Algum => Esquerdo != LadoEsquerdo.Nada || Barras; }
public static class LadosNotch
{
    public const double LarguraLado = 40;
    public static Lados Decidir(ResumoAtividade a, EstadoMidia? midia, bool midiaLigada);
}
```

- [ ] **Passo 1: testes que falham** (relógio falso `agora`):

```csharp
[Fact] void Comecou_conta_rodando()                 // Comecou s1 → Resumo == (1,0,false)
[Fact] void Duas_sessoes_na_mesma_pasta_contam_duas() // Comecou s1 e s2, mesma Sessao → Rodando == 2
[Fact] void Sem_SessaoId_a_pasta_e_a_chave()         // dois Comecou sem SessaoId, mesma pasta → 1
[Fact] void Stop_do_Gustavo_acende_o_check_por_3s()  // Comecou, Terminou → (0,0,true); +3,1 s e Conferir → Terminou false
[Fact] void Stop_de_outra_origem_tira_sem_check()    // Origem "task_notification" → (0,0,false)
[Fact] void Stop_sem_Comecou_nao_acende()            // Terminou de sessão desconhecida → (0,0,false)
[Fact] void Notification_de_pedido_pinta_esperando() // Comecou, Esperando("permission_prompt") → (0,1,false)
[Fact] void Ocioso_depois_do_Stop_nao_pinta()        // Comecou, Terminou, Esperando("idle_prompt") → Esperando == 0
[Fact] void Pedido_aberto_e_fechado()                // Comecou, PedidoAbriu → (0,1); PedidoSaiu → (1,0)
[Fact] void Transcricao_crescendo_tira_da_espera()   // Esperando sem pedido; Conferir com UltimaEscrita > espera+2 s → Rodando
[Fact] void Interrompida_some_sem_check()            // Conferir com Interrompida → (0,0,false)
[Fact] void Trinta_minutos_parada_some()             // UltimaEscrita há 30 min e 1 s → some; há 29 min → fica
[Fact] void Mudou_dispara_so_quando_o_resumo_muda()
// LadosNotch
[Fact] void Esperando_ganha_de_rodando()     // (2,1,false) → Esperando, Numero 1
[Fact] void Rodando_com_musica()             // (2,0,false) + mídia → Rodando, 2, Barras true
[Fact] void So_musica_mostra_capa()          // (0,0,false) + mídia → Capa, Barras true
[Fact] void Musica_desligada_no_perfil()     // midiaLigada false → sem Capa nem Barras
[Fact] void Terminou_ganha_da_capa()         // (0,0,true) + mídia → Terminou
[Fact] void Nada()                           // → Nada, Barras false, Algum false
```

- [ ] **Passo 2:** rodar com `--filter "FullyQualifiedName~AtividadeClaudeTests"`; falham.
- [ ] **Passo 3: implementar.** Estado por chave: `Rodando` (com `Transcricao` e hora da última escrita vista),
  `Esperando` (hora em que entrou, pedidos abertos). `Esperando` vindo de `Notification` só vale para sessão já
  rodando e com `TipoNotificacao != "idle_prompt"`. `PedidoAbriu` cria a sessão se não existe (pedido sem
  `Comecou`, Vitral aberto no meio do turno). `Resumo.Esperando` conta sessões; `Rodando` conta as que não esperam.
- [ ] **Passo 4:** passam; suíte inteira passa.
- [ ] **Passo 5:** commit `feat: contagem das sessões do Claude e lados do notch (R1, R8)`.

### Tarefa 3: Bandas do som e cor da capa

**Arquivos:**
- Criar: `src/Vitral.Ui/Barra/BandasSom.cs`, `src/Vitral.Ui/Barra/CorDaCapa.cs`
- Teste: `tests/Vitral.Ui.Tests/BandasSomTests.cs`

**Interfaces:**

```csharp
public sealed class BandasSom
{
    public static readonly double[] Centros = [60, 250, 1000, 4000, 10000];
    public static double Goertzel(ReadOnlySpan<float> amostras, double frequencia, int taxa); // magnitude 0..1 para seno de amplitude 1
    public IReadOnlyList<double> Processar(ReadOnlySpan<float> mono, int taxa); // 5 valores 0..1, suavizados
    public void Zerar();
}
public static class CorDaCapa
{
    public static (byte R, byte G, byte B) Media(ReadOnlySpan<byte> bgra);           // ignora pixels com alfa < 128
    public static (byte R, byte G, byte B) Clarear((byte R, byte G, byte B) cor);    // luminância relativa ≥ 0,45
}
```

- [ ] **Passo 1: testes que falham**

```csharp
[Fact] void Goertzel_acha_o_seno() // seno 1 kHz, amplitude 1, 48 kHz, 960 amostras: Goertzel(1000) > 0,9; Goertzel(60) < 0,1
[Fact] void Seno_de_1kHz_acende_a_banda_do_meio() // Processar repetido 10×: índice 2 é o maior e > 0,5
[Fact] void Silencio_zera()                       // só zeros desde o começo → todas < 0,01
[Fact] void Desce_devagar()                        // som alto e depois 1 janela de silêncio → banda ainda > 0,3; 30 janelas → < 0,05
[Fact] void Som_baixo_ainda_aparece()              // seno 1 kHz amplitude 0,02, 100 janelas → banda 2 > 0,3 (ganho automático)
[Fact] void Media_de_dois_pixels()                 // BGRA vermelho + azul → (127,0,127) ±1
[Fact] void Clarear_cor_escura()                   // (20,10,40) → luminância ≥ 0,45, mesmo matiz (B continua o maior)
[Fact] void Clarear_mantem_cor_clara()             // (230,180,120) → igual
```

- [ ] **Passo 2:** `--filter "FullyQualifiedName~BandasSomTests"`; falham.
- [ ] **Passo 3: implementar.** Goertzel clássico normalizado por N/2. `Processar`: subida imediata, descida
  multiplicando por ~0,85 por janela; ganho automático seguindo o pico das 5 bandas com subida rápida e descida lenta
  (~5 s até a metade), ganho máximo limitado para silêncio não virar ruído (abaixo de amplitude 0,001 → zero).
- [ ] **Passo 4:** passam.
- [ ] **Passo 5:** commit `feat: bandas do som por Goertzel e cor da capa (R3)`.

### Tarefa 4: Captura em loopback

**Arquivos:**
- Criar: `src/Vitral.App/Barra/CapturaSom.cs` (interop `IAudioClient`, `IAudioCaptureClient` junto da de `CoreAudio.cs`)
- Modificar: `src/Vitral.App/Barra/CoreAudio.cs` (só se precisar expor `IMMDevice.Activate`)

**Interfaces:**
- Consome: `BandasSom` (Tarefa 3).
- Produz: `sealed class CapturaSom : IDisposable { void Ligar(); void Desligar(); event Action<IReadOnlyList<double>>? Bandas; }`
  — `Bandas` dispara na thread da interface ~30×/s enquanto ligada.

- [ ] **Passo 1: implementar.** Aparelho de saída padrão (`eRender`, `eConsole`), `IAudioClient.Initialize` em modo
  compartilhado com `AUDCLNT_STREAMFLAGS_LOOPBACK`, formato de `GetMixFormat` (float 32; outro formato → log uma vez
  e não liga). Thread própria lendo pacotes a cada ~10 ms, downmix para mono, janelas de 20 ms para `BandasSom`, envio
  à interface no máximo a cada 33 ms. Pacote com `AUDCLNT_BUFFERFLAGS_SILENT` conta como zeros. Troca do aparelho
  padrão (`IMMNotificationClient.OnDefaultDeviceChanged`, já usado em `CoreAudio.cs`) ou erro
  `AUDCLNT_E_DEVICE_INVALIDATED`: reabre no aparelho novo, com um log por troca. `Desligar` para a thread e solta o
  COM. Sem exceção saindo da thread.
- [ ] **Passo 2: conferir à mão.** Uma janela de teste não entra no repositório: ligue pelo próprio notch na
  Tarefa 5. Aqui só o build: `dotnet build` sem avisos novos.
- [ ] **Passo 3:** commit `feat: captura do som que sai em loopback (R3)`.

### Tarefa 5: Os lados no notch fechado

**Arquivos:**
- Modificar: `src/Vitral.App/Barra/JanelaNotch.cs`, `src/Vitral.App/Barra/ControleBarra.cs`,
  `src/Vitral.Ui/Barra/FormaNotch.cs` (`NotchMedidas`)

**Interfaces:**
- Consome: Tarefas 1–4.
- Produz: `JanelaNotch.Atividade` (`AtividadeClaude`, setter), `JanelaNotch.Midia` (`IFonteMidia?`, setter),
  `JanelaNotch.MidiaLigada` (bool).

- [ ] **Passo 1: implementar.**
  - `ControleBarra` cria um `AtividadeClaude` (vive enquanto a barra existe, não com a janela do notch). `Avisar`
    passa todo aviso para `Atividade.Chegou` **antes** de decidir se o notch está visível; `Comecou` não vai ao
    `JanelaNotch.Avisar`. `Pedir` chama `PedidoAbriu(chave)` antes e `PedidoSaiu(chave)` depois do `await`.
  - Conferência das transcrições a cada 10 s num `DispatcherTimer` ligado só com `PrecisaConferir`; a leitura do
    arquivo (data da última escrita e os últimos ~8 KB para `TurnoClaude.Interrompido`) fora da thread da interface.
  - `JanelaNotch`: um lado à esquerda e outro à direita do relógio, dentro de `_linhaRelogio`, cada um com
    `LadosNotch.LarguraLado`. Esquerdo: spinner (`TextBlock` com os glifos, `CorClaude`) + número; ponto e número em
    `Ambar`; check verde; capa 18 × 18 cantos 4. Direito: 5 retângulos de 3 px, cantos 1,5, altura 3–16 px, cor de
    `CorDaCapa.Clarear(Media(capa))` (cinza `TextoSecundario` sem capa). Música pausada: alturas fixas baixas (3 px).
  - Largura no modo Normal: `LarguraNormal + (lados.Algum ? 2 × LarguraLado : 0)` somada ao que já existe, pelo mesmo
    `Mover` (mola). O `ExtraLembrete` continua.
  - `CapturaSom` ligada só com mídia `Tocando`, `MidiaLigada` e notch visível; `Bandas` alimenta as alturas.
    `Movimento.Ligado` falso: glifo `✻` fixo e barras em altura média fixa (sem captura).
  - Spinner e check animados por um `DispatcherTimer` de 120 ms ligado só quando o lado esquerdo é `Rodando`.
  - `LarguraNormal` continua sendo só o relógio (as ilhas se posicionam por ela; os lados crescem por dentro da janela
    fixa, centrados).
- [ ] **Passo 2:** build e suíte inteira passam.
- [ ] **Passo 3: conferir à mão** com o Vitral de desenvolvimento (ver Tarefa 7): mandar uma mensagem num Claude
  Code → spinner e 1; Ctrl+C → some em até 10 s; música → barras; pausar → barras paradas; sem Claude → capa.
- [ ] **Passo 4:** commit `feat: Claude trabalhando e som no notch fechado (R1, R3)`.

### Tarefa 6: O notch desce com a música; a ilha da mídia sai

**Arquivos:**
- Modificar: `src/Vitral.App/Barra/JanelaNotch.cs`, `src/Vitral.App/Barra/ControleBarra.cs`,
  `src/Vitral.Ui/Barra/PosicaoIlhas.cs`, `src/Vitral.Ui/Barra/FormaNotch.cs`, a tela das ilhas na aba da barra do topo
  (rótulo da opção "Mídia")
- Remover: `src/Vitral.App/Barra/IlhaMidia.xaml(.cs)`, `src/Vitral.App/Barra/PainelMidia.xaml(.cs)` e
  `IlhaMidiaViewModel` se nada mais usar (o `PainelMidiaViewModel` fica, o cartão usa)
- Testes: `tests/Vitral.Ui.Tests` (os de `PosicaoIlhas.ComFundo` e os que citam a ilha da mídia)

**Interfaces:**
- Consome: Tarefa 5. `PainelMidiaViewModel` (título, subtítulo, capa, tocando, progresso) e `IFonteMidia`
  (anterior, tocar/pausar, próxima, trazer o app).
- Produz: `NotchMedidas.AlturaLinhaMidia = 76`; `PosicaoIlhas.ComFundo(bool barraUnica, bool temStatus)`.

- [ ] **Passo 1: testes que falham.** `ComFundo(barraUnica: true, temStatus: false) == false`;
  `ComFundo(true, true) == true`; `ComFundo(false, true) == false`. Ajustar ou apagar os testes da ilha da mídia que
  deixam de valer (listar no commit quais).
- [ ] **Passo 2:** falham (assinatura nova).
- [ ] **Passo 3: implementar.**
  - Com `_estado.Laterais` e mídia presente e ligada: a linha de cima é a de hoje (laterais e relógio, com os lados de
    atividade escondidos enquanto as laterais estão abertas) e embaixo uma linha da música, ocupando as 3 colunas:
    capa 46 × 46 cantos 8, título (uma linha, "…"), subtítulo, barras, botões ⏮ ⏯ ⏭ (glifos `E892`, `E768`/`E769`,
    `E893` da `FonteIcones`) e a linha do tempo fina embaixo quando `MostrarProgresso`. Clique no título: `TrazerApp`.
  - Altura = `Formas.Altura + AlturaLinhaMidia`; largura = a das laterais (`LarguraNormal + 2 × LarguraLateral`);
    mesmo `Mover`, centrado (a janela é fixa e centrada; o corpo cresce para os dois lados). Raio:
    `NotchMedidas.RaioAviso`. A linha entra com o V3 (`Trocar(..., animar)`).
  - A mídia some com o notch descido: volta à altura das laterais.
  - `ControleBarra`: tira `_midia`, `_vmMidia`, `AoMudarMidia`, `AtualizarVisibilidadeMidia`, `MouseNaMidia`, o
    `Painel.Midia` no coordenador e o `PassarMouse` da mídia. `IlhasBarra.Midia` passa a ser `notch.MidiaLigada`.
    `Calcular` recebe largura 0 para a mídia.
  - Rótulo da opção na tela: "Música no notch".
- [ ] **Passo 4:** build e suíte inteira passam.
- [ ] **Passo 5: conferir à mão:** mouse parado no notch com música → desce centrado, sino inteiro; botões funcionam;
  mídia some com ele descido → recolhe; sem música → hover de hoje; barra única sem status → sem faixa.
- [ ] **Passo 6:** commit `feat: notch desce com a música e a ilha da mídia sai (R3)`.

### Tarefa 7: Versão, notas e entrega

**Arquivos:**
- Modificar: `src/Vitral.App/Vitral.App.csproj` (`<Version>0.19.0</Version>`), `docs/ideias-pendentes.md`
- Criar: `docs/notas/0.19.0.md` (no formato das anteriores)

- [ ] **Passo 1:** notas da versão; em `ideias-pendentes.md`, o bloco 3 riscado como feito na 0.19.0 e a linha de
  "Já feito" com a 0.19.0. Ficou para depois: ordem configurável (R8), nome da música fechado.
- [ ] **Passo 2:** build e suíte inteira passam.
- [ ] **Passo 3:** o Gustavo vê funcionando: fechar o Vitral instalado, abrir o `Vitral.exe` de
  `src\Vitral.App\bin\Debug\net10.0-windows10.0.19041.0\`, ligar os ganchos pelas Configurações (o `Atualizar` só
  regrava o comando do instalado) e repetir as conferências das Tarefas 5 e 6.
- [ ] **Passo 4:** commit `chore: versão 0.19.0 (notch fechado com atividades)` e PR contra `main`, avisando que
  depende da 0.18.0 ainda não juntada.
