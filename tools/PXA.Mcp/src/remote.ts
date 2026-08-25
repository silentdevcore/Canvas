import { createServer } from 'node:http';

import { createRemoteMcpApp } from './remote/app.js';
import { readRemoteMcpConfig } from './remote/config.js';

const config = readRemoteMcpConfig();
const httpServer = createServer({
  maxHeaderSize: 16 * 1024,
  requestTimeout: 35_000,
  headersTimeout: 10_000,
}, createRemoteMcpApp(config));

httpServer.listen(config.port, config.host, () => {
  console.error(`pxa-mcp remote ready - ${config.resourceUri.href}`);
});

for (const signal of ['SIGINT', 'SIGTERM'] as const) {
  process.once(signal, () => {
    httpServer.close((error) => {
      if (error) {
        console.error('pxa-mcp remote shutdown failed', { error: error.name });
        process.exitCode = 1;
      }
    });
  });
}
