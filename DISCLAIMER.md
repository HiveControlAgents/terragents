# Aviso de responsabilidade / Disclaimer

## Português

O TerrAgents é um projeto experimental e gratuito, feito por hobby, distribuído "no estado em que se
encontra" sob a licença MIT. Usa quem quiser, por sua conta e risco.

**Ao rodar o TerrAgents, você entende e aceita que:**

1. **Os agentes são autônomos.** Com o piloto automático ligado, ninguém revisa o trabalho antes de ele
   entrar no seu repositório. O Core faz merge e push sozinho, seguindo apenas as regras do seu
   `policy.json` e o resultado dos seus testes.
2. **Testes verdes não garantem código correto.** Um merge automático pode quebrar seu sistema, apagar ou
   alterar dados, introduzir falhas de segurança ou subir algo que você não queria.
3. **Agentes de IA erram.** Eles podem entender a meta errado, escrever código ruim, apagar arquivos dentro
   do worktree, gastar mais do que o esperado ou agir de forma inesperada. As travas (worktree isolado,
   push bloqueado para os agentes, tetos de custo, negar comandos por padrão) reduzem o risco, mas não
   eliminam.
4. **O custo é seu.** Todo o uso da API da Anthropic (ou do seu plano Claude) é cobrado de você. Os valores
   mostrados pelo TerrAgents são estimativas e podem diferir da sua fatura.
5. **Você é o responsável** pelo repositório que aponta para o TerrAgents, pelas chaves que usa, pelo que for
   publicado e por cumprir os termos dos serviços envolvidos (Anthropic, GitHub, Steam, Terraria,
   tModLoader).
6. **Os autores e contribuidores não se responsabilizam** por nenhum dano, perda de dados, perda de
   dinheiro, indisponibilidade, falha de segurança ou qualquer outra consequência de merges automáticos,
   ações dos agentes ou do uso do TerrAgents de qualquer forma.

**Recomendações:** use primeiro o modo simulado (`npm run demo`); teste num repositório de teste ou numa
cópia; deixe `push` em `"pr"` ou `"off"` em projetos importantes; tenha backup; mantenha os tetos de custo
baixos; nunca aponte o TerrAgents para produção sem revisão humana.

**Terraria:** o mod é um projeto de fã, não oficial, sem ligação com a Re-Logic ou com a equipe do
tModLoader. Terraria é marca da Re-Logic. Nenhum arquivo do jogo é distribuído aqui.

**AgentCraft:** o TerrAgents é um fork do AgentCraft (MIT). Os autores do AgentCraft não têm relação com
este fork e não respondem por ele.

## English

TerrAgents is a free, experimental hobby project, provided "as is" under the MIT license. Anyone may use it,
at their own risk.

**By running TerrAgents you understand and accept that:**

1. **The agents are autonomous.** With the autopilot on, nobody reviews the work before it lands in your
   repository. The Core merges and pushes on its own, following only your `policy.json` rules and the result
   of your tests.
2. **Passing tests do not guarantee correct code.** An automatic merge can break your system, delete or change
   data, introduce security flaws, or publish something you did not want.
3. **AI agents make mistakes.** They can misread the goal, write bad code, delete files inside their worktree,
   spend more than expected, or behave unexpectedly. The guards (isolated worktrees, push blocked for agents,
   spend caps, deny-by-default commands) reduce the risk; they do not remove it.
4. **The cost is yours.** All Anthropic API usage (or Claude plan usage) is billed to you. Amounts shown by
   TerrAgents are estimates and may differ from your bill.
5. **You are responsible** for the repository you point TerrAgents at, the keys you use, anything that gets
   published, and complying with the terms of the services involved (Anthropic, GitHub, Steam, Terraria,
   tModLoader).
6. **The authors and contributors accept no liability** for any damage, data loss, financial loss, downtime,
   security incident, or any other consequence of automatic merges, agent actions, or any use of TerrAgents.

**Recommendations:** start with the simulated mode (`npm run demo`); try it on a test repository or a copy;
keep `push` at `"pr"` or `"off"` for important projects; keep backups; keep spend caps low; never point
TerrAgents at production without human review.

**Terraria:** the mod is an unofficial fan project, not affiliated with Re-Logic or the tModLoader team.
Terraria is a trademark of Re-Logic. No game files are distributed here.

**AgentCraft:** TerrAgents is a fork of AgentCraft (MIT). The AgentCraft authors are not involved with this
fork and are not responsible for it.
