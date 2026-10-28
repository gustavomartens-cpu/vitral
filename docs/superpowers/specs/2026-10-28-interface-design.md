# Repaginação da janela principal do Vitral

Data: 28/10/2026. A janela principal (perfis e opções) ganha uma organização mais limpa e um visual de vidro fumê: barra
lateral só com os perfis, engrenagem de Configurações, abas por parte da tela, controles neutros e o âmbar só para
ação. As opções que hoje aparecem mas não funcionam saem da tela até serem consertadas.

## 1. Objetivo e escopo

Hoje a janela é azul-marinho, com a barra lateral lotada (perfis, cinco botões grandes, três caixas de marcar, versão),
duas abas mortas (Iniciar, Janelas), "Não mexer" em âmbar em cada linha e quatro botões de peso parecido no rodapé.
Toda vez que abre é preciso clicar no perfil, mesmo havendo um ativo. Algumas opções não fazem efeito (ocultar
automaticamente a barra de tarefas).

**Sucesso:** abrir o Vitral e cair direto no perfil ativo; achar cada coisa no lugar da parte da tela a que pertence;
ver só opções que funcionam; e uma janela que pareça acabada, em preto translúcido.

**Fora do escopo:** consertar as opções que não funcionam (projeto separado, a partir da lista da §5); a aparência da
doca, do notch e das ilhas (já tem a aba Aparência); comportamento dos perfis (aplicar, testar, desfazer).

## 2. Escolhas do Gustavo

- Layout A: barra lateral enxuta com os perfis.
- Abas por parte da tela: Windows, Barra de tarefas e doca, Barra do topo, Aparência.
- Opções que não funcionam: esconder até consertar.
- Acabamento B: vidro fumê (acrílico com tinta preta).

## 3. Estrutura

### 3.1 Barra lateral

- Título "PERFIS" com dois ícones: **+** (novo perfil) e **⋯** (menu: Importar, Exportar, Desfazer última mudança, Voltar
  ao Windows original).
- A lista de perfis, com a gema da cor e a etiqueta "ativo" no perfil ativo.
- No pé, **⚙ Configurações**.
- Saem da barra: os botões Novo perfil, Importar, Exportar, Desfazer, Voltar ao original; as caixas Iniciar com o
  Windows, Avisos do Claude, Agenda no notch; build e versão.

### 3.2 Ao abrir

Seleciona o perfil ativo (`config.PerfilAtivo`); sem ativo (ou ativo inexistente), o primeiro da lista; sem perfis, um
convite "Crie seu primeiro perfil" com o botão **+**.

### 3.3 Cabeçalho

- Nome do perfil (22, seminegrito) e a etiqueta "ativo" quando for o ativo.
- À direita: **Testar (20 s)** (botão discreto) e **Aplicar** (âmbar).
- Com mudança não salva, abaixo do nome: "Alterações não salvas · **Salvar** · **Descartar**" (links). Sem mudança, a
  linha some.
- O rodapé de hoje (Descartar, Salvar, Testar, Aplicar e a mensagem) sai; a mensagem de resultado ("'X' aplicado.")
  passa para a linha de baixo do nome, no lugar da de alterações, e some depois de 6 s.

### 3.4 Abas

1. **Windows**: modo do Windows, modo dos apps, efeitos de transparência, cor de destaque (paleta, cor livre, cor
   automática), destaque no Iniciar e na barra, destaque nos títulos.
2. **Barra de tarefas e doca**: alinhamento, pesquisa, visão de tarefas, mostrar área de trabalho, agrupar, ocultar
   automaticamente, todos os monitores, selos, piscar, ícones perto do relógio; Doca do Vitral (ligar); formas da doca
   (formato, ícones, ampliação).
3. **Barra do topo**: ligar a barra; ilhas (mídia, relógio, status, CPU e memória); formas do notch (formato, tamanho)
   e das ilhas (disposição, distância).
4. **Aparência**: como hoje (temas, prévia, personalizar, diferenciar). A prévia passa a usar as formas do editor de
   formas, que agora vivem nas abas 2 e 3.

As abas Iniciar e Janelas saem. A aba Formas some (as formas foram para as abas 2 e 3). Opções escondidas pela §5 não
aparecem nas abas.

### 3.5 Configurações

Janela pequena (420 de largura), no mesmo vidro fumê, aberta pela engrenagem:

- Iniciar com o Windows (chave);
- Avisos do Claude no notch (chave);
- Agenda no notch (chave) e o link "Contas…";
- Versão, build do Windows e a linha de atualização ("Atualizado · Procurar agora", ou a versão nova pronta).

O comportamento de cada uma é o de hoje (o código sai da `JanelaPrincipal` e vai para a janela de Configurações).

### 3.6 Trocar de perfil com mudança não salva

Pergunta "Salvar as mudanças em 'X'?" com **Salvar**, **Descartar** e **Cancelar** (Cancelar mantém o perfil atual
selecionado). Hoje a mudança some sem aviso.

## 4. Visual: vidro fumê

### 4.1 Material

- Acrílico do Windows (o mesmo `SetWindowCompositionAttribute` das ilhas) com tinta preta `#0C0C0E` a 55% (alfa 0x8C)
  em toda a janela; barra de título escura (modo escuro do DWM) e cantos arredondados do Windows 11.
- Transparência do Windows desligada: fundo opaco `#0E0E10`.
- Janela de Configurações e de Contas da agenda: o mesmo material.

### 4.2 Cores da janela principal (Tokens.xaml)

| Chave | Valor |
|---|---|
| Noite (fundo opaco) | `#0E0E10` |
| NoiteVidro (tinta) | `#8C0C0C0E` |
| NoiteLateral | `#330A0A0C` (a lateral um pouco mais escura) |
| Texto | `#EDEDF0` |
| TextoSecundario | `#99EDEDF0` |
| Linha / Borda | `#14FFFFFF` / `#1AFFFFFF` |
| Superficie / SuperficieHover (cartões) | `#0DFFFFFF` / `#17FFFFFF` |
| Selecao (item escolhido) | `#24FFFFFF` |
| Ambar | `#F2B544` (só ação) |
| Foco do teclado | `#B3FFFFFF` (branco 70%) |

A doca, o notch e as ilhas continuam com o dicionário de cada parte (aparência); os menus de contexto sem aparência
(bandeja) passam a usar o preto novo.

### 4.3 Controles (Estilos.xaml)

- **Escolha (SeletorPilula):** controle segmentado neutro: trilho `Superficie`, opção marcada em `Selecao` com texto
  `Texto`, outras em `TextoSecundario`. "Não mexer" aparece como **—** (dica "Não mexer").
- **Botão principal:** âmbar cheio, texto escuro, cantos 8.
- **Botão secundário:** `Superficie`, texto `Texto`, cantos 8; hover `SuperficieHover`.
- **Botão-ícone (+, ⋯, ⚙):** 26×26, sem fundo; hover `SuperficieHover`.
- **Chave (ToggleSwitch):** trilho 34×18; ligada em âmbar.
- **Cor de destaque:** quadrados 20×20, cantos 5; a escolhida com anel `Texto` de 2 px.
- **Cartão:** `Superficie`, borda `Linha` de 1 px, cantos 12, título de seção em maiúsculas 10, `TextoSecundario`.
- **Abas:** texto 12,5; ativa em `Texto` com traço âmbar de 2 px; outras em `TextoSecundario`.
- **Lista de perfis:** item 32 de altura, cantos 8; escolhido em `Selecao`; "ativo" em âmbar 10.

## 5. Opções que não funcionam

Antes de fechar a tela, cada opção do perfil é conferida no Windows: aplicar num perfil de teste, conferir o efeito
(registro, barra, janela), voltar ao original. As que não fizerem efeito:

- saem das abas (a linha inteira some);
- continuam no modelo e nos perfis (o valor guardado é preservado ao salvar);
- entram em `docs/ideias-pendentes.md`, seção "Opções a consertar", com o que foi observado.

A lista do que saiu é mostrada ao Gustavo antes do fechamento.

## 6. Erros e bordas

- Perfil ativo inexistente: abre no primeiro. Nenhum perfil: convite.
- Troca de perfil com mudança pendente: a pergunta da §3.6.
- Janela no tamanho mínimo (860×580): lateral fixa de 220; as abas e o conteúdo rolam.
- Transparência desligada: opaco.
- Contorno de foco visível em todos os controles (Tab).

## 7. Testes

- **Ui (PrincipalViewModel):** abre no perfil ativo / no primeiro / sem perfil; "alterações não salvas" liga ao editar
  e desliga ao salvar, descartar e trocar de perfil; troca com pendência pede confirmação (Salvar, Descartar,
  Cancelar); opção escondida mantém o valor ao salvar; a mensagem de resultado some depois de 6 s.
- **Na tela, com o Gustavo:** as quatro abas, Configurações, o "⋯", o vidro fumê sobre o papel de parede e com a
  transparência desligada, o teclado.

## 8. Riscos

- **Leitura sobre papel de parede claro:** a tinta de 55% foi escolhida para segurar; se não bastar na conferência,
  sobe para 70%.
- **Opção escondida que alguém usava sem saber que não funciona:** o valor continua no perfil; só deixa de aparecer.
