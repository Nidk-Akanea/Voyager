// PostToolUse hook: lints the Markdown file Claude just wrote or edited, applies safe fixes,
// and reports remaining issues back to Claude (exit code 2 feeds stderr to the model).
import { spawnSync } from 'node:child_process';
import path from 'node:path';

let input = '';
for await (const chunk of process.stdin) {
  input += chunk;
}

const payload = JSON.parse(input || '{}');
const filePath = payload.tool_input?.file_path ?? payload.tool_response?.filePath;
if (!filePath || !filePath.toLowerCase().endsWith('.md')) {
  process.exit(0);
}

const projectDir = process.env.CLAUDE_PROJECT_DIR ?? process.cwd();
const relativePath = path.relative(projectDir, filePath).split(path.sep).join('/');
if (relativePath.startsWith('..')) {
  process.exit(0);
}

// ":" marks a literal path, so folder names such as "[Dev Perso]" are not read as globs.
// "--no-globs" lints only this file instead of the whole repository.
const result = spawnSync(
  'npx',
  ['--yes', 'markdownlint-cli2', '--no-globs', '--fix', `":${relativePath}"`],
  { cwd: projectDir, encoding: 'utf8', shell: true },
);

if (result.status === 0) {
  process.exit(0);
}

process.stderr.write(
  `markdownlint found issues it could not fix in ${relativePath}:\n${result.stdout}${result.stderr}`,
);
process.exit(2);
