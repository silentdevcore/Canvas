import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';

import { createPvadMcpServer } from './core/server.js';
import { normalizePvadEnvironment } from '../../configuration/environment.mjs';

export async function startStdioServer(): Promise<void> {
  const environment = normalizePvadEnvironment();
  const apiUrl = environment.PVAD_API_URL ?? 'http://localhost:5086';
  const server = createPvadMcpServer({
    apiUrl,
    apiKey: environment.PVAD_API_KEY,
  });

  await server.connect(new StdioServerTransport());
  console.error(`pvad-mcp ready - API ${apiUrl}`);
}
