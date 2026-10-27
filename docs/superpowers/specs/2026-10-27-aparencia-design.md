# Aparência da doca, do notch e das ilhas (fase 1)

Data: 27/10/2026. O perfil passa a escolher a aparência da doca, do notch e das ilhas da barra do topo (com os painéis
que descem delas): sete temas prontos, um "personalizar" opcional e, se quiser, cada parte com a sua. A janela do
Vitral ganha a aba "Aparência" com uma prévia sobre o papel de parede.

## 1. Objetivo e escopo

Hoje o visual está fixo no código: ilhas e doca em vidro acrílico azul-noite com destaque âmbar e cantos de ~8 px, e o
notch preto sólido. O Gustavo quer que quem usa o Vitral escolha o estilo, como já escolhe o da barra de tarefas do
Windows.

**Sucesso:** trocar de tema na aba Aparência, ver na prévia como fica, testar por 20 s ou aplicar, e ver a doca, o
notch, as ilhas, os painéis e as fichas do notch mudarem juntos, legíveis em qualquer combinação.

**Fase 2 (fora desta):** formas — doca flutuante ou corrida, tamanho e ampliação dos ícones, tamanho/arredondamento/
orelhas do notch, ilhas separadas ou numa barra única, encostadas ou com respiro.

**Também fora:** a janela principal do Vitral (continua como está) e os painéis do Windows.

## 2. Escolhas do Gustavo

| Item | Escolha |
|------|---------|
| Tipo de customização | temas prontos + personalizar (C), e formas numa fase 2 |
| Relação com o Windows | tema "Automático" segue o modo e a cor de destaque do Windows; os outros são fixos |
| Granularidade | uma aparência para as três partes, com "diferenciar" opcional em cada uma |
| Temas | os sete de `temas.html` |
| Abordagem | um dicionário central de aparência (a regra calcula, todos leem do mesmo lugar) |
| Aba | a de `aba-aparencia.html`: grade de temas, prévia grande, personalizar e diferenciar à direita |

## 3. Modelo (Vitral.Core/Perfis)

```csharp
public enum TemaAparencia { Automatico, VidroEscuro, PretoSolido, Claro, VidroColorido, Grafite, Cristal }
public enum MaterialParte { Vidro, Solido }
public enum CantosParte { Retos, Suaves, Redondos }

public sealed record AjustesAparencia(MaterialParte? Material = null, string? CorFundo = null, int? Transparencia = null,
    string? CorDestaque = null, CantosParte? Cantos = null);

public sealed record AparenciaPerfil(TemaAparencia Tema, AjustesAparencia? Geral = null,
    AjustesAparencia? Doca = null, AjustesAparencia? Notch = null, AjustesAparencia? Ilhas = null);
```

- `Perfil` ganha `AparenciaPerfil? Aparencia = null`. Nulo é "não mexer", como nas outras seções.
- `Transparencia` vai de 0 (opaco) a 100 (invisível); fora disso é recortada para 0–100.
- `CorFundo` e `CorDestaque` em `#RRGGBB`. Inválida: o campo é ignorado.
- Os campos de `AjustesAparencia` nulos valem o que vem de baixo (tema, depois o geral).
- A aparência atual fica no `config.json` (`EstadoAparencia`, como `EstadoBarra` e `EstadoDoca`); sem nada guardado,
  vale o Vidro escuro.

## 4. A regra (Vitral.Core/Aparencia, pura)

`ResolverAparencia.Resolver(AparenciaPerfil? aparencia, VisualWindows windows) → ValoresAparencia`

- `VisualWindows(bool ModoEscuro, string CorDestaque, bool Transparencia)`: lido do Windows pelo App.
- `ValoresAparencia(ValoresParte Doca, ValoresParte Notch, ValoresParte Ilhas)`.
- `ValoresParte(MaterialParte Material, Cor Fundo, Cor Texto, Cor TextoSecundario, Cor Borda, Cor Destaque,
  CantosParte Cantos)`, com `Cor(byte A, byte R, byte G, byte B)`.

### 4.1 Os temas (valores-base)

Para doca e ilhas; o notch tem os seus. Opacidade é 100 − transparência.

| Tema | Material | Fundo | Opacidade | Destaque | Notch |
|------|----------|-------|-----------|----------|-------|
| Vidro escuro | Vidro | `#0A0F20` | 72% | `#F2B544` | sólido `#0B0C10` |
| Automático (Windows escuro) | Vidro | `#141826` | 55% | destaque do Windows | sólido `#0B0C10` |
| Automático (Windows claro) | Vidro | `#FAFAFC` | 72% | destaque do Windows | sólido `#F4F4F6` |
| Preto sólido | Sólido | `#0B0C10` | 100% | `#F2F2F2` | sólido `#0B0C10` |
| Claro | Vidro | `#FAFAFC` | 72% | `#2F6FED` | sólido `#F4F4F6` |
| Vidro colorido | Vidro | `#783CB4` | 42% | `#FFD1FF` | vidro `#461E6E` 85% |
| Grafite | Sólido | `#24262B` | 100% | `#3DDBB5` | sólido `#1A1B1F` |
| Cristal | Vidro | `#FFFFFF` | 8% | `#FFFFFF` | vidro `#000000` 55% |

Todos com cantos **Redondos**.

### 4.2 Camadas e derivados

1. Valores do tema para a parte.
2. Por cima, os campos não nulos de `Geral`.
3. Por cima, os campos não nulos da parte (`Doca`, `Notch` ou `Ilhas`).
4. Derivados:
   - **Texto:** escuro (`#1B1D22`) quando o fundo é claro (luminância relativa > 0,5) **e** a opacidade ≥ 50%; senão
     claro (`#ECEEF2`). `TextoSecundario`: o texto a 62%.
   - **Borda:** branco a 10% com texto claro; preto a 8% com texto escuro.
   - **Transparência do Windows desligada:** todo material Vidro vira Sólido com opacidade 100%.
   - **Notch:** nunca tem desfoque (janela com transparência por pixel). Material Vidro no notch = translúcido sem
     desfoque, na opacidade pedida.
   - **Cantos em px:** Retos 0, Suaves 4, Redondos 8. Nas janelas com vidro, o Windows arredonda: Retos = sem
     arredondar, Suaves = pequeno, Redondos = padrão.

## 5. No Vitral (App)

### 5.1 Serviço de aparência

- `ServicoAparencia`: guarda os `ValoresAparencia` atuais e o evento `Mudou`. Recalcula ao aplicar um perfil (via
  `EstadoAparencia`) e, no tema Automático, quando o Windows muda de modo ou de destaque (`UserPreferenceChanged`
  General/Color).
- Publica em `Application.Current.Resources`, por parte (`Doca`, `Notch`, `Ilhas`): `Aparencia.<Parte>.Fundo`,
  `.Texto`, `.TextoSecundario`, `.Borda`, `.Destaque` (pincéis congelados) e `.Raio` (`CornerRadius`).
- Os XAML das ilhas, da doca e dos painéis trocam as cores fixas por `DynamicResource` dessas chaves. O código
  (notch, fichas, `Pilula`, `VidroIlha`) ouve `Mudou`.

### 5.2 Quem muda

- **Ilhas e painéis** (`JanelaIlha`, `JanelaPainel`, conteúdos): parte `Ilhas`. `VidroIlha.Atualizar` recebe os
  valores: vidro → acrílico com a cor e a opacidade do tema; sólido → fundo opaco; cantos pelo DWM (retos, pequenos,
  padrão).
- **Doca** (`JanelaDoca`, `JanelaMiniaturas`, `MenuDoca`): parte `Doca`, do mesmo jeito.
- **Notch** (`JanelaNotch`, fichas de permissão e de resposta, `PainelPendencias` desce das ilhas e segue `Ilhas`):
  parte `Notch`. Corpo com o fundo e a opacidade do notch; textos com o texto do notch; o ponto de lembrete e a barra
  de tempo no destaque.
- **`Pilula`:** o principal com fundo = texto do notch e texto = fundo do notch; o de contorno com borda do texto a
  28%; o de perigo `#FF8A8A` com texto claro e `#C62828` com texto escuro.

### 5.3 Perfil, testar, desfazer

- `Aplicador` aplica `Perfil.Aparencia` (não nula) pelo `EstadoAparencia`, como a barra do topo e a doca.
- `ServicoRetratos` guarda a aparência atual: "Testar (20 s)" volta sozinho, "Desfazer" volta à anterior, "Voltar ao
  Windows original" volta ao Vidro escuro.
- Exportar e importar levam a seção junto.

### 5.4 Aba "Aparência" (`aba-aparencia.html`)

- Nova aba entre "Barra do topo" e "Janelas".
- **Esquerda:** grade de temas com miniatura (o primeiro é "Não mexer"), a prévia grande e os botões de sempre.
- **Prévia (`PreviaAparencia`):** desenho das ilhas, do notch e da doca com os `ValoresAparencia` da escolha atual, sobre
  o papel de parede atual (desfocado sob as partes em vidro). Sem papel de parede legível: um gradiente escuro.
- **Direita, "Personalizar":** material, cor do fundo (paleta + "do tema"), transparência (deslizante), destaque
  (paleta + "do tema") e cantos, todos começando em "do tema". **"Diferenciar":** Doca, Notch, "Ilhas e painéis";
  marcar abre os mesmos campos para a parte.

## 6. Erros e bordas

| Caso | Comportamento |
|------|---------------|
| Transparência do Windows desligada | vidro vira sólido (a prévia mostra assim) |
| Automático e o Windows muda de modo/destaque | recalcula e redesenha na hora |
| Fundo claro escolhido num tema escuro | o texto vira escuro sozinho |
| Perfil sem a seção, ou importado de versão antiga | "não mexer"; sem aparência guardada vale o Vidro escuro |
| Cor inválida no perfil | o campo é ignorado |
| Barra do topo ou doca desligadas | a aparência é guardada e vale quando ligarem |

## 7. Testes

**Com teste escrito antes:**

- **Core:**
  - os sete temas;
  - o Automático, com o Windows claro e o escuro;
  - as camadas (tema, geral, parte);
  - o texto legível (claro e escuro, opacidade baixa);
  - a transparência do Windows desligada;
  - o notch sem desfoque;
  - a transparência recortada;
  - a cor inválida;
  - `Perfil` com e sem a seção (ida e volta no pacote);
  - o `EstadoAparencia` e os retratos (testar, desfazer, voltar ao original).
- **Ui:** o view model da aba (tema, campos "do tema", diferenciar), ligado ao perfil.

**Na prática, com o Gustavo:**

- os sete temas na doca, no notch, nas ilhas, nos painéis e nas fichas;
- o "diferenciar";
- trocar o modo do Windows com o Automático;
- desligar a transparência do Windows;
- Testar, Desfazer e Voltar ao original.

## 8. Riscos

- **Pontos de cor espalhados:** cores fixas espalhadas pelo XAML e pelo código das ilhas, da doca e dos painéis.
  Trocar todas sem esquecer nenhuma pede uma varredura (busca por `#`, `Ambar`, `NoiteVidro`, `Texto` nos arquivos
  dessas partes).
- **Tema Claro:** é a primeira vez que essas partes ficam claras; ícones e glifos pensados para fundo escuro precisam
  ser conferidos um a um.
- **Acrílico:** o acrílico com cor muito clara e opacidade baixa (Cristal) pode ficar diferente da prévia, que é
  simulada.
