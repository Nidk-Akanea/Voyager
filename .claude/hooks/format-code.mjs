// PostToolUse hook: formats the code file Claude just wrote or edited.
// C# goes through `dotnet format whitespace` (no build needed, about a second);
// TypeScript, JavaScript and JSON go through Prettier, which honours .prettierignore.
// Reports failures back to Claude (exit code 2 feeds stderr to the model).
import { spawnSync } from 'node:child_process';
import path from 'node:path';

const CSHARP = /\.(cs|csx)$/i;
const PRETTIER = /\.(ts|tsx|js|mjs|cjs|json|yaml|yml)$/i;

let input = '';
for await (const chunk of process.stdin) {
  input += chunk;
}

const payload = JSON.parse(input || '{}');
const filePath = payload.tool_input?.file_path ?? payload.tool_response?.filePath;
if (!filePath || !(CSHARP.test(filePath) || PRETTIER.test(filePath))) {
  process.exit(0);
}

const projectDir = process.env.CLAUDE_PROJECT_DIR ?? process.cwd();
const relativePath = path.relative(projectDir, filePath).split(path.sep).join('/');
// Files outside the repository, including on another drive, are not ours to change.
if (relativePath.startsWith('..') || path.isAbsolute(relativePath)) {
  process.exit(0);
}

const [command, args] = CSHARP.test(relativePath)
  ? ['dotnet', ['format', 'whitespace', '.', '--folder', '--include', `"${relativePath}"`]]
  : ['pnpm', ['exec', 'prettier', '--write', '--ignore-unknown', `"${relativePath}"`]];

const result = spawnSync(command, args, {
  cwd: projectDir,
  encoding: 'utf8',
  shell: true,
  env: { ...process.env, COREPACK_ENABLE_DOWNLOAD_PROMPT: '0' },
});

if (result.status === 0) {
  process.exit(0);
}

process.stderr.write(`Formatting failed for ${relativePath}:\n${result.stdout ?? ''}${result.stderr ?? ''}${result.error?.message ?? ''}`);
process.exit(2);
