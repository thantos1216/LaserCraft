import { Client } from '../../REA/node_modules/@modelcontextprotocol/client/dist/index.mjs';
import { StdioClientTransport } from '../../REA/node_modules/@modelcontextprotocol/client/dist/stdio.mjs';
import { writeFile } from 'node:fs/promises';
const transport = new StdioClientTransport({
  command: 'docker',
  args: ['exec', '-i', 'rea-rea-1', 'node', '/workspace/scripts/rea.mjs', '--mcp'],
  stderr: 'pipe',
});
const client = new Client({ name: 'lasercraft-integration-check', version: '0.1.0' });
try {
  await client.connect(transport);
  const { tools } = await client.listTools();
  const selected = tools.find(t => t.name === 'inspect_pe_resources');
  if (!selected) throw new Error('PE resource inspection tool was not advertised');
  // Explicitly staged by the operator; never scan or copy targets implicitly.
  const result = await client.callTool({ name: selected.name, arguments: { path: '/tmp/lasercraft-lightburn-reference.exe' } });
  if (result.isError) throw new Error(JSON.stringify(result));
  const summary = { server: client.getServerVersion(), toolCount: tools.length, smokeTest: selected.name, passed: true };
  await writeFile(new URL('../tmp/rea-mcp-handshake.json', import.meta.url), JSON.stringify(summary, null, 2));
  console.log(JSON.stringify(summary));
} finally { await client.close(); }
