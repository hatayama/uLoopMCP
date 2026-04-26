#!/usr/bin/env tsx
/**
 * Static linter for bundled SKILL.md sources.
 *
 * Why this exists:
 * The empirical-prompt-tuning rollout (run locally via the
 * empirical-prompt-tuning skill) found two recurring gap families that any
 * new bundled skill is likely to repeat:
 *  1. description missing a "How" mechanism tail (CLAUDE.md What -> When -> How)
 *  2. body missing or stubbing the `## Output` section, which forces the
 *     closed-book agent to fabricate response fields
 * This script scans every bundled SKILL.md so those gaps surface before a new
 * skill ships, without re-running the full closed-book pipeline.
 *
 * Targets (matches skills-manager.collectAllSkills()):
 *  - Packages/src/Editor/Api/McpTools/<X>/Skill/SKILL.md
 *  - Packages/src/Cli~/src/skills/skill-definitions/cli-only/<X>/Skill/SKILL.md
 * Files with `internal: true` in frontmatter are skipped.
 */

import { readFileSync, readdirSync, existsSync, statSync } from 'node:fs';
import { join, resolve, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const SCRIPT_DIR = resolve(__filename, '..');
const CLI_ROOT = resolve(SCRIPT_DIR, '..');
const PACKAGE_SRC_ROOT = resolve(CLI_ROOT, '..');
const REPO_ROOT = resolve(PACKAGE_SRC_ROOT, '..', '..');

const MCP_TOOLS_ROOT = join(PACKAGE_SRC_ROOT, 'Editor', 'Api', 'McpTools');
const CLI_ONLY_ROOT = join(CLI_ROOT, 'src', 'skills', 'skill-definitions', 'cli-only');

type Severity = 'error' | 'warn';

interface Finding {
  rule: string;
  severity: Severity;
  message: string;
}

interface SkillFile {
  path: string;
  relPath: string;
  source: 'mcp-tool' | 'cli-only';
}

interface ParsedSkill {
  frontmatter: Record<string, string | boolean>;
  body: string;
  description: string;
}

const HOW_KEYWORDS = [
  'via',
  'through',
  'routes through',
  'executes',
  'calls',
  'sends',
  'communicates',
  'invokes',
  'wraps',
  'uses',
  'tcp',
  'cli',
  'osascript',
  'powershell',
  'shell out',
  'shells out',
  'spawns',
  'pipe',
  'unity api',
  'unity editor api',
  'editor api',
  'eventsystem',
  'graphicraycaster',
  'mouse.current',
  'input system',
  'reflection',
  'domain reload',
  'menu item',
  'play mode',
  'subprocess',
];

const WHEN_PATTERNS = [
  /\bUse when\b/i,
  /\bWhen you (?:need|want)\b/i,
  /\(1\)[^.]+\(2\)/,
];

function discoverSkillFiles(): SkillFile[] {
  const files: SkillFile[] = [];

  if (existsSync(MCP_TOOLS_ROOT)) {
    for (const entry of readdirSync(MCP_TOOLS_ROOT, { withFileTypes: true })) {
      if (!entry.isDirectory()) continue;
      const candidate = join(MCP_TOOLS_ROOT, entry.name, 'Skill', 'SKILL.md');
      if (existsSync(candidate) && statSync(candidate).isFile()) {
        files.push({
          path: candidate,
          relPath: relative(REPO_ROOT, candidate),
          source: 'mcp-tool',
        });
      }
    }
  }

  if (existsSync(CLI_ONLY_ROOT)) {
    for (const entry of readdirSync(CLI_ONLY_ROOT, { withFileTypes: true })) {
      if (!entry.isDirectory()) continue;
      const candidate = join(CLI_ONLY_ROOT, entry.name, 'Skill', 'SKILL.md');
      if (existsSync(candidate) && statSync(candidate).isFile()) {
        files.push({
          path: candidate,
          relPath: relative(REPO_ROOT, candidate),
          source: 'cli-only',
        });
      }
    }
  }

  files.sort((a, b) => a.relPath.localeCompare(b.relPath));
  return files;
}

function parseFrontmatter(content: string): { frontmatter: Record<string, string | boolean>; body: string } {
  const match = content.match(/^---\r?\n([\s\S]*?)\r?\n---\r?\n?([\s\S]*)$/);
  if (!match) {
    return { frontmatter: {}, body: content };
  }

  const fm: Record<string, string | boolean> = {};
  const lines = match[1].split(/\r?\n/);
  let pendingKey: string | null = null;
  let pendingValue: string[] = [];

  const flush = (): void => {
    if (pendingKey !== null) {
      fm[pendingKey] = pendingValue.join('\n').trim();
      pendingKey = null;
      pendingValue = [];
    }
  };

  for (const line of lines) {
    const colonIdx = line.indexOf(':');
    const looksLikeKey = colonIdx !== -1 && /^[A-Za-z][A-Za-z0-9_-]*$/.test(line.slice(0, colonIdx).trim());
    if (looksLikeKey) {
      flush();
      const key = line.slice(0, colonIdx).trim();
      let raw = line.slice(colonIdx + 1).trim();
      if ((raw.startsWith('"') && raw.endsWith('"')) || (raw.startsWith("'") && raw.endsWith("'"))) {
        raw = raw.slice(1, -1);
      }
      pendingKey = key;
      pendingValue = [raw];
    } else if (pendingKey !== null) {
      pendingValue.push(line);
    }
  }
  flush();

  for (const [k, v] of Object.entries(fm)) {
    if (v === 'true') fm[k] = true;
    else if (v === 'false') fm[k] = false;
  }

  return { frontmatter: fm, body: match[2] };
}

function parseSkill(file: SkillFile): ParsedSkill {
  const content = readFileSync(file.path, 'utf-8');
  const { frontmatter, body } = parseFrontmatter(content);
  const desc = typeof frontmatter.description === 'string' ? frontmatter.description : '';
  return { frontmatter, body, description: desc };
}

function findSection(
  body: string,
  heading: string,
  level: 2 | 3 | 'any' = 2,
): { present: boolean; content: string } {
  const hashes = level === 2 ? '##' : level === 3 ? '###' : '#{2,3}';
  const re = new RegExp(`(^|\\n)${hashes}\\s+${heading}[^\\n]*\\n([\\s\\S]*?)(?=\\n#{2,3}\\s+|$)`, 'i');
  const match = body.match(re);
  if (!match) return { present: false, content: '' };
  return { present: true, content: match[2].trim() };
}

function findFirstSection(
  body: string,
  headings: string[],
  level: 2 | 3 | 'any' = 2,
): { present: boolean; content: string; matched: string | null } {
  for (const h of headings) {
    const found = findSection(body, h, level);
    if (found.present) return { ...found, matched: h };
  }
  return { present: false, content: '', matched: null };
}

function checkDescription(desc: string, findings: Finding[]): void {
  if (desc.length === 0) {
    findings.push({ rule: 'DESC-001', severity: 'error', message: 'frontmatter `description` is missing or empty' });
    return;
  }

  const lower = desc.toLowerCase();

  const hasWhen = WHEN_PATTERNS.some((p) => p.test(desc));
  if (!hasWhen) {
    findings.push({
      rule: 'DESC-002',
      severity: 'error',
      message: 'description has no When clause (expected "Use when you need to: (1)..., (2)...")',
    });
  }

  const hasHow = HOW_KEYWORDS.some((kw) => lower.includes(kw));
  if (!hasHow) {
    findings.push({
      rule: 'DESC-003',
      severity: 'error',
      message: `description has no How tail (expected mechanism keyword like "via", "Executes", "Routes through", "uloop CLI", etc.)`,
    });
  }

  if (desc.length < 80) {
    findings.push({
      rule: 'DESC-004',
      severity: 'warn',
      message: `description is suspiciously short (${desc.length} chars); What -> When -> How rarely fits under 80 chars`,
    });
  }
}

function looksLikeFieldEnumeration(content: string): boolean {
  const backtickedListItems = content.match(/^\s*[-*]\s+`[A-Za-z_][A-Za-z0-9_]*`/gm);
  if (backtickedListItems && backtickedListItems.length >= 1) return true;
  const boldedListItems = content.match(/^\s*[-*]\s+\*\*[A-Za-z_][A-Za-z0-9_]*\*\*/gm);
  if (boldedListItems && boldedListItems.length >= 1) return true;
  const tableRows = content.match(/^\|[^|\n]+\|/gm);
  if (tableRows && tableRows.length >= 2) return true;
  // CLI-only skills (e.g. uloop launch) print to stdout rather than returning a JSON object;
  // their Output section is a narrative bullet list. Accept any list of >=2 items as well.
  const plainBullets = content.match(/^\s*[-*]\s+\S/gm);
  if (plainBullets && plainBullets.length >= 2) return true;
  return false;
}

function checkOutput(body: string, findings: Finding[]): void {
  const section = findSection(body, 'Output');
  if (!section.present) {
    findings.push({
      rule: 'BODY-001',
      severity: 'error',
      message: '`## Output` section is missing — the closed-book agent will invent response fields',
    });
    return;
  }

  const contentLines = section.content.split('\n').filter((l) => l.trim().length > 0);
  if (contentLines.length <= 1) {
    findings.push({
      rule: 'BODY-002',
      severity: 'error',
      message: `\`## Output\` section is a one-line stub (${contentLines.length} non-blank lines); enumerate the response fields`,
    });
    return;
  }

  if (!looksLikeFieldEnumeration(section.content)) {
    findings.push({
      rule: 'BODY-003',
      severity: 'warn',
      message: '`## Output` section does not look like a field enumeration (no `- `name``, `- **name**`, or table); the agent may not know which fields to surface',
    });
  }
}

function checkRequiredSections(body: string, findings: Finding[]): void {
  // Accept established alternative section names so the linter does not nag
  // skills that already follow a working convention. The Output check above is
  // strict because that gap reliably caused fabrication; these are stylistic.
  const usageAliases = ['Usage', 'Tool Reference', 'Workflow'];
  const usage = findFirstSection(body, usageAliases, 2);
  if (!usage.present) {
    findings.push({
      rule: 'BODY-010',
      severity: 'warn',
      message: `no usage-equivalent section found (looked for: ${usageAliases.map((h) => `\`## ${h}\``).join(', ')})`,
    });
  }

  // Parameters may live at H2 (`## Parameters`) or H3 nested under Tool Reference
  // (`## Tool Reference\n### Parameters`). Accept either.
  const paramsH2 = findSection(body, 'Parameters', 2);
  const paramsH3 = findSection(body, 'Parameters', 3);
  if (!paramsH2.present && !paramsH3.present) {
    findings.push({
      rule: 'BODY-011',
      severity: 'warn',
      message: '`## Parameters` (or `### Parameters`) section is missing',
    });
  }

  // Examples may be a dedicated section or be folded into the Usage section as a
  // bash code block. Accept either, plus `Code Examples by Category` for skills
  // (e.g. ExecuteDynamicCode) whose examples are code snippets, not CLI calls.
  const exampleAliases = ['Examples', 'Code Examples by Category'];
  const examples = findFirstSection(body, exampleAliases, 2);
  const usageHasCodeBlock = usage.present && /```[\s\S]+```/.test(usage.content);
  if (!examples.present && !usageHasCodeBlock) {
    findings.push({
      rule: 'BODY-012',
      severity: 'warn',
      message: `no examples-equivalent found (looked for: ${exampleAliases.map((h) => `\`## ${h}\``).join(', ')}, or a code block inside the usage section)`,
    });
  }
}

interface Report {
  file: SkillFile;
  skipped?: 'internal';
  findings: Finding[];
}

function lintFile(file: SkillFile): Report {
  const skill = parseSkill(file);
  if (skill.frontmatter.internal === true) {
    return { file, skipped: 'internal', findings: [] };
  }

  const findings: Finding[] = [];
  checkDescription(skill.description, findings);
  checkOutput(skill.body, findings);
  checkRequiredSections(skill.body, findings);
  return { file, findings };
}

function format(report: Report): string {
  const lines: string[] = [];
  lines.push(`\n${report.file.relPath}`);
  if (report.skipped === 'internal') {
    lines.push('  [skipped] internal: true');
    return lines.join('\n');
  }
  if (report.findings.length === 0) {
    lines.push('  OK');
    return lines.join('\n');
  }
  for (const f of report.findings) {
    const tag = f.severity === 'error' ? 'ERROR' : 'WARN ';
    lines.push(`  ${tag} ${f.rule}: ${f.message}`);
  }
  return lines.join('\n');
}

function main(): void {
  const files = discoverSkillFiles();
  if (files.length === 0) {
    console.error('No bundled SKILL.md files found. Did the layout move?');
    process.exit(2);
  }

  const reports = files.map(lintFile);

  let errorCount = 0;
  let warnCount = 0;
  let okCount = 0;
  let skippedCount = 0;

  for (const r of reports) {
    process.stdout.write(format(r));
    if (r.skipped) {
      skippedCount++;
      continue;
    }
    const errs = r.findings.filter((f) => f.severity === 'error').length;
    const warns = r.findings.filter((f) => f.severity === 'warn').length;
    errorCount += errs;
    warnCount += warns;
    if (errs === 0 && warns === 0) okCount++;
  }

  process.stdout.write('\n\nSummary\n');
  process.stdout.write(`  files scanned : ${reports.length}\n`);
  process.stdout.write(`  internal skipped: ${skippedCount}\n`);
  process.stdout.write(`  clean         : ${okCount}\n`);
  process.stdout.write(`  errors        : ${errorCount}\n`);
  process.stdout.write(`  warnings      : ${warnCount}\n`);

  process.exit(errorCount > 0 ? 1 : 0);
}

main();
