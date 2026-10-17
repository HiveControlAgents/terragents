// Autopilot: answers the decisions the Core (forked from the AgentCraft Foreman) would put in front of a human, following
// <home>/policy.json, so the team runs with nobody watching. The one decision it never answers is
// its own daily budget question: when the day's spend reaches the cap it pauses every agent and
// asks the user whether to continue.
//
//   merge       tests pass (the Validator is the judge) -> Merge, then push the base branch.
//               tests fail -> Request changes, up to maxAttempts; then Reject.
//   permission  question matches one of permissions.allow (regex) -> Allow once; else permissions.default.
//   question    the first option (the recommended one), or a text telling the agent to decide itself.
//
// Caps per task: maxMinutesPerTask (time since it was first seen in "doing") and maxUsdPerTask
// (sum of its sessions' cost). A task over a cap is cancelled; the user can retry it.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import type { Foreman } from './foreman.js';
import type { Decision, Task } from './protocol.js';
import { run } from './util/proc.js';

export interface Policy {
  maxAttempts: number;
  maxMinutesPerTask: number;
  maxUsdPerTask: number;
  /** estimated USD per day; 0 = no cap */
  dailyUsd: number;
  /** merges need a passing test run (ci "pass"); false also merges when no tests ran */
  requireTests: boolean;
  /**
   * after every merge: "direct" pushes the base branch to origin; "pr" pushes it to a new branch
   * and opens a pull request for a human to merge (protected main branches); "off" keeps it local.
   * true/false from older files mean "direct"/"off".
   */
  push: 'direct' | 'pr' | 'off';
  permissions: { allow: string[]; default: 'Allow once' | 'Deny' };
}

export const DEFAULT_POLICY: Policy = {
  maxAttempts: 5,
  maxMinutesPerTask: 45,
  maxUsdPerTask: 1.5,
  dailyUsd: 0.33,
  requireTests: true,
  push: 'direct',
  permissions: { allow: [], default: 'Deny' },
};

export function loadPolicy(file: string): Policy {
  if (!fs.existsSync(file)) return structuredClone(DEFAULT_POLICY);
  const raw = JSON.parse(fs.readFileSync(file, 'utf8')) as Partial<Policy>;
  const p: Policy = { ...structuredClone(DEFAULT_POLICY), ...raw, permissions: { ...DEFAULT_POLICY.permissions, ...(raw.permissions ?? {}) } };
  const push = p.push as unknown;
  p.push = push === true ? 'direct' : push === false ? 'off' : (push as Policy['push']);
  if (!['direct', 'pr', 'off'].includes(p.push)) throw new Error('policy.json: push must be "direct", "pr" or "off"');
  for (const k of ['maxAttempts', 'maxMinutesPerTask', 'maxUsdPerTask', 'dailyUsd'] as const) {
    if (typeof p[k] !== 'number' || !(p[k] >= 0)) throw new Error(`policy.json: ${k} must be a number >= 0`);
  }
  if (p.permissions.default !== 'Allow once' && p.permissions.default !== 'Deny') throw new Error('policy.json: permissions.default must be "Allow once" or "Deny"');
  for (const r of p.permissions.allow) new RegExp(r); // throws on a bad pattern
  return p;
}

export interface AutopilotState {
  day: string;
  /** costUsd (Foreman total) when the day started */
  dayStartUsd: number;
  /** spend allowed today; grows by dailyUsd each time the user says "continue" */
  allowanceUsd: number;
  attempts: Record<string, number>;
  taskStart: Record<string, number>;
  budgetDecision?: string;
  /** agents the budget pause stopped (resumed on "continue") */
  pausedByBudget: string[];
}

export const BUDGET_OPTIONS = ['Continuar hoje', 'Parar até amanhã'] as const;
const NO_HOOKS_DIR = path.join(os.tmpdir(), 'agentcraft-no-hooks-7f3e9c');

export class Autopilot {
  private creatingHuman = false;
  private timer: NodeJS.Timeout | undefined;
  /** pending autopilot work, so tests (and shutdown) can wait for it */
  private work: Promise<unknown> = Promise.resolve();

  constructor(
    private fm: Foreman,
    readonly policy: Policy,
    private opts: {
      push?: (repoPath: string, branch: string) => Promise<string>;
      openPr?: (repoPath: string, branch: string, title: string, body: string) => Promise<string>;
    } = {},
  ) {}

  get state(): AutopilotState {
    const data = this.fm.store.data as { autopilot?: AutopilotState };
    data.autopilot ??= { day: '', dayStartUsd: 0, allowanceUsd: this.policy.dailyUsd, attempts: {}, taskStart: {}, pausedByBudget: [] };
    return data.autopilot;
  }

  /** The Foreman asks before notifying the user: true = the autopilot answers this one. */
  handles(d: Decision): boolean {
    return !this.creatingHuman && d.id !== this.state.budgetDecision;
  }

  start(): void {
    this.fm.decisions.onCreated((d) => {
      if (this.handles(d)) this.queue(() => this.answer(d));
    });
    this.fm.subscribe((m) => {
      if (m.type === 'foreman.status') this.checkBudget();
      if (m.type === 'task.upsert') this.trackTask(m.task);
      if (m.type === 'decision.upsert' && m.decision.id === this.state.budgetDecision && m.decision.status === 'answered') this.onBudgetAnswer(m.decision);
    });
    // decisions left open by a previous run
    for (const d of this.fm.decisions.open()) if (this.handles(d)) this.queue(() => this.answer(d));
    this.timer = setInterval(() => this.checkTasks(), 60_000);
    this.timer.unref?.();
    this.checkBudget();
  }

  stop(): void {
    if (this.timer) clearInterval(this.timer);
  }

  idle(): Promise<unknown> {
    return this.work;
  }

  private queue(fn: () => Promise<void>): void {
    // after the creator's own code (it may still be attaching its wait())
    this.work = this.work.then(() => new Promise((r) => setTimeout(r, 0))).then(fn).catch((e) => this.fm.log.error(`autopilot: ${(e as Error).message}`));
  }

  private say(text: string, agentId?: string): void {
    this.fm.bus.feed('decision', `Piloto automático: ${text}`, agentId ? { agentId } : {});
  }

  // ---- decisions ----------------------------------------------------------------------------

  private async answer(d: Decision): Promise<void> {
    if (this.fm.decisions.get(d.id)?.status !== 'open') return;
    if (d.kind === 'question') {
      if (d.options.length) {
        this.say(`respondeu "${d.options[0]}" para ${this.fm.nameOf(d.agentId)}`, d.agentId);
        await this.fm.answerDecision(d.id, 0);
      } else {
        this.say(`devolveu a pergunta para ${this.fm.nameOf(d.agentId)} decidir`, d.agentId);
        await this.fm.answerDecision(d.id, undefined, 'Ninguém vai responder: decida você pela opção mais simples e segura, registre o motivo no resumo e continue.');
      }
      return;
    }
    if (d.kind === 'permission') {
      const allowed = this.policy.permissions.allow.some((r) => new RegExp(r, 'i').test(d.question));
      const option = allowed ? 'Allow once' : this.policy.permissions.default;
      this.say(`${option === 'Deny' ? 'negou' : 'liberou'} "${d.question}"`, d.agentId);
      await this.fm.answerDecision(d.id, option, option === 'Deny' ? 'Negado pelo piloto automático (policy.json). Faça de outro jeito, dentro do seu worktree.' : undefined);
      return;
    }
    await this.answerMerge(d);
  }

  private async answerMerge(d: Decision): Promise<void> {
    const task = d.taskId ? this.fm.tasks.get(d.taskId) : undefined;
    const ci = task?.ci ?? 'unknown';
    const ok = ci === 'pass' || (ci !== 'fail' && !this.policy.requireTests);
    if (ok) {
      const res = await this.fm.answerDecision(d.id, 'Merge');
      if (res.status !== 'answered') {
        // the merge was refused (dirty checkout, conflict nobody could take): a human has to look
        this.fm.notify('need_user', `Piloto automático: merge de ${task?.id ?? d.id} recusado; precisa de você`, d.id);
        return;
      }
      this.say(`fez o merge de ${task?.id ?? d.worktree} (testes: ${ci})`, d.agentId);
      if (d.repoId && this.policy.push === 'direct') await this.pushBase(d.repoId);
      if (d.repoId && this.policy.push === 'pr') await this.openPr(d.repoId, task);
      return;
    }
    const id = task?.id ?? d.id;
    const n = (this.state.attempts[id] ?? 0) + 1;
    this.state.attempts[id] = n;
    this.fm.store.markDirty();
    if (n >= this.policy.maxAttempts) {
      this.say(`rejeitou ${id} depois de ${n} tentativas sem testes passando`, d.agentId);
      await this.fm.answerDecision(d.id, 'Reject');
      return;
    }
    const why = ci === 'fail' ? 'os testes falharam' : 'nenhum teste rodou (a política exige testes passando)';
    this.say(`pediu mudanças em ${id}: ${why} (tentativa ${n} de ${this.policy.maxAttempts})`, d.agentId);
    await this.fm.answerDecision(d.id, 'Request changes', `Validator: ${why}. Corrija e rode os testes de novo antes de mandar para revisão.`);
  }

  private async pushBase(repoId: string): Promise<void> {
    const repo = this.fm.repos.get(repoId);
    if (!repo) return;
    try {
      const out = await (this.opts.push ?? gitPush)(repo.path, repo.branch);
      this.fm.bus.feed('merge', `Piloto automático: push de ${repo.branch} para origin${out ? ` (${out})` : ''}`);
    } catch (e) {
      this.fm.bus.feed('error', `Piloto automático: push de ${repo.branch} falhou: ${(e as Error).message}`);
      this.fm.notify('warn', `Push de ${repo.branch} falhou: ${(e as Error).message}`);
    }
  }

  private async openPr(repoId: string, task: Task | undefined): Promise<void> {
    const repo = this.fm.repos.get(repoId);
    if (!repo) return;
    const title = task ? `${task.id}: ${task.title}` : `TerrAgents: merge em ${repo.branch}`;
    const body = ['Aberto pelo piloto automático do TerrAgents depois do merge local com testes verdes.', task?.summary ? `Resumo do agente: ${task.summary}` : ''].filter(Boolean).join('\n\n');
    try {
      const url = await (this.opts.openPr ?? gitOpenPr)(repo.path, repo.branch, title, body);
      this.fm.bus.feed('merge', `Piloto automático: PR aberto para ${repo.branch}: ${url}`);
      this.fm.notify('need_user', `PR para revisar: ${url}`);
    } catch (e) {
      this.fm.bus.feed('error', `Piloto automático: abrir PR falhou: ${(e as Error).message}`);
      this.fm.notify('warn', `Abrir PR falhou: ${(e as Error).message}`);
    }
  }

  // ---- task caps ----------------------------------------------------------------------------

  private trackTask(t: Task): void {
    if (t.status === 'doing' && this.state.taskStart[t.id] === undefined) {
      this.state.taskStart[t.id] = this.fm.ctx.now();
      this.fm.store.markDirty();
    }
  }

  taskCostUsd(taskId: string): number {
    return Object.entries(this.fm.store.data.sessions)
      .filter(([k]) => k.endsWith(`:${taskId}`))
      .reduce((sum, [, s]) => sum + (s.costUsd || 0), 0);
  }

  checkTasks(): void {
    const now = this.fm.ctx.now();
    for (const t of this.fm.tasks.list()) {
      // only work in progress: a task in review already finished, and cancelling it threw the work away
      if (t.status !== 'doing') continue;
      const start = this.state.taskStart[t.id];
      const minutes = start === undefined ? 0 : (now - start) / 60_000;
      const usd = this.taskCostUsd(t.id);
      let why: string | undefined;
      if (this.policy.maxMinutesPerTask && minutes > this.policy.maxMinutesPerTask) why = `passou de ${this.policy.maxMinutesPerTask} min`;
      else if (this.policy.maxUsdPerTask && usd > this.policy.maxUsdPerTask) why = `passou de US$ ${this.policy.maxUsdPerTask}`;
      if (!why) continue;
      this.say(`cancelou ${t.id} (${why}); dá para tentar de novo com retry`);
      this.fm.taskAction(t.id, 'cancel');
    }
  }

  // ---- daily budget -------------------------------------------------------------------------

  private today(): string {
    const d = new Date(this.fm.ctx.now());
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  spentToday(): number {
    return Math.max(0, (this.fm.status.costUsd ?? 0) - this.state.dayStartUsd);
  }

  checkBudget(): void {
    const s = this.state;
    const day = this.today();
    if (s.day !== day) {
      s.day = day;
      s.dayStartUsd = this.fm.status.costUsd ?? 0;
      s.allowanceUsd = this.policy.dailyUsd;
      this.fm.store.markDirty();
      if (s.budgetDecision) {
        // new day: the cap resets; drop yesterday's question and resume
        const open = this.fm.decisions.get(s.budgetDecision);
        delete s.budgetDecision;
        if (open?.status === 'open') this.fm.decisions.cancel(open.id, 'novo dia, teto zerado');
        this.resumeAgents();
      }
    }
    const round = (n: number) => Math.round(n * 100) / 100;
    if (this.policy.dailyUsd) this.fm.setStatus({ dayUsd: round(this.spentToday()), dayCapUsd: round(s.allowanceUsd) }); // unchanged values do not re-emit
    if (!this.policy.dailyUsd || s.budgetDecision || this.spentToday() < s.allowanceUsd) return;
    s.pausedByBudget = this.fm.agents().filter((a) => a.active && !a.paused).map((a) => a.id);
    for (const id of s.pausedByBudget) void Promise.resolve(this.fm.agentAction(id, 'pause')).catch((e) => this.fm.log.warn(`pause ${id}: ${(e as Error).message}`));
    const spent = this.spentToday().toFixed(2);
    this.creatingHuman = true;
    try {
      const d = this.fm.createDecision({
        agentId: 'foreman',
        kind: 'question',
        question: `O gasto de hoje chegou a US$ ${spent} (teto US$ ${s.allowanceUsd.toFixed(2)}). Continuar hoje?`,
        options: [...BUDGET_OPTIONS],
        context: `"${BUDGET_OPTIONS[0]}" libera mais US$ ${this.policy.dailyUsd.toFixed(2)} e retoma os agentes. "${BUDGET_OPTIONS[1]}" deixa todos pausados até a virada do dia.`,
      });
      s.budgetDecision = d.id;
    } finally {
      this.creatingHuman = false;
    }
    this.fm.store.markDirty();
  }

  private onBudgetAnswer(d: Decision): void {
    const s = this.state;
    if (d.answer?.option === BUDGET_OPTIONS[0]) {
      s.allowanceUsd = this.spentToday() + this.policy.dailyUsd;
      delete s.budgetDecision;
      this.resumeAgents();
      this.say(`retomado com mais US$ ${this.policy.dailyUsd.toFixed(2)} para hoje`);
    } else {
      // keep budgetDecision set (answered): no new question today; the day change resumes
      this.say('agentes pausados até amanhã');
    }
    this.fm.store.markDirty();
  }

  private resumeAgents(): void {
    const ids = this.state.pausedByBudget;
    this.state.pausedByBudget = [];
    for (const id of ids) void Promise.resolve(this.fm.agentAction(id, 'resume')).catch((e) => this.fm.log.warn(`resume ${id}: ${(e as Error).message}`));
  }
}

/** Push the local base to terragents/<timestamp> and open a PR; merging it stays with a human. */
async function gitOpenPr(repoPath: string, branch: string, title: string, body: string): Promise<string> {
  const env = { ...process.env, GIT_TERMINAL_PROMPT: '0', GCM_INTERACTIVE: 'never' };
  const head = `terragents/${new Date().toISOString().replace(/[-:]/g, '').slice(0, 15)}`;
  // bring in what reached origin since the last PR (earlier PRs merged there, other people's work):
  // otherwise every new PR repeats old commits and conflicts with the base
  const fetch = await run('git', ['-c', `core.hooksPath=${NO_HOOKS_DIR}`, 'fetch', 'origin', branch], { cwd: repoPath, env, timeoutMs: 120_000 });
  if (fetch.code !== 0) throw new Error((fetch.stderr || fetch.stdout).trim().split('\n').pop() ?? 'git fetch failed');
  const merge = await run('git', ['-c', `core.hooksPath=${NO_HOOKS_DIR}`, 'merge', '--no-edit', `origin/${branch}`], { cwd: repoPath, env, timeoutMs: 120_000 });
  if (merge.code !== 0) {
    await run('git', ['merge', '--abort'], { cwd: repoPath, env, timeoutMs: 60_000 });
    throw new Error(`origin/${branch} conflita com o ${branch} local; resolva e abra o PR à mão (${(merge.stdout || merge.stderr).trim().split('\n').pop()})`);
  }
  const push = await run('git', ['-c', `core.hooksPath=${NO_HOOKS_DIR}`, 'push', 'origin', `${branch}:refs/heads/${head}`], { cwd: repoPath, env, timeoutMs: 120_000 });
  if (push.code !== 0) throw new Error((push.stderr || push.stdout).trim().split('\n').pop() ?? 'git push failed');
  const pr = await run('gh', ['pr', 'create', '--base', branch, '--head', head, '--title', title, '--body', body], { cwd: repoPath, env, timeoutMs: 120_000 });
  if (pr.code !== 0) throw new Error((pr.stderr || pr.stdout).trim().split('\n').pop() ?? 'gh pr create failed');
  return pr.stdout.trim().split('\n').pop() ?? '';
}

/** The Foreman's own git never uses a transport; this push is the one deliberate exception. */
async function gitPush(repoPath: string, branch: string): Promise<string> {
  const res = await run('git', ['-c', `core.hooksPath=${NO_HOOKS_DIR}`, 'push', 'origin', `${branch}:${branch}`], {
    cwd: repoPath,
    env: { ...process.env, GIT_TERMINAL_PROMPT: '0', GCM_INTERACTIVE: 'never' },
    timeoutMs: 120_000,
  });
  if (res.code !== 0) throw new Error((res.stderr || res.stdout).trim().split('\n').slice(-2).join(' | '));
  return (res.stderr || res.stdout).trim().split('\n').pop() ?? '';
}
