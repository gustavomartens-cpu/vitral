# Formas da doca, do notch e das ilhas (aparência, fase 2)

Data: 27/10/2026. O perfil passa a escolher as formas da doca, do notch e das ilhas da barra do topo: doca flutuante
ou corrida, tamanho dos ícones e ampliação; notch encaixado, sem orelhas ou em ilha, e o tamanho dele; ilhas separadas
ou numa barra única, com respiro ou encostadas. A janela do Vitral ganha a aba "Formas", com a mesma prévia da aba
Aparência.

## 1. Objetivo e escopo

Hoje cada forma é uma constante no código (`PosicaoIlhas`, `NotchMedidas`, `FormaNotch`, os tamanhos de
`ConteudoDoca.xaml`). A fase 1 (0.10.0) deixou o perfil escolher cores e material; esta fase faz o mesmo com a
geometria.

**Sucesso:** escolher cada opção na aba Formas, ver na prévia, testar por 20 s ou aplicar, e ver a parte mudar de forma
na hora, sem janela nenhuma ficar embaixo dela (a reserva acompanha), inclusive depois de trocar de tela ou de escala.

**Fora do escopo:** posição da doca em outro lado da tela, ícones alinhados à esquerda na doca corrida, tamanho livre
(deslizante), cantos arredondados só de um lado em janela de vidro (o Windows não permite; ver §5.3).

## 2. Escolhas do Gustavo

- Ajustes soltos por parte, sem estilos prontos (B).
- Doca: formato Flutuante/Corrida (corrida com ícones centralizados), ícones Pequeno/Médio/Grande, ampliação
  ligada/desligada.
- Notch: formato Encaixado/Sem orelhas/Ilha, tamanho Compacto/Normal/Grande; o arredondamento vem do campo "Cantos" da
  fase 1, que o notch passa a seguir.
- Ilhas: disposição Separadas/Barra única, distância Com respiro/Encostadas; encostadas = todos os cantos retos.
- Doca corrida: todos os cantos retos.
- Seção própria no perfil, independente da aparência (abordagem 1). Aba própria, "Formas", depois de "Aparência".
- Todo campo tem "Não mexer" (nulo).

## 3. Modelo (Vitral.Core/Perfis)

```csharp
public enum FormatoDoca { Flutuante, Corrida }
public enum TamanhoIcones { Pequeno, Medio, Grande }
public enum FormatoNotch { Encaixado, SemOrelhas, Ilha }
public enum TamanhoNotch { Compacto, Normal, Grande }
public enum DisposicaoIlhas { Separadas, BarraUnica }
public enum DistanciaIlhas { ComRespiro, Encostadas }

public sealed record FormasDoca(FormatoDoca? Formato = null, TamanhoIcones? Icones = null, bool? Ampliacao = null);
public sealed record FormasNotch(FormatoNotch? Formato = null, TamanhoNotch? Tamanho = null);
public sealed record FormasIlhas(DisposicaoIlhas? Disposicao = null, DistanciaIlhas? Distancia = null);
public sealed record FormasPerfil(FormasDoca? Doca = null, FormasNotch? Notch = null, FormasIlhas? Ilhas = null);
```

- `Perfil.Formas` (nulo = não mexer), `ConfigApp.Formas` (a forma em vigor; nula = a de hoje) e `Retrato.Formas`.
- Seção `formas` no `perfil.json`, enums como texto (como o resto do perfil). O esquema continua 4: versão antiga ignora
  a seção. Um valor de enum novo numa versão futura exige subir o esquema.

## 4. A regra (Vitral.Core/Formas, pura)

`ResolverFormas.Resolver(FormasPerfil? formas, CantosParte cantosNotch) → ValoresFormas`. Campo nulo = o valor de hoje.
Com tudo nulo, os números são exatamente as constantes atuais (teste compara).

### 4.1 Doca — `ValoresDoca(Formato, Icone, Botao, Altura, Folga, Reserva, Ampliacao)`

| Ícones  | Ícone | Altura da doca |
|---------|-------|----------------|
| Pequeno | 24    | 38             |
| Médio   | 30    | 46 (hoje)      |
| Grande  | 38    | 56             |

- O tamanho do botão e da imagem acompanham na mesma proporção dos de hoje (botão 40×44 e imagem 24 para ícone 30).
- Flutuante: folga 8 px da borda de baixo, largura pelo conteúdo, reserva = altura + 16 (hoje 62).
- Corrida: folga 0, largura do monitor, reserva = altura, cantos retos.
- Ampliação: padrão ligada.

### 4.2 Notch — `ValoresNotch(Formato, Altura, EscalaTexto, Orelha, Sobra, Raio)`

- Altura em repouso: Compacto 28, Normal 34 (hoje), Grande 40. Escala do texto do relógio: 0,85 / 1 / 1,15.
- Orelha: 16 no Encaixado, 0 nos outros.
- Sobra (pedaço acima da borda da tela): 10 no Encaixado e no Sem orelhas (hoje); no Ilha a janela começa 6 px abaixo
  da borda (sobra −6) e os quatro cantos são arredondados.
- Raio pelo "Cantos" da fase 1 (do notch, depois do geral, padrão Redondos): Redondos 16 (hoje), Suaves 10, Retos 4. No
  Ilha, Redondos = metade da altura (pílula).
- Avisos, ficha de permissão e ficha de resposta mantêm as medidas de hoje; seguem o formato (sem orelhas; no Ilha,
  arredondadas também em cima e soltas da borda).

### 4.3 Ilhas — `ValoresIlhas(Disposicao, Topo, Borda, Altura, CantosRetos)`

- Com respiro: topo 8, borda 12 (hoje). Encostadas: topo 0, borda 0, cantos retos.
- Altura 30 (hoje) nos dois.
- Barra única: um retângulo de `borda` a `largura − borda`, no mesmo topo.

### 4.4 Reserva do topo

`max(fundo das ilhas, fundo do notch) + (8 com respiro | 0 encostadas)`. Hoje: max(38, 34) + 8 = 46.

## 5. No Vitral (App)

### 5.1 Estado e serviço

- `EstadoFormas(IControleFormas, config)` como o `EstadoAparencia`: `Atual()`, `Aplicar(f?)` (nulo = não mexer),
  `AoIniciar()`. Entra no `ServicoRetratos` e no `Aplicador` (aplicar e reverter); original sem a seção = formas de
  hoje.
- `ServicoFormas` (App, singleton): guarda os `ValoresFormas`, recalcula quando muda o "Cantos" do notch (evento
  `Mudou` da aparência) e dispara `Mudou`.

### 5.2 Quem muda

- **Geometria (Vitral.Ui/Barra):** `PosicaoIlhas.Calcular`, `Doca`, `ReservaDoca`, `Reserva` e `NotchMedidas` passam a
  receber os valores em vez das constantes. `FormaNotch.Caminho` ganha orelha 0 e o modo com os quatro cantos.
- **Doca:** altura, ícone e botão por chaves de recurso em `ConteudoDoca.xaml` (sem transformação de escala, para os
  ícones não borrarem); ampliação desligada = `Efeitos.Lupa` falso; corrida ocupa a largura do monitor.
- **Notch:** altura e texto em repouso, sobra, orelha e raio pelos valores.
- **Barra única:** uma janela nova de vidro, o fundo da barra, de lado a lado atrás das ilhas; as ilhas de mídia e de
  status ficam nas pontas sem vidro nem borda próprios; o notch fica por cima, no meio. Os painéis descem como hoje,
  6 px abaixo da ilha de que vêm.
- **Reserva:** `ReservaFaixa` usa as reservas calculadas.
- No `Mudou`, os controladores da doca e da barra usam o caminho que já têm para troca de tela: recalculam, reservam de
  novo e redesenham.

### 5.3 Cantos

O Windows arredonda os quatro cantos de uma janela de vidro ou nenhum. Doca corrida e ilhas encostadas: nenhum
(preferência de canto do DWM "não arredondar" e `CornerRadius` 0). O notch é janela transparente desenhada pelo
próprio Vitral, então pode ter qualquer forma.

### 5.4 Aba "Formas"

- Depois de "Aparência". Esquerda: a prévia grande (`PreviaAparencia`, que passa a desenhar também as formas, com as
  cores do perfil). Direita: três cartões com seletores em pílula, "Não mexer" primeiro:
  - Doca: Formato, Ícones, Ampliação.
  - Notch: Formato, Tamanho.
  - Ilhas: Disposição, Distância.
- A prévia da aba Aparência também segue as formas do perfil.
- `EditorFormasViewModel` (Vitral.Ui) com `Carregar(FormasPerfil?)` e `ParaPerfil()`, ligado ao `PrincipalViewModel`
  como o editor da aparência.

## 6. Erros e bordas

- Perfil sem `formas` (antigo ou importado): não mexe. Config sem nada: formas de hoje.
- Troca de tela, escala ou reinício do Explorer: tudo sai dos valores pelo caminho de hoje.
- Doca desligada ou barra do topo desligada no perfil: as formas ficam guardadas; quando a parte liga, já nasce com
  elas.
- Barra única com a barra do topo desligada: o fundo da barra também não aparece.
- Ilha do notch com a lista de pendências ou os avisos: mesmos comportamentos, só a forma muda.

## 7. Testes

- **Core:** cada opção vira os números de §4; tudo nulo = constantes de hoje; reservas de cima e de baixo; Cantos →
  raio do notch (e pílula no Ilha); `EstadoFormas` (aplicar, não mexer, ao iniciar); retratos e aplicador (testar,
  desfazer, original sem a seção).
- **Ui:** `PosicaoIlhas` por formato (barra única, encostadas, doca corrida, notch Ilha); `FormaNotch.Caminho` sem
  orelha e com os quatro cantos; `EditorFormasViewModel` (carregar, não mexer, para o perfil).
- **Na tela, com o Gustavo:** cada opção na doca, no notch e nas ilhas; barra única com cada formato de notch; troca
  de escala; testar e desfazer.

## 8. Riscos

- **Ordem das janelas na barra única:** o fundo precisa ficar atrás das ilhas e do notch, todos "sempre no topo". Se
  piscar ou inverter, as ilhas são reposicionadas depois do fundo.
- **Ícones da bandeja e dos apps no tamanho Grande:** o Windows entrega alguns ícones pequenos; podem sair menos
  nítidos. Conferência na tela.
- **Notch Ilha e o clique na borda de cima:** hoje o notch encosta na borda e é fácil de acertar com o mouse jogado para
  cima; solto 6 px, isso muda. Conferência com o Gustavo.
