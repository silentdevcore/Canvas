import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';

import { createPxaMcpServer } from './core/server.js';

export async function startStdioServer(): Promise<void> {
  const apiUrl = process.env.PXA_API_URL ?? 'http://localhost:5086';
  const server = createPxaMcpServer({
    apiUrl,
    apiKey: process.env.PXA_API_KEY,
  });

  await server.connect(new StdioServerTransport());
  console.error(`pxa-mcp ready - API ${apiUrl}`);
}
