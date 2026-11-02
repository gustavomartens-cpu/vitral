# Iniciar com o visual do Vitral (fase 2, primeira etapa)

Data: 2026-11-01 · Aprovado na conversa com o Gustavo (abordagem C, cores da doca, reserva por botão) · Prova:
`docs/superpowers/specs/2026-11-01-fase2-prova-iniciar.md`

## 1. O que é

O Menu Iniciar do Windows 11 passa a usar o vidro, a cor, a borda e os cantos da **doca** do Vitral e muda junto com o
perfil. A peça nova é um componente em C++ carregado dentro do Iniciar (StartMenuExperienceHost) pela rota externa
provada na fase 0 e na prova de 01/11. Só o Iniciar: central de notificações, configurações rápidas e busca ficam para
etapas seguintes.

**Sucesso:** com "Combinar com o Vitral" no perfil, abrir o Iniciar mostra o vidro da doca; trocar de perfil, mexer na
aparência da doca, "Testar (20 s)" e "Desfazer" mudam o Iniciar na hora; "Não mexer"/desligar e sair do Vitral (ou ele
cair) devolvem o Iniciar do Windows; quando o componente não conecta, a janela diz por quê e oferece a opção nativa.

## 2. Perfil e estado

- `Perfil.Iniciar` (nova seção, anulável = **não mexer**): `IniciarPerfil(ModoIniciar? Modo)`, com
  `enum ModoIniciar { Vitral, Windows }`. `Windows` = o visual do Windows (desliga o componente). Enum em texto, lido
  pelo `EnumTolerante` (valor futuro vira "não mexer"). O esquema continua 4: versão antiga ignora a seção.
- `ConfigApp.Iniciar` guarda o modo em vigor, no molde de `Aparencia`/`Formas`; nulo = `Windows` (hoje ninguém pinta o
  Iniciar). `EstadoIniciar` (Core) aplica, grava e reaplica ao abrir, como `EstadoAparencia`.
- Aplicar e testar o perfil passam pelo mesmo caminho das outras seções (retrato, Testar 20 s, Desfazer).

## 3. Os valores (Core, regra pura)

`ValoresIniciar.De(ValoresParte doca)` → `ValoresIniciar(bool Vidro, CorArgb Tinta, CorArgb Borda, double Raio)`:

- `Vidro` = `doca.Desfoque` (vidro com a transparência do Windows ligada).
- `Tinta` = `doca.Fundo` (com o alfa da transparência da doca; sólido usa alfa 255).
- `Borda` = `doca.Borda`.
- `Raio` = Retos 0, Suaves 4, Redondos 8 (o Iniciar do Windows usa 8).

O arquivo de cores é JSON pequeno e plano (`{"ligado":true,"vidro":true,"tinta":"#E60A0F20","borda":"#33FFFFFF","raio":8}`),
gravado de forma atômica (arquivo temporário + troca). `ligado:false` = devolver o original.

## 4. Peças

| Peça | Onde | O que faz |
|---|---|---|
| `IniciarPerfil`, `ModoIniciar`, `EstadoIniciar`, `ValoresIniciar`, `RegrasIniciar` | Vitral.Core/Iniciar | Regras puras: valores, JSON, quando conectar, mensagens |
| `VitralIniciar.dll` | src/Vitral.Iniciar (C++, clang do G:) | Dentro do Iniciar: acha `AcrylicBorder`, guarda o original, aplica e reaplica quando o arquivo muda; devolve tudo com `ligado:false` ou quando o Vitral morre |
| `ServicoIniciar` | Vitral.App/Iniciar | Copia a DLL, dá a permissão, grava o arquivo de cores, vigia o processo do Iniciar e conecta |
| Opção na aba Aparência | Vitral.Ui + JanelaPrincipal | "Iniciar": Não mexer / Combinar com o Vitral / Visual do Windows, e a linha de estado com o botão da reserva |

### 4.1 Componente (C++)

- Base: `spike/tap/IniciarProva.cpp`, reescrito para produção (sem log em pasta do spike; log em
  `%LOCALAPPDATA%\Vitral\iniciar\logs`, limitado).
- Elemento: Border `AcrylicBorder` dentro de `StartMenu.StartBlendedFlexFrame`. Vidro → `AcrylicBrush` (fundo do host,
  `TintColor` = tinta sem alfa, `TintOpacity` = alfa/255); sólido → `SolidColorBrush` opaco. `BorderBrush` e
  `CornerRadius` no mesmo elemento. Se a borda não aparecer no `AcrylicBorder`, a tarefa do plano confere
  `MainMenuHighContrastBorder` e registra a escolha.
- Arquivo de cores: caminho nos dados de inicialização (`hostpid=<n>;cores=<caminho>`). Uma thread espera mudanças na
  pasta (`FindFirstChangeNotification`) e reaplica pelo dispatcher do Iniciar.
- Uma conexão por processo (mutex `Local\VitralIniciar-<pid>`), como na fase 0. Nunca descarrega.
- Erros dentro do Iniciar são engolidos e registrados: o componente nunca pode derrubar o Iniciar.

### 4.2 Serviço (App)

- **Instalação:** a cada versão, copia `VitralIniciar.dll` do pacote para `%LOCALAPPDATA%\Vitral\iniciar\<versão>\` e
  dá leitura a `ALL APPLICATION PACKAGES` (S-1-15-2-1); a pasta do arquivo de cores e a de log ganham escrita. Pastas
  de versões antigas são apagadas quando a DLL não está em uso.
- **Conexão:** com o modo `Vitral`, a cada 5 s confere o PID do Iniciar; PID novo → conecta pela rota externa
  (`InitializeXamlDiagnosticsEx`, varredura das portas a cada 2 s, até 90 s), em segundo plano, sem travar a interface.
  O host é o próprio Vitral (`hostpid`), então o Iniciar volta ao normal quando ele sai ou cai.
- **Ao vivo:** `ServicoAparencia.Mudou` e a troca de modo regravam o arquivo de cores.
- **Não conecta:** o Windhawk aberto (um consumidor por processo), a varredura esgota, a DLL recusada. Registra no log e
  publica um estado com o motivo.

### 4.3 Janela

Na aba Aparência, abaixo das partes: "Iniciar" com Não mexer / Combinar com o Vitral / Visual do Windows. Abaixo, a
linha de estado: "Iniciar com o visual do Vitral", ou "Iniciar com o visual do Windows: <motivo>" com o botão "Usar a
cor de destaque do Windows no Iniciar", que liga a opção nativa (`ColorPrevalence`) pelo mesmo caminho dos ajustes
nativos. O botão avisa que ela também pinta a barra de tarefas do Windows.

## 5. Erros

| Situação | O que acontece |
|---|---|
| Windhawk aberto | Não conecta; motivo "o Windhawk está usando o Iniciar" |
| Varredura esgota (90 s) | Motivo "o Iniciar não aceitou o componente"; tenta de novo no próximo PID |
| Elemento `AcrylicBorder` não aparece | Componente registra "sem fundo conhecido"; o serviço lê o log e mostra "o Windows mudou o Iniciar" |
| Arquivo de cores ilegível | Componente mantém o último visual aplicado |
| Vitral sai ou cai | O componente devolve o original (espera no processo do host) |
| Antivírus remove a DLL | Cópia falha ou conexão falha; o motivo aparece |

## 6. Testes

- **Automáticos (Core e Ui):** `ValoresIniciar.De` para os temas e cantos; JSON do arquivo de cores; `EstadoIniciar`
  (aplicar, não mexer, padrão Windows); `RegrasIniciar` (quando conectar: modo, PID novo, Windhawk; mensagens);
  editor da opção (ida e volta do perfil, "Alterações não salvas").
- **Roteiro na máquina** (como `spike/rodada-iniciar.ps1`): conectar com o Iniciar reiniciado, vidro e sólido, trocar a
  aparência da doca com o Iniciar aberto, Não mexer, Testar 20 s e Desfazer, fechar o Vitral, matar o Vitral à força.
  O Gustavo confere o visual.

## 7. Fora desta etapa

Central de notificações e configurações rápidas, busca, assinatura digital da DLL, textos e ícones do Iniciar com as
cores da doca, tirar partes do Iniciar.
