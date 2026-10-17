// The all-Claude team (`--backend claude`): the agent team (../team.ts) with the Claude engine for
// the lead and every worker.
import type { query } from '@anthropic-ai/claude-agent-sdk';
import type { ClaudeConfig } from '../../config.js';
import type { Foreman } from '../../foreman.js';
import { TeamBackend, type PullFetcher } from '../team.js';
import { ClaudeEngine } from './engine.js';

export { agentEnv, TeamBackend, type PullFetcher } from '../team.js';

export interface ClaudeBackendOptions {
  /** injectable for tests */
  queryFn?: typeof query;
  /** skip the startup auth probe (tests) */
  skipAuthCheck?: boolean;
  /** injectable for tests: pull request intake (default: git + gh, see pulls.ts) */
  pullFetcher?: PullFetcher;
}

export class ClaudeBackend extends TeamBackend {
  constructor(fm: Foreman, cfg: ClaudeConfig, opts: ClaudeBackendOptions = {}) {
    const claude = new ClaudeEngine(fm, cfg, opts.queryFn);
    super(fm, cfg, {
      name: 'claude',
      engines: { lead: claude, worker: claude },
      ...(opts.skipAuthCheck ? { skipAuthCheck: true } : {}),
      ...(opts.pullFetcher ? { pullFetcher: opts.pullFetcher } : {}),
    });
  }
}
