# TerrAgents

Uma cidade de Terraria que mostra, em tempo real, uma equipe de agentes de IA trabalhando sozinha num repositório
de verdade. O humano dá objetivos; os agentes planejam, codificam, testam, revisam, resolvem conflitos e integram.

Arquitetura e roadmap: `docs/arquitetura-v0.1.md` (Agentaria Architecture v0.1).

## Estrutura

```
core/          Agentaria Core: fork do Foreman do AgentCraft (Node 22 + TypeScript)
protocol/      protocolo WebSocket entre o Core e as visões (protocol.md)
terraria-mod/  mod tModLoader (C#), a partir da fase 5
docs/          arquitetura, ADRs, spec original do AgentCraft
```

## Rodar o Core (fase 0)

```
cd core
npm install
npm test            # 482 testes herdados do AgentCraft
npm run start -- --backend sim --reset --speed 2   # equipe simulada, sem API
npm run tui         # cliente de terminal que fala o protocolo
```

Com agentes de verdade: `ANTHROPIC_API_KEY` no ambiente e `npm run start -- --backend claude --repo <caminho>`.

## Ver no navegador

Com o Core ligado, abra http://127.0.0.1:7878/ : o salão de guilda com os agentes andando, a meta, o feed,
as decisões com botões e uma caixa para mandar metas. Não precisa do Terraria aberto. Só a página que o
próprio Core serve pode abrir o WebSocket; outros sites continuam bloqueados.

## Piloto automático (fase 1)

Com `--backend claude` ou `codex`, ninguém precisa responder nada: o piloto automático (`core/src/autopilot.ts`)
responde perguntas, permissões e merges seguindo `<home>/policy.json` (exemplo em `core/policy.example.json`).

- Merge só com testes passando; com teste falhando, devolve para o agente até `maxAttempts` e depois rejeita.
- Depois de cada merge faz `git push origin <base>`.
- Tarefa que passa de `maxMinutesPerTask` ou `maxUsdPerTask` é cancelada (dá para `retry`).
- Ao chegar em `dailyUsd` no dia, pausa todos e pergunta se continua. É a única pergunta que chega ao humano.

Desligar: `--no-autopilot`.

## Origem

Fork do [AgentCraft](https://github.com/blendi-remade/agentcraft) (MIT). Ver `NOTICE` e `LICENSE`.
O mod do Minecraft não veio no fork; o Terraria entra no lugar.
