import http from 'k6/http';
import ws from 'k6/ws';
import { check, fail } from 'k6';
import { Counter } from 'k6/metrics';
const disconnects = new Counter('unexpected_disconnects');
const separator = String.fromCharCode(30);
const base = __ENV.BASE_URL;
const token = __ENV.SIGNALR_ACCESS_TOKEN;
if (!/^https:\/\/[^/]+$/.test(base || '') || !token) throw new Error('HTTPS BASE_URL and private SIGNALR_ACCESS_TOKEN required');
export const options = {
    scenarios: { connections: { executor: 'per-vu-iterations', vus: 50, iterations: 1, maxDuration: '32m' } },
    thresholds: { checks: ['rate==1'], unexpected_disconnects: ['count==0'] },
};
export default function () {
    const negotiation = http.post(`${base}/hubs/operations/negotiate?negotiateVersion=1`, null,
        { headers: { Authorization: `Bearer ${token}` }, redirects: 0, tags: { name: 'SignalR negotiate' } });
    if (!check(negotiation, { 'negotiation succeeded': r => r.status === 200 })) fail('SignalR negotiation failed');
    const connectionToken = negotiation.json('connectionToken');
    if (!connectionToken) fail('Missing SignalR connection token');
    let completed = false;
    let handshake = false;
    let buffer = '';
    const response = ws.connect(`${base.replace('https:', 'wss:')}/hubs/operations?id=${encodeURIComponent(connectionToken)}`,
        { headers: { Authorization: `Bearer ${token}` }, tags: { name: 'SignalR socket' } }, socket => {
            socket.on('open', () => socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + separator));
            socket.on('message', data => {
                buffer += data;
                let boundary;
                while ((boundary = buffer.indexOf(separator)) >= 0) {
                    const frame = JSON.parse(buffer.substring(0, boundary));
                    buffer = buffer.substring(boundary + 1);
                    if (!handshake && !frame.error && frame.type === undefined) handshake = true;
                    if (frame.error || frame.type === 7) socket.close();
                }
            });
            socket.setInterval(() => { if (handshake) socket.send('{"type":6}' + separator); }, 15000);
            socket.setTimeout(() => { completed = handshake; socket.close(); }, 1800000);
            socket.on('error', () => { completed = false; });
        });
    disconnects.add(completed ? 0 : 1);
    check(response, { 'upgrade 101 and held 30 minutes': r => r && r.status === 101 && completed });
}
