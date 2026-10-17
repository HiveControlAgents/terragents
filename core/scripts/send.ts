// Send one client message to the running Core: npm run send -- '{"type":"task.action","taskId":"t1","action":"retry"}' [port]
import WebSocket from 'ws';

const [json, port = '7878'] = process.argv.slice(2);
if (!json) {
  console.error(`usage: send '<client message json>' [port]`);
  process.exit(2);
}
const ws = new WebSocket(`ws://127.0.0.1:${port}`);
ws.on('open', () => ws.send(JSON.stringify({ v: 1, id: 'cli1', ...JSON.parse(json) })));
ws.on('message', (raw) => {
  const m = JSON.parse(String(raw)) as { type: string; re?: string; ok?: boolean; error?: string };
  if (m.type === 'ack' && m.re === 'cli1') {
    console.log(m.ok ? 'ok' : `refused: ${m.error}`);
    process.exitCode = m.ok ? 0 : 1;
    ws.close();
  }
});
ws.on('error', (e) => {
  console.error(e.message);
  process.exit(1);
});
