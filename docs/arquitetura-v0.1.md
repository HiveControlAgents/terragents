# Agentaria Architecture v0.1 (TerrAgents)

Primeira arquitetura do projeto (outubro de 2026). O README tem o estado atual.

## Decisões de base

| Decisão | Escolha |
| --- | --- |
| Núcleo | Fork do Foreman do AgentCraft (MIT, Node 22 + TypeScript) |
| Nome | TerrAgents |
| Push para o remoto | Automático, feito pelo Core depois do merge |
| Teto de gasto diário | Ao atingir, o Core pausa e pergunta ao humano se continua |
| Primeiro teste real | Um sistema de agentes em Python com testes (primeira meta entregue sozinha pelo time) |
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
