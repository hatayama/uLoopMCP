/**
 * Skills are scattered across multiple directories (McpTools, cli-only).
 * Without this generator, we'd need manual synchronization between source
 * SKILL.md files and the bundled output, which is error-prone and tedious.
 */

import { readdirSync, readFileSync, writeFileSync, existsSync } from 'fs';
import { join, dirname, relative } from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const MCPTOOLS_DIR = join(__dirname, '../../Editor/Api/McpTools');
const CLI_ONLY_DIR = join(__dirname, '../src/skills/skill-definitions/cli-only');
const OUTPUT_FILE = join(__dirname, '../src/skills/bundled-skills.ts');
const OUTPUT_DIR = dirname(OUTPUT_FILE);

interface SkillMetadata {
  dirName: string;
  name: string;
  isInternal: boolean;
  importPath: string;
  skillDirPath: string;
}

function parseFrontmatter(content: string): Record<string, string | boolean> {
  const frontmatterMatch = content.match(/^---\n([\s\S]*?)\n---/);
  if (!frontmatterMatch) {
    return {};
  }

  const frontmatter: Record<string, string | boolean> = {};
  const lines = frontmatterMatch[1].split('\n');

  for (const line of lines) {
    const colonIndex = line.indexOf(':');
    if (colonIndex === -1) {
      continue;
    }

    const key = line.slice(0, colonIndex).trim();
    const value = line.slice(colonIndex + 1).trim();

    if (value === 'true') {
      frontmatter[key] = true;
    } else if (value === 'false') {
      frontmatter[key] = false;
    } else {
      frontmatter[key] = value;
    }
  }

  return frontmatter;
}

function getSkillMetadataFromPath(
  skillMdPath: string,
  folderName: string
): SkillMetadata | null {
  if (!existsSync(skillMdPath)) {
    return null;
  }

  const content = readFileSync(skillMdPath, 'utf-8');
  const frontmatter = parseFrontmatter(content);

  const name = typeof frontmatter.name === 'string' ? frontmatter.name : folderName;
  const isInternal = frontmatter.internal === true;
  const dirName = name;

  const relativePath = relative(OUTPUT_DIR, skillMdPath).replace(/\\/g, '/');
  const importPath = relativePath.startsWith('.') ? relativePath : `./${relativePath}`;
  const skillDirPath = dirname(skillMdPath);

  return { dirName, name, isInternal, importPath, skillDirPath };
}

function collectAdditionalFiles(skillDirPath: string): Record<string, string> {
  const examplesDir = join(skillDirPath, 'examples');
  if (!existsSync(examplesDir)) {
    return {};
  }

  const files: Record<string, string> = {};
  const entries = readdirSync(examplesDir, { withFileTypes: true });

  for (const entry of entries) {
    if (entry.isFile() && entry.name.endsWith('.md')) {
      const filePath = join(examplesDir, entry.name);
      const relativePath = `examples/${entry.name}`;
      files[relativePath] = readFileSync(filePath, 'utf-8');
    }
  }

  return files;
}

function collectSkillsFromMcpTools(): SkillMetadata[] {
  if (!existsSync(MCPTOOLS_DIR)) {
    console.warn(`Warning: McpTools directory not found: ${MCPTOOLS_DIR}`);
    return [];
  }

  const dirs = readdirSync(MCPTOOLS_DIR, { withFileTypes: true })
    .filter((dirent) => dirent.isDirectory())
    .map((dirent) => dirent.name)
    .sort();

  const skills: SkillMetadata[] = [];

  for (const dirName of dirs) {
    const skillPath = join(MCPTOOLS_DIR, dirName, 'SKILL.md');
    const metadata = getSkillMetadataFromPath(skillPath, dirName);
    if (metadata !== null) {
      skills.push(metadata);
    }
  }

  return skills;
}

function collectSkillsFromCliOnly(): SkillMetadata[] {
  if (!existsSync(CLI_ONLY_DIR)) {
    console.warn(`Warning: CLI-only directory not found: ${CLI_ONLY_DIR}`);
    return [];
  }

  const dirs = readdirSync(CLI_ONLY_DIR, { withFileTypes: true })
    .filter((dirent) => dirent.isDirectory())
    .map((dirent) => dirent.name)
    .sort();

  const skills: SkillMetadata[] = [];

  for (const dirName of dirs) {
    const skillPath = join(CLI_ONLY_DIR, dirName, 'SKILL.md');
    const metadata = getSkillMetadataFromPath(skillPath, dirName);
    if (metadata !== null) {
      skills.push(metadata);
    }
  }

  return skills;
}

function toVariableName(dirName: string): string {
  return dirName
    .replace(/^uloop-/, '')
    .replace(/-([a-z])/g, (_, char: string) => char.toUpperCase())
    .concat('Skill');
}

function escapeStringForTemplate(str: string): string {
  return str
    .replace(/\\/g, '\\\\')
    .replace(/`/g, '\\`')
    .replace(/\$\{/g, '\\${');
}

function generateBundledSkillsFile(skills: SkillMetadata[]): string {
  const imports = skills
    .map((skill) => {
      const varName = toVariableName(skill.dirName);
      return `import ${varName} from '${skill.importPath}';`;
    })
    .join('\n');

  const entries = skills
    .map((skill) => {
      const varName = toVariableName(skill.dirName);
      const additionalFiles = collectAdditionalFiles(skill.skillDirPath);
      const hasAdditionalFiles = Object.keys(additionalFiles).length > 0;

      let additionalFilesCode = '';
      if (hasAdditionalFiles) {
        const fileEntries = Object.entries(additionalFiles)
          .map(([path, content]) => {
            const escapedContent = escapeStringForTemplate(content);
            return `      '${path}': \`${escapedContent}\`,`;
          })
          .join('\n');
        additionalFilesCode = `
    additionalFiles: {
${fileEntries}
    },`;
      }

      return `  {
    name: '${skill.name}',
    dirName: '${skill.dirName}',
    content: ${varName},${additionalFilesCode}
  },`;
    })
    .join('\n');

  return `/**
 * AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 * Generated by: scripts/generate-bundled-skills.ts
 *
 * This file is automatically generated from:
 *   - Editor/Api/McpTools/<ToolFolder>/SKILL.md
 *   - Editor/Api/McpTools/<ToolFolder>/examples/*.md (additional files)
 *   - skill-definitions/cli-only/<SkillFolder>/SKILL.md
 *
 * To add a new skill, create a SKILL.md file in the appropriate location.
 * To exclude a skill from bundling, add \`internal: true\` to its frontmatter.
 */

${imports}

export interface BundledSkill {
  name: string;
  dirName: string;
  content: string;
  additionalFiles?: Record<string, string>;
}

export const BUNDLED_SKILLS: BundledSkill[] = [
${entries}
];

export function getBundledSkillByName(name: string): BundledSkill | undefined {
  return BUNDLED_SKILLS.find((skill) => skill.name === name);
}
`;
}

function main(): void {
  const mcpToolsSkills = collectSkillsFromMcpTools();
  const cliOnlySkills = collectSkillsFromCliOnly();

  const allSkills: SkillMetadata[] = [];
  const internalSkills: string[] = [];

  for (const skill of [...mcpToolsSkills, ...cliOnlySkills]) {
    if (skill.isInternal) {
      internalSkills.push(skill.name);
      continue;
    }
    allSkills.push(skill);
  }

  allSkills.sort((a, b) => a.name.localeCompare(b.name));

  const output = generateBundledSkillsFile(allSkills);
  writeFileSync(OUTPUT_FILE, output, 'utf-8');

  console.log(`Generated ${OUTPUT_FILE}`);
  console.log(`  - From McpTools: ${mcpToolsSkills.length} skills found`);
  console.log(`  - From cli-only: ${cliOnlySkills.length} skills found`);
  console.log(`  - Included: ${allSkills.length} skills`);
  if (internalSkills.length > 0) {
    console.log(`  - Excluded (internal): ${internalSkills.join(', ')}`);
  }
}

main();
