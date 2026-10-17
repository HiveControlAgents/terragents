# Mod TerrAgents (tModLoader)

Cliente "burro" do TerrAgents Core: desenha os agentes como NPCs e manda os comandos do jogador.
Não decide nada. Usa o sprite do Guia do próprio Terraria da pessoa; nenhum arquivo do jogo vem aqui.

## Instalar para desenvolver

1. Instale o tModLoader pela Steam (grátis, precisa do Terraria) e abra uma vez.
2. Copie `terraria-mod/TerrAgents` para `Documentos/My Games/Terraria/tModLoader/ModSources/TerrAgents`.
3. No tModLoader: Workshop, Develop Mods, TerrAgents, Build + Reload. Isso precisa do .NET 8 SDK instalado.
   Sem o SDK, feche o tModLoader e compile pela linha de comando, com o .NET que vem junto com ele:

   ```
   cd "C:\Program Files (x86)\Steam\steamapps\common\tModLoader"
   dotnet\dotnet.exe tModLoader.dll -server -build "%USERPROFILE%\Documents\My Games\Terraria\tModLoader\ModSources\TerrAgents"
   ```

4. Ligue o Core e entre num mundo single player.

## No jogo

- Os agentes no turno aparecem perto do spawn e andam até a estação do que fazem:
  reunião, biblioteca e mesa à esquerda; descanso no centro; terminal, testes e merge à direita.
- `/ta construir` constrói a cidade onde você está: um escritório por setor, com móveis e placa, que os
  agentes do setor dividem. Ela sobrescreve o terreno ali e fica salva no mundo (use um mundo de teste).
- `/ta` mostra o estado. `/ta meta <texto>` manda uma meta. `/ta responder d3 Merge`. `/ta falar kit <texto>`.
