# Mod TerrAgents (tModLoader)

Cliente "burro" do TerrAgents Core: desenha os agentes como NPCs e manda os comandos do jogador.
Não decide nada. Usa o sprite do Guia do próprio Terraria da pessoa; nenhum arquivo do jogo vem aqui.

## Instalar para desenvolver

1. Instale o tModLoader pela Steam (grátis, precisa do Terraria) e abra uma vez.
2. Copie `terraria-mod/TerrAgents` para `Documentos/My Games/Terraria/tModLoader/ModSources/TerrAgents`.
3. No tModLoader: Workshop, Develop Mods, TerrAgents, Build + Reload.
4. Ligue o Core e entre num mundo single player.

## No jogo

- Os agentes no turno aparecem perto do spawn e andam até a estação do que fazem:
  reunião, biblioteca e mesa à esquerda; descanso no centro; terminal, testes e merge à direita.
- `/ta` mostra o estado. `/ta meta <texto>` manda uma meta. `/ta responder d3 Merge`. `/ta falar kit <texto>`.
