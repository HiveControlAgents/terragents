import { describe, expect, it } from 'vitest';
import { loadConfig } from '../src/config.js';

describe('--accept-risk', () => {
  it('is off unless the user says so (flag or env)', () => {
    expect(loadConfig(['--home', '/tmp/x', '--backend', 'sim'], {}).acceptRisk).toBe(false);
    expect(loadConfig(['--home', '/tmp/x', '--backend', 'sim', '--accept-risk'], {}).acceptRisk).toBe(true);
    expect(loadConfig(['--home', '/tmp/x', '--backend', 'sim'], { TERRAGENTS_ACCEPT_RISK: '1' }).acceptRisk).toBe(true);
  });
});
