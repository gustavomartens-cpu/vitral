# Prova dos controles do Win+A (resultado)

Data: 2026-11-02 · Windows 11 Home 25H2, build 26200.9550 · Código descartável em `spike/controles`

| Controle | Caminho | Resultado |
|---|---|---|
| Rádios (Wi-Fi, Bluetooth) | `Windows.Devices.Radios.Radio` (`RequestAccessAsync`, `SetStateAsync`) | **Funciona.** Acesso `Allowed` num exe comum; desligar e religar o Bluetooth voltou ao estado de antes |
| Modo avião | Todos os rádios desligados pela mesma API e devolvidos como estavam | **Funciona** (Bluetooth e Wi-Fi desligaram e voltaram). Não liga o ícone de "modo avião" do Windows: é o efeito, não o interruptor |
| Aparelhos Bluetooth | `DeviceInformation` + `BluetoothDevice`/`BluetoothLEDevice` (`ConnectionStatus`) | **Lista funciona**: 9 aparelhos pareados (fones JBL e QCY, Echo Dot, celular, teclado BT5.0 KB, controle 8BitDo) com o estado |
| Conectar/desconectar aparelho | Sem API oficial | **Não testado.** Fones têm um caminho conhecido pelo áudio do Windows (`IKsControl`, o mesmo do ToothTray); mouse e teclado, não |
| Não perturbe | COM não documentado `IQuietHoursSettings` (CLSID f53321fa-…) | **Funciona.** `Unrestricted` → `PriorityOnly` → de volta |
| Luz noturna | Blob do CloudStore (`bluelightreductionstate`), byte 18 = 0x15 ligada | **O registro muda e volta.** O efeito na tela não foi conferido (a captura de tela não mostra o filtro de cor) |
| Brilho do notebook | WMI `WmiMonitorBrightness` | **Funciona** (40) |
| Brilho de monitor externo | DDC/CI (`dxva2`) | Um dos dois monitores externos responde (40, de 0 a 100); o outro não tem DDC/CI |
| Microfone | O áudio do Windows que o painel de volume já usa | Não testado (risco baixo) |

**Efeito colateral visto:** desligar o Bluetooth derruba o teclado BT5.0 KB; ao religar, ele aparece desconectado até
voltar sozinho (teclados Bluetooth costumam reconectar ao apertar uma tecla). O painel precisa avisar isso no botão do
Bluetooth e do modo avião.
