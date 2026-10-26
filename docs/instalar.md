# Instalar o Vitral

O instalador é um arquivo só. Ele **instala para o seu usuário, sem pedir administrador**, e já traz o .NET: o
computador não precisa de nada instalado antes. Depois de instalado, o Vitral **se atualiza sozinho**
(veja [Atualização](atualizacao.md)); este passo a passo é feito uma vez só por computador.

**Quem só usa o Vitral não precisa de token nem de variável nenhuma**: a chave de leitura das versões já vai dentro do
instalador. Os tokens do [Publicar](publicar.md) servem apenas a quem publica versões novas.

## 1. Baixar o instalador

O repositório é público: não precisa de conta do GitHub nem de convite.

1. Abra <https://github.com/gustavomartens-cpu/vitral/releases/latest>.
2. Em **Assets**, clique em **`VitralApp-win-Setup.exe`** para baixar.

Use sempre a release marcada como **Latest**. Versões marcadas como **Pre-release** são testes.

## 2. Instalar

1. Dê dois cliques no `VitralApp-win-Setup.exe`.
2. Se aparecer **"O Windows protegeu o computador"** (SmartScreen, porque o Vitral não tem assinatura digital), clique em
   **Mais informações** e depois em **Executar assim mesmo**.
3. O Vitral abre sozinho no fim e ganha atalhos na Área de Trabalho e no Menu Iniciar. Ele fica perto do relógio, na
   bandeja, mesmo com a janela fechada.

> **Bloqueio sem a opção "Executar assim mesmo"?** É o **Controle Inteligente de Aplicativos** (Smart App Control), que
> bloqueia qualquer app sem assinatura. Ele fica em Configurações → Privacidade e segurança → Segurança do Windows →
> Controle de aplicativos e do navegador. Desligá-lo é uma decisão sua, e não dá para religar depois sem reinstalar o
> Windows.

Já tem uma versão antiga instalada? Rode o Setup novo por cima: os perfis são mantidos.

## 3. Trazer os perfis de outro computador

1. No computador de origem, selecione os perfis no Vitral e clique **Exportar**. Sai um arquivo `.vitral` por perfil.
2. Leve os arquivos por pendrive, OneDrive ou rede.
3. No computador novo, clique **Importar** e escolha o arquivo `.vitral`.
4. Selecione o perfil e clique **Testar (20 s)**. Se gostar, **Manter**; senão, ele volta sozinho.
5. Para o Vitral voltar a cada vez que o computador ligar, marque **Iniciar com o Windows** e use **Aplicar**.

A **barra do topo** (mídia, relógio e status no alto da tela) também faz parte do perfil: aba **Barra do topo** →
**Ligada**. Ela aparece no monitor principal e reserva uma faixa no alto, para as janelas maximizadas começarem abaixo
dela. Com o Vitral fechado, a barra some e a faixa é liberada.

## 4. Avisos do Claude Code (opcional)

Quem usa o Claude Code pode marcar **Avisos do Claude no notch**, na barra lateral do Vitral. O notch passa a avisar
quando o Claude termina uma tarefa ou espera você.

## Desinstalar

Antes, se quiser o Windows exatamente como era, use **Voltar ao Windows original** no Vitral.

Depois: Configurações → Aplicativos → Aplicativos instalados → **Vitral** → Desinstalar. Os perfis em
`%LOCALAPPDATA%\Vitral` **ficam**; apague essa pasta se quiser removê-los também.

## Gerar o instalador sem o GitHub

Quem tem o código pode gerar o Setup localmente com `pwsh build.ps1 -Versao X.Y.Z` (sai em `artifacts\release`). Sem o
token de leitura, essa versão não se atualiza sozinha; veja [Publicar](publicar.md).
