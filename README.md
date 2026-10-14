# Os Esquecidos

RPG de ação distópico no estilo de Maze Runner. Inspirado no **Flare** (jogabilidade: atributos, classes, poderes, loot com afixos, missões por status) e principalmente no **Pal3.Unity** (arquitetura: núcleo em C# puro, comandos e executores, roteiros como sequência de comandos, objetos de cenário com estado). Nada de código ou arte desses projetos foi copiado.

Você acorda num elevador de carga que sobe até o **Pátio**, um jardim cercado por muros de 24 m. Do lado de fora fica o **Labirinto**: os muros mudam a cada noite, os portões fecham ao anoitecer e os **Tecelões** saem para caçar.

Tudo no jogo é gerado por código: modelos, animação, shaders, texturas, música e efeitos sonoros. Não há nenhum arquivo de arte ou de áudio no projeto.

## Jogar

Baixe o zip para Windows na página de [Releases](https://github.com/PauloMatiasxvs/os-esquecidos/releases/latest), extraia e abra `Os Esquecidos.exe`. Para jogar a partir do código, gere o executável (veja abaixo) e abra `dist/OsEsquecidos/Os Esquecidos.exe`.

| Comando | Ação |
|---|---|
| Clique esquerdo | Andar; em inimigo, atacar (segure para continuar); em gente, conversar; em item, pegar |
| W A S D | Andar direto |
| Shift + clique | Atacar parado na direção do cursor |
| Clique direito / 1–6 | Poderes e itens da barra |
| Espaço | Rolamento |
| F | Interagir com o que estiver perto |
| Q / E, roda do mouse | Girar e aproximar a câmera |
| I, C, K, J, M | Mochila, personagem, poderes, diário, mapa |
| F5 / F9 / Esc | Salvar / carregar / pausa |

Controle de videogame também funciona. O jogo salva sozinho ao voltar para o Pátio; o arquivo fica em `%USERPROFILE%\AppData\LocalLow\paulo\Os Esquecidos\salvo.txt`.

## Estrutura

| Parte | O que é |
|---|---|
| `Assets/Scripts/Esquecidos.Nucleo` | Regras do jogo em C# puro, sem Unity: simulação a 60 Hz, IA, combate, poderes, loot, inventário, missões, roteiros, gerador de labirinto, Pátio, ciclo dia/noite, salvamento |
| `Assets/Resources/Dados`, `Roteiros` | Conteúdo em texto: 3 classes, 24 poderes de classe, 97 itens, 28 afixos, 23 inimigos (6 chefes), 8 NPCs, 13 missões, 6 setores, 18 registros, história com 2 finais |
| `Assets/Scripts/Motor` | Construtor de malhas, texturas de ruído, materiais, entrada, síntese de som e música generativa |
| `Assets/Scripts/Esquecidos.Jogo` | Cena 3D, personagens e bichos animados, câmera, controle, efeitos, áudio e interface |
| `Assets/Resources/Shaders` | Toon com contorno, muro triplanar com hera e ferrugem, chão, grama com vento, céu e partículas (URP) |

## Testes

```powershell
cd testes
dotnet test
```

181 testes xUnit cobrem dados, roteiros, labirinto (8 sementes por setor, conectividade pela grade de colisão real), combate, noite, morte, loja, oficina e salvamento. O núcleo compila como .NET Standard 2.1 / C# 9, igual ao Unity.

O executável tem um modo de teste que joga sozinho (menu, criação, abertura, Pátio, conversa, setor, combate, noite, laboratório, morte) e tira fotos da tela:

```powershell
& ".\dist\OsEsquecidos\Os Esquecidos.exe" --captura
```

As fotos e um relatório com os erros do log vão para `dist/OsEsquecidos/capturas`. Nesse modo o jogo ignora teclado e mouse e não grava por cima do jogo salvo.

## Gerar o executável

Projeto Unity 6.6 (URP). Pela linha de comando, sem abrir o editor:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe" -batchmode -quit -projectPath . -executeMethod Esquecidos.Editor.Construcao.Windows
```

Ou, com o projeto aberto no editor, menu **Os Esquecidos → Gerar executável Windows**.

## Licença

MIT — veja [LICENSE](LICENSE).
