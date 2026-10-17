// Answer one open decision from the command line: npm run answer -- <decisionId> "<option>" [port]
import WebSocket from 'ws';

const [decisionId, option, port = '7878'] = process.argv.slice(2);
if (!decisionId || !option) {
  console.error('usage: answer <decisionId> "<option>" [port]');
  process.exit(2);
}
const ws = new WebSocket(`ws://127.0.0.1:${port}`);
ws.on('open', () => ws.send(JSON.stringify({ v: 1, type: 'decision.answer', id: 'cli1', decisionId, option })));
ws.on('message', (raw) => {
  const m = JSON.parse(String(raw)) as { type: string; re?: string; ok?: boolean; error?: string };
  if (m.type === 'ack' && m.re === 'cli1') {
    console.log(m.ok ? `answered ${decisionId}: ${option}` : `refused: ${m.error}`);
    process.exitCode = m.ok ? 0 : 1;
    ws.close();
  }
});
ws.on('error', (e) => {
  console.error(e.message);
  process.exit(1);
});
