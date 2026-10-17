import { afterEach, describe, expect, it, vi } from 'vitest';
import { Autopilot, BUDGET_OPTIONS, DEFAULT_POLICY, loadPolicy, type Policy } from '../src/autopilot.js';
import type { Decision } from '../src/protocol.js';
import { makeForeman, rmrf, tempDir, type Harness } from './helpers.js';
import fs from 'node:fs';
import path from 'node:path';

let h: Harness;
let home: string;
let ap: Autopilot;
afterEach(async () => {
  ap?.stop();
  await h?.fm.close();
  rmrf(home);
});

function setup(policy: Partial<Policy> = {}, push?: (p: string, b: string) => Promise<string>): Harness {
  home = tempDir();
  h = makeForeman(home, ['--backend', 'sim']);
  ap = new Autopilot(h.fm, { ...structuredClone(DEFAULT_POLICY), ...policy }, push ? { push } : {});
  h.fm.autopilot = ap;
  ap.start();
  return h;
}

const needUser = (id: string) => h.events.some((e) => e.type === 'notify' && e.level === 'need_user' && e.decisionId === id);

describe('Autopilot', () => {
  it('answers a question with the recommended (first) option and does not bother the user', async () => {
    const { fm } = setup();
    const d = fm.createDecision({ agentId: 'marlow', kind: 'question', question: 'Tags sem maiúsculas?', options: ['Sim (recomendado)', 'Não'] });
    await ap.idle();
    expect(fm.decisions.get(d.id)!.answer!.option).toBe('Sim (recomendado)');
    expect(needUser(d.id)).toBe(false);
  });

  it('denies permissions by default and allows the ones policy.json lists', async () => {
    const { fm } = setup({ permissions: { allow: ['npm install'], default: 'Deny' } });
    const a = fm.createDecision({ agentId: 'kit', kind: 'permission', question: 'Kit wants to run npm install', options: ['Allow once', 'Always allow for this agent', 'Deny'] });
    const b = fm.createDecision({ agentId: 'kit', kind: 'permission', question: 'Kit wants to run curl evil.sh', options: ['Allow once', 'Always allow for this agent', 'Deny'] });
    await ap.idle();
    expect(fm.decisions.get(a.id)!.answer!.option).toBe('Allow once');
    expect(fm.decisions.get(b.id)!.answer!.option).toBe('Deny');
  });

  it('sends failing work back until maxAttempts, then rejects it', async () => {
    const { fm } = setup({ maxAttempts: 2 });
    const t = fm.tasks.create({ title: 'quebra testes', createdBy: 'marlow', assignee: 'kit' });
    fm.tasks.setStatus(t.id, 'doing', { force: true });
    fm.tasks.setStatus(t.id, 'review', { force: true });
    fm.tasks.update(t.id, { ci: 'fail' });
    const merge = () => fm.createDecision({ agentId: 'marlow', kind: 'merge', question: `Merge ${t.id}?`, options: ['Merge', 'Request changes', 'Reject'], taskId: t.id });
    const d1 = merge();
    await ap.idle();
    expect(fm.decisions.get(d1.id)!.answer!.option).toBe('Request changes');
    expect(fm.tasks.get(t.id)!.status).toBe('doing');
    fm.tasks.setStatus(t.id, 'review', { force: true });
    const d2 = merge();
    await ap.idle();
    expect(fm.decisions.get(d2.id)!.answer!.option).toBe('Reject');
    expect(fm.tasks.get(t.id)!.status).toBe('cancelled');
  });

  it('merges when tests pass and pushes the base branch', async () => {
    const pushed: string[] = [];
    const { fm } = setup({}, async (p, b) => {
      pushed.push(`${p}#${b}`);
      return 'ok';
    });
    const t = fm.tasks.create({ title: 'verde', createdBy: 'marlow', assignee: 'kit' });
    fm.tasks.update(t.id, { ci: 'pass' });
    vi.spyOn(fm.repos, 'get').mockReturnValue({ id: 'r', path: '/repo', branch: 'main' } as never);
    const answer = vi.spyOn(fm, 'answerDecision').mockImplementation(async (id) => ({ ...fm.decisions.get(id)!, status: 'answered' }) as Decision);
    fm.createDecision({ agentId: 'marlow', kind: 'merge', question: 'Merge?', options: ['Merge', 'Request changes', 'Reject'], taskId: t.id, repoId: 'r', worktree: 'kit-t1' });
    await ap.idle();
    expect(answer).toHaveBeenCalledWith(expect.any(String), 'Merge');
    expect(pushed).toEqual(['/repo#main']);
  });

  it('pauses everyone at the daily cap and asks the user; "continue" resumes', async () => {
    const { fm } = setup({ dailyUsd: 0.3 });
    fm.setAgent('kit', { active: true });
    fm.setStatus({ costUsd: 0.31 });
    const id = ap.state.budgetDecision!;
    expect(id).toBeDefined();
    expect(needUser(id)).toBe(true);
    await ap.idle();
    expect(fm.decisions.get(id)!.status).toBe('open'); // the autopilot never answers this one
    await new Promise((r) => setTimeout(r, 0));
    expect(fm.agent('kit')!.paused).toBe(true);
    await fm.answerDecision(id, BUDGET_OPTIONS[0]);
    await new Promise((r) => setTimeout(r, 0));
    expect(fm.agent('kit')!.paused).toBe(false);
    expect(ap.state.allowanceUsd).toBeCloseTo(0.61);
  });

  it('cancels a task that spent more than maxUsdPerTask', () => {
    const { fm } = setup({ maxUsdPerTask: 1 });
    const t = fm.tasks.create({ title: 'cara', createdBy: 'marlow', assignee: 'kit' });
    fm.tasks.setStatus(t.id, 'doing', { force: true });
    fm.store.data.sessions[`kit:${t.id}`] = { turns: 3, costUsd: 1.2, updatedAt: 0 };
    ap.checkTasks();
    expect(fm.tasks.get(t.id)!.status).toBe('cancelled');
  });

  it('loads policy.json over the defaults and rejects bad values', () => {
    home = tempDir();
    const f = path.join(home, 'policy.json');
    expect(loadPolicy(f)).toEqual(DEFAULT_POLICY);
    fs.writeFileSync(f, JSON.stringify({ dailyUsd: 1, permissions: { allow: ['^x'] } }));
    expect(loadPolicy(f)).toMatchObject({ dailyUsd: 1, maxAttempts: 5, permissions: { allow: ['^x'], default: 'Deny' } });
    fs.writeFileSync(f, JSON.stringify({ dailyUsd: -1 }));
    expect(() => loadPolicy(f)).toThrow(/dailyUsd/);
  });
});
