# Agentaria Architecture v0.1 (TerrAgents)

Documento vivo, com os diagramas: https://claude.ai/code/artifact/3942b2dd-f5cf-4fd5-beac-119db28df4ea

## Decisões tomadas em 17/10/2026

| Decisão | Escolha |
| --- | --- |
| Núcleo | Fork do Foreman do AgentCraft (MIT, Node 22 + TypeScript) |
| Nome | TerrAgents |
| Repositório | `HiveControlAgents/terragents`, privado até a fase 6 |
| Push para o remoto | Automático, feito pelo Core depois do merge |
| Teto de gasto diário | Ao atingir, o Core pausa e pergunta ao Isaque se continua |
| Projeto-cobaia | A Colmeia inteira (`colmeia-agentes`) |
| Licença | MIT, mantendo o copyright do AgentCraft (`NOTICE`) |

## Travas que substituem a aprovação humana

| Trava | Valor inicial |
| --- | --- |
| Tentativas por tarefa | 5 ciclos Execute → Validate |
| Tokens por tarefa | 300 mil |
| Tempo por tarefa | 45 min |
| Subagentes vivos | 3, profundidade 1, 150 mil tokens ou 30 min cada |
| Rodadas de revisão | 2; na terceira o coordenador decide |
| Mensagens por tópico entre agentes | 6 |

## Roadmap (ordem recomendada)

0. Fork rodando `sim` + `tui`, testes verdes.
1. Autonomia: `policy.yaml` no lugar da fila de decisões; Validator como juiz; orçamentos.
2. Multiagente autônomo (coordenador + 3 trabalhadores, revisão cruzada, conflito resolvido).
3. Subagentes com orçamento e fim obrigatório.
4. Protocolo v2 (`agent.location`, `agent.message`, `agent.health`, `project.cost`, `seq`).
5. Terraria conecta. 6. Um NPC. 7. Cidade. 8. QA autônomo. 9. Cidade dinâmica. 10. Multiplayer. 11. Vários projetos.
