# Atualização

Depois de instalado pelo Setup, o Vitral procura versões novas sozinho. Não precisa fazer nada.

## Como a versão nova chega

1. **Procura:** um minuto depois de o Vitral abrir e, depois, a cada 6 horas. Precisa de internet; sem ela, tenta de
   novo na próxima vez.
2. **Download:** achou uma versão nova, baixa em segundo plano, sem atrapalhar.
3. **Aviso:** o notch, no alto da tela, mostra o logo do Vitral com **"Vitral X pronto"** e **"clique para reiniciar e
   atualizar"**. Depois que o aviso some, passar o mouse no notch mostra **"Atualização pronta"** na lateral esquerda.
4. **Reinício:** clique no aviso. O Vitral fecha, troca de versão e abre de novo em poucos segundos, com os mesmos
   perfis.

**Não quer reiniciar agora?** Ignore o aviso: a versão nova entra sozinha na próxima vez que o Vitral abrir (por
exemplo, ao ligar o computador).

Sem a barra do topo ligada, não há notch: a atualização acontece do mesmo jeito, e o aviso fica só na janela do
Vitral (abaixo).

## Ver a versão e procurar agora

Abra a janela do Vitral (ícone na bandeja, perto do relógio). No pé da barra lateral aparecem:

- **"Vitral X.Y.Z"**: a versão instalada;
- **a linha de estado**, com o link **Procurar agora** para conferir na hora.

| A linha diz | Quer dizer |
|-------------|------------|
| Atualizado | você está na versão mais nova |
| Baixando a versão X… | a versão nova está chegando |
| Versão X pronta — **reiniciar** | clique em "reiniciar" para entrar nela |
| Não foi possível procurar atualizações | sem internet, ou o GitHub não respondeu; tenta de novo sozinho |
| … (acesso negado: o token venceu?) | a chave que o Vitral usa para ler as versões venceu; veja abaixo |
| Atualização desligada (versão de desenvolvimento) | este Vitral foi compilado sem a chave e não se atualiza |

## Se não atualizar

**Clicou em reiniciar e voltou a versão antiga?** Algum programa está usando a pasta do Vitral. Até a 0.9.1, os
programas abertos pela doca (como o app do Claude) faziam isso. Feche-os, clique em reiniciar de novo e abra-os depois.


Baixe o Setup mais novo e rode por cima, como na [instalação](instalar.md#1-baixar-o-instalador). Os perfis são
mantidos, e a partir daí as atualizações voltam a chegar sozinhas.

Isso é necessário em dois casos: quando a linha diz **acesso negado** (a chave venceu e foi trocada numa versão nova)
e em instalações de antes da 0.7.2, que ainda não tinham o atualizador.

## Versões de teste

Releases marcadas como **Pre-release** no GitHub não chegam pela atualização. Para testar uma, baixe o Setup dela à
mão.
