# Painel de controles na barra do topo

Data: 2026-11-03 · Aprovado na conversa com o Gustavo · Prova: `docs/superpowers/specs/2026-11-02-prova-controles.md`

## 1. O que é

O que faltava do Win+A entra na barra do topo do Vitral: um painel de controles que desce da ilha de status, com
Bluetooth (e os aparelhos), Modo avião, Não perturbe, Luz noturna e Brilho; e o microfone no painel de volume que já
existe. O Win+A do Windows continua como está.

**Sucesso:** cada interruptor liga, desliga e mostra o estado certo (também quando mudado fora do Vitral); o brilho
muda a tela do notebook e os monitores que aceitam; o fone conecta pelo clique; o microfone silencia e troca; o "Não
perturbe" também para o notch de descer com as notificações dos apps.

Fora: o ícone de "modo avião" do Windows (o Vitral desliga os rádios, não liga o interruptor do Windows), horário e
intensidade da luz noturna, hotspot, projetar, acessibilidade.

## 2. Onde fica

- Ilha de status: um ícone novo (glifo de controles, como o do Win+A) à esquerda do Wi-Fi; o clique abre o
  `Painel.Controles`, alinhado à direita como o do Wi-Fi.
- Painel de volume (`PainelVolume`): seção nova "Microfone" abaixo das saídas.

## 3. O painel de controles

- **Grade de interruptores** (2 colunas): Bluetooth, Modo avião, Não perturbe, Luz noturna. Cada um com ícone, nome e
  estado (aceso = ligado). Um interruptor que o Windows recusa fica apagado com a dica do motivo.
- **Brilho:** um deslizante por tela que aceita: a do notebook (WMI `WmiMonitorBrightness`/`WmiSetBrightness`) e os
  monitores externos com DDC/CI (`dxva2` `SetMonitorBrightness`), com o nome do monitor. Tela sem controle não aparece.
- **Aparelhos Bluetooth:** a seta ao lado do interruptor abre a lista dos pareados (ícone pelo tipo: fone/caixa,
  teclado, mouse, celular, controle, outro) com "conectado"/"desconectado". Clique num fone ou caixa (os que têm saída
  de áudio Bluetooth): conecta ou desconecta (§4.2); nos outros: abre `ms-settings:bluetooth`.
- **Aviso no Bluetooth e no Modo avião:** quando há teclado ou mouse Bluetooth conectado, o botão mostra a dica
  "O teclado e o mouse Bluetooth desconectam enquanto estiver desligado".
- O estado é relido ao abrir o painel (e depois de cada clique), para acompanhar mudanças feitas fora do Vitral.

## 4. Como cada controle funciona

### 4.1 Rádios

`Windows.Devices.Radios.Radio` (`RequestAccessAsync` uma vez; `SetStateAsync`). Bluetooth = o rádio do tipo
`Bluetooth`. **Modo avião ligado** = guarda o estado de cada rádio no config (`ConfigApp.RadiosAntesDoAviao`) e desliga
todos; **desligado** = devolve cada rádio ao guardado e apaga a lista. "Modo avião" aparece ligado quando há lista
guardada.

### 4.2 Fones e caixas Bluetooth

O caminho da prova (ToothTray): cada saída de áudio Bluetooth tem um filtro KS (`BTHENUM`); `IKsControl.KsProperty`
com `KSPROPSETID_BtAudio` (`7FA06C40-B8F6-4C7E-8556-E8C33A12E54D`), id 0 = conectar, 1 = desconectar. O aparelho é
achado pelo endereço Bluetooth no identificador do filtro (`&<12 hex>_c`). **Ainda não provado num fone ligado** (o
Echo Dot não conectou): a Task 1 do plano prova com um fone; se não conectar, o clique abre `ms-settings:bluetooth`
para todos.

### 4.3 Não perturbe

COM não documentado `IQuietHoursSettings` (CLSID `f53321fa-34f8-4b7f-b9a3-361877cb94cf`, IID
`6BFF4732-81EC-4FFB-AE67-B6C1BC29631F`): perfil `Microsoft.QuietHoursProfile.PriorityOnly` = ligado,
`...Unrestricted` = desligado (provado). Ligado: o `EstadoNotch` não desce com as notificações dos apps (elas ficam no
painel do sino); os avisos do Claude e da agenda continuam.

### 4.4 Luz noturna

O blob `Data` em
`HKCU\...\CloudStore\Store\DefaultAccount\Current\default$windows.data.bluelightreduction.bluelightreductionstate\windows.data.bluelightreduction.bluelightreductionstate`:
byte 18 = `0x15` ligada, `0x13` desligada; ligar insere `0x10 0x00` na posição 23, desligar tira; o carimbo nos bytes
10..14 avança. Provado no registro; o efeito na tela é conferido com o Gustavo. Formato desconhecido (tamanho curto,
byte 18 com outro valor): o interruptor fica apagado com "Abra a luz noturna nas Configurações do Windows".

### 4.5 Microfone

O Core Audio que o painel de volume já usa, para o fluxo de entrada (`eCapture`): mudo do padrão
(`IAudioEndpointVolume.SetMute`) e a troca do padrão (`IPolicyConfig`, como nas saídas).

## 5. Peças

| Peça | Onde |
|---|---|
| `RegrasControles` (modo avião guarda/devolve, tipo do aparelho, aviso do teclado, formato da luz noturna) | Vitral.Core/Controles |
| `PainelControlesViewModel`, microfone no `PainelVolumeViewModel`, "Não perturbe" no `EstadoNotch` | Vitral.Ui/Barra |
| `FonteRadios`, `FonteBluetooth` (lista + fones), `FonteNaoPerturbe`, `FonteLuzNoturna`, `FonteBrilho`, entrada no `FonteDispositivosAudioWindows` | Vitral.App/Controles |
| `PainelControles` (WPF), ícone na `IlhaStatus` | Vitral.App/Barra |

## 6. Testes

- Automáticos: modo avião (guarda e devolve; liga quando há lista), tipo do aparelho pela classe Bluetooth, aviso do
  teclado, luz noturna (ligar/desligar o blob e o formato desconhecido, com blobs de exemplo), "Não perturbe" calando
  as notificações no notch e não o Claude, modelo do painel (estados, cliques, brilho), microfone no modelo do volume.
- Na máquina, com o Gustavo: cada interruptor indo e voltando, o efeito da luz noturna, o brilho no notebook e no
  monitor externo, o microfone, um fone conectando e desconectando.
