import assert from 'node:assert/strict';
import { createHash, createHmac, timingSafeEqual } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { createServer as createHttpsServer } from 'node:https';
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';

export function verifyDelivery(body, headers, contract, secret, now = Date.now()) {
  const timestamp = headers[contract.timestampHeader.toLowerCase()];
  assert.match(timestamp ?? '', /^\d{10}$/);
  assert.ok(Math.abs(now / 1000 - Number(timestamp)) <= 60, 'Expired webhook timestamp.');
  const supplied = headers[contract.signatureHeader.toLowerCase()];
  const expected = `sha256=${createHmac('sha256', secret).update(timestamp + '\n').update(body).digest('hex')}`;
  assert.equal(Buffer.byteLength(supplied ?? ''), Buffer.byteLength(expected));
  assert.ok(timingSafeEqual(Buffer.from(supplied), Buffer.from(expected)), 'Invalid webhook signature.');
  const digest = createHash('sha256').update(body).digest('hex');
  assert.equal(headers[contract.idempotencyHeader.toLowerCase()], digest, 'Invalid idempotency digest.');
  const payload = JSON.parse(body);
  assert.ok(['firing', 'resolved'].includes(payload.status));
  assert.ok(Array.isArray(payload.alerts) && payload.alerts.length === 1);
  assert.deepEqual(Object.keys(payload).sort(), ['alerts', 'status']);
  const alert = payload.alerts[0];
  assert.deepEqual(Object.keys(alert.labels).sort(), ['alertname', 'environment', 'service', 'severity']);
  assert.equal(alert.labels.alertname, contract.alertName);
  assert.equal(alert.status, payload.status);
  assert.ok(Object.keys(alert.annotations).every(key => ['summary', 'description', 'dashboard_path', 'runbook_id'].includes(key)));
  return { status: payload.status, payloadSha256: digest, signatureVerified: true, idempotencyVerified: true };
}

export function startReceiver(directory) {
  const contract = JSON.parse(readFileSync(resolve(directory, 'receiver.json')));
  const secret = readFileSync(resolve(directory, 'signing.key'));
  const events = [];
  let rejectedOnce = false, invalid = 0;
  const delivery = createHttpsServer({ key: readFileSync(resolve(directory, 'receiver.key')),
    cert: readFileSync(resolve(directory, 'receiver.crt')), minVersion: 'TLSv1.2' }, async (request, response) => {
    if (request.method !== 'POST' || request.url !== '/alerts') { response.writeHead(404).end(); return; }
    try {
      const chunks = []; let size = 0;
      for await (const chunk of request) {
        size += chunk.length;
        assert.ok(size <= 64 * 1024, 'Synthetic delivery exceeded its bound.');
        chunks.push(chunk);
      }
      const event = verifyDelivery(Buffer.concat(chunks), request.headers, contract, secret);
      // One real transient failure proves Alertmanager retries the actual relay.
      const accepted = rejectedOnce;
      rejectedOnce = true;
      assert.ok(events.length < 100, 'Unbounded alert delivery.');
      events.push({ ...event, accepted });
      response.writeHead(accepted ? 202 : 503).end();
    } catch {
      invalid++;
      response.writeHead(400).end();
    }
  });
  const control = createServer((request, response) => {
    if (request.method !== 'GET' || !['/health', '/events'].includes(request.url)) { response.writeHead(404).end(); return; }
    response.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
    response.end(JSON.stringify(request.url === '/health' ? { ready: true } : { events, invalid }));
  });
  delivery.requestTimeout = control.requestTimeout = 5000;
  delivery.listen(8443, '0.0.0.0');
  control.listen(8080, '0.0.0.0');
  const stop = () => { delivery.close(); control.close(); secret.fill(0); };
  process.once('SIGTERM', stop);
  process.once('SIGINT', stop);
  return { delivery, control };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  assert.equal(process.argv.length, 3);
  startReceiver(process.argv[2]);
}
