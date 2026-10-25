# Instalar o Vitral no notebook

O instalador é um arquivo só, **instala para o seu usuário, sem pedir admin**, e já traz o .NET embutido: o notebook não precisa de nada instalado antes.

## 1. Gerar os arquivos (neste PC)

1. No terminal: `pwsh G:\dev\vitral\build.ps1`. No fim aparece `Pronto: ...\artifacts\release\Vitral-win-Setup.exe`.
2. No Vitral, selecione os perfis que quer levar e clique **Exportar**. Isso gera um arquivo `.vitral` por perfil.

## 2. Levar para o notebook

Copie o `Vitral-win-Setup.exe` e os arquivos `.vitral` por pendrive, OneDrive ou rede.

## 3. Instalar

1. Dê dois cliques no `Vitral-win-Setup.exe`.
2. Se aparecer **"O Windows protegeu o computador"** (SmartScreen, porque o app não tem assinatura digital), clique em **Mais informações** e depois em **Executar assim mesmo**. Isso costuma acontecer só quando o arquivo veio da internet; por pendrive em geral não aparece.
3. O Vitral abre sozinho no fim, e ganha atalhos na Área de Trabalho e no Menu Iniciar.

> Se aparecer um bloqueio sem a opção "Executar assim mesmo", é o **Controle Inteligente de Aplicativos** (Smart App Control) ligado. Ele bloqueia qualquer app sem assinatura. Ele fica em Configurações → Privacidade e segurança → Segurança do Windows → Controle de aplicativos e do navegador. Desligá-lo é uma decisão sua (e não dá para religar depois sem reinstalar o Windows).

## 4. Trazer os perfis

1. No Vitral, clique **Importar** e escolha o arquivo `.vitral`.
2. Selecione o perfil e clique **Testar (20 s)**. Se gostar, **Manter**; senão, ele volta sozinho.
3. Para ele voltar a cada vez que o notebook ligar, marque **Iniciar com o Windows** e use **Aplicar**.

## Atualizar

Gere um `Setup.exe` novo com uma versão maior (`pwsh build.ps1 -Versao 0.2.0`) e rode-o no notebook por cima. Seus perfis são mantidos.

## Desinstalar

Configurações → Aplicativos → Aplicativos instalados → **Vitral** → Desinstalar. Os perfis em `%LOCALAPPDATA%\Vitral` **ficam** (apague essa pasta se quiser removê-los também). Antes de desinstalar, se quiser o Windows exatamente como era, use **Voltar ao Windows original** no Vitral.
