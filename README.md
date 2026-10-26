# Vitral

O Vitral muda o visual do Windows 11 por perfis: tema, cores, ícones, barra de tarefas, uma doca embaixo e uma barra no
topo da tela com mídia, relógio e status. O relógio fica num notch, que também avisa quando o Claude Code termina uma
tarefa ou espera você.

Cada perfil pode ser testado por 20 segundos antes de ser mantido, e o Vitral sempre sabe voltar ao Windows original.

## Requisitos

- Windows 11, 64 bits.
- Nada mais: o instalador traz o .NET junto e instala só para o seu usuário, sem pedir administrador.
- O repositório é público: o instalador se baixa direto da [release mais nova](https://github.com/gustavomartens-cpu/vitral/releases/latest),
  sem conta do GitHub. Só o dono do repositório pode alterá-lo (veja [Publicar](docs/publicar.md#quem-pode-alterar-o-repositório)).

## Guias

| Guia | Para quem | O que tem |
|------|-----------|-----------|
| [Instalar](docs/instalar.md) | quem vai usar | baixar e instalar, avisos do Windows, trazer perfis, desinstalar |
| [Atualização](docs/atualizacao.md) | quem vai usar | como as versões novas chegam sozinhas e o que fazer se não chegarem |
| [Publicar](docs/publicar.md) | quem mantém | tokens do GitHub, gerar e publicar uma versão nova |

O que ainda está por vir fica em [ideias pendentes](docs/ideias-pendentes.md).

## Desenvolver

```powershell
. tools/env.ps1          # .NET 10 do projeto
dotnet test              # testes
pwsh build.ps1 -Versao X.Y.Z   # Setup.exe local em artifacts\release
```

Uma versão compilada assim, sem o token de leitura, não se atualiza sozinha; a janela mostra
"Atualização desligada (versão de desenvolvimento)".
