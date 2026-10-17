import { afterEach, describe, expect, it } from 'vitest';
import { makeForeman, rmrf, tempDir, type Harness } from './helpers.js';

let h: Harness;
let home: string;
afterEach(async () => {
  await h?.fm.close();
  rmrf(home);
});

describe('agent energy', () => {
  it('drains while an agent works and refills while it rests', () => {
    home = tempDir();
    h = makeForeman(home, ['--backend', 'sim']);
    const { fm } = h;
    fm.setAgent('kit', { active: true, state: 'editing', station: 'desk' });
    fm.tickEnergy(600); // 10 min working: -10%
    expect(fm.agent('kit')!.energy).toBe(0.9);
    fm.setAgent('kit', { state: 'idle', station: 'lounge' });
    fm.tickEnergy(150); // 2.5 min resting: +10%
    expect(fm.agent('kit')!.energy).toBe(1);
  });
});
