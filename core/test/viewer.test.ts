import type { IncomingMessage } from 'node:http';
import { describe, expect, it } from 'vitest';
import { refuseReason } from '../src/server.js';

const req = (headers: Record<string, string>) => ({ headers }) as unknown as IncomingMessage;
const self = ['http://127.0.0.1:7878', 'http://localhost:7878'];

describe('browser view origin', () => {
  it('lets in only the page the Core serves itself', () => {
    expect(refuseReason(req({ host: '127.0.0.1:7878', origin: 'http://127.0.0.1:7878' }), false, self)).toBeUndefined();
    expect(refuseReason(req({ host: '127.0.0.1:7878', origin: 'https://evil.example' }), false, self)).toMatch(/browser origin/);
    expect(refuseReason(req({ host: '127.0.0.1:7878', origin: 'null' }), false, self)).toMatch(/browser origin/);
    expect(refuseReason(req({ host: 'evil.example:7878', origin: 'http://127.0.0.1:7878' }), false, self)).toMatch(/Host/);
    expect(refuseReason(req({ host: '127.0.0.1:7878' }), false, self)).toBeUndefined(); // the mod and CLI tools
  });
});
