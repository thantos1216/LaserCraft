import { Client } from '../../REA/rea/node_modules/@modelcontextprotocol/client/dist/index.mjs';
import { StdioClientTransport } from '../../REA/rea/node_modules/@modelcontextprotocol/client/dist/stdio.mjs';
import { writeFile } from 'node:fs/promises';
const transport = new StdioClientTransport({
  command: 'C:\\Users\\thant\\.cache\\codex-runtimes\\codex-primary-runtime\\dependencies\\node\\bin\\node.exe',
  args: ['D:\\Development\\REA\\rea\\scripts\\rea.mjs', 'mcp'],
  stderr: 'pipe',
});
const client = new Client({ name: 'lasercraft-integration-check', version: '0.1.0' });
try {
  await client.connect(transport);
  const { tools } = await client.listTools();
  const selected = tools.find(t => t.name === 'inspect_managed_artifact');
  if (!selected) throw new Error('Managed inspection tool was not advertised');
  const result = await client.callTool({ name: selected.name, arguments: { path: 'D:\\Development\\LaserCraft\\src\\LaserCraft.Domain\\bin\\Debug\\net10.0\\LaserCraft.Domain.dll', detail: 'summary' } });
  if (result.isError) throw new Error(JSON.stringify(result));
  const summary = { server: client.getServerVersion(), toolCount: tools.length, smokeTest: 'inspect_managed_artifact', passed: true };
  await writeFile(new URL('../tmp/rea-mcp-handshake.json', import.meta.url), JSON.stringify(summary, null, 2));
  console.log(JSON.stringify(summary));
} finally { await client.close(); }
