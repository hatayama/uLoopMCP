/**
 * Unit tests for skills-manager.ts
 *
 * Tests pure functions that don't require Unity connection.
 */

import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'fs';
import { homedir, tmpdir } from 'os';
import { join } from 'path';

import {
  getInstallDir,
  getManagedSkillsDir,
  migrateLegacyManagedSkills,
  parseFrontmatter,
  removeDeprecatedSkillDirs,
  syncInstalledSkillDirectory,
} from '../skills/skills-manager.js';
import { getTargetConfig } from '../skills/target-config.js';

describe('parseFrontmatter', () => {
  it('should parse basic frontmatter with string values', () => {
    const content = `---
name: uloop-test-skill
description: A test skill for testing
---

# Test Skill

Some content here.
`;

    const result = parseFrontmatter(content);

    expect(result.name).toBe('uloop-test-skill');
    expect(result.description).toBe('A test skill for testing');
  });

  it('should parse boolean true value', () => {
    const content = `---
name: internal-skill
internal: true
---

# Internal Skill
`;

    const result = parseFrontmatter(content);

    expect(result.name).toBe('internal-skill');
    expect(result.internal).toBe(true);
  });

  it('should parse boolean false value', () => {
    const content = `---
name: public-skill
internal: false
---

# Public Skill
`;

    const result = parseFrontmatter(content);

    expect(result.name).toBe('public-skill');
    expect(result.internal).toBe(false);
  });

  it('should return empty object for content without frontmatter', () => {
    const content = `# No Frontmatter

Just some markdown content.
`;

    const result = parseFrontmatter(content);

    expect(result).toEqual({});
  });

  it('should return empty object for empty content', () => {
    const result = parseFrontmatter('');

    expect(result).toEqual({});
  });

  it('should handle frontmatter with colons in value', () => {
    const content = `---
name: uloop-test
description: Use when: (1) first case, (2) second case
---

# Test
`;

    const result = parseFrontmatter(content);

    expect(result.name).toBe('uloop-test');
    expect(result.description).toBe('Use when: (1) first case, (2) second case');
  });

  it('should handle frontmatter with empty values', () => {
    const content = `---
name: uloop-test
description:
---

# Test
`;

    const result = parseFrontmatter(content);

    expect(result.name).toBe('uloop-test');
    expect(result.description).toBe('');
  });

  it('should skip lines without colons', () => {
    const content = `---
name: uloop-test
this line has no colon
description: valid description
---

# Test
`;

    const result = parseFrontmatter(content);

    expect(result.name).toBe('uloop-test');
    expect(result.description).toBe('valid description');
    expect(Object.keys(result)).toHaveLength(2);
  });

  it('should trim whitespace from keys and values', () => {
    const content = `---
  name  :   uloop-test
  description  :   spaced description
---

# Test
`;

    const result = parseFrontmatter(content);

    expect(result.name).toBe('uloop-test');
    expect(result.description).toBe('spaced description');
  });

  it('should handle incomplete frontmatter (missing closing)', () => {
    const content = `---
name: uloop-test
description: incomplete

# No closing delimiter
`;

    const result = parseFrontmatter(content);

    expect(result).toEqual({});
  });

  it('should handle frontmatter-only content (no body)', () => {
    const content = `---
name: minimal-skill
---`;

    const result = parseFrontmatter(content);

    expect(result.name).toBe('minimal-skill');
  });
});

describe('skill install layout', () => {
  const temporaryRoots: string[] = [];

  afterEach(() => {
    while (temporaryRoots.length > 0) {
      const temporaryRoot = temporaryRoots.pop();
      if (temporaryRoot) {
        rmSync(temporaryRoot, { recursive: true, force: true });
      }
    }
  });

  function createSkillsRoot(): string {
    const temporaryRoot = mkdtempSync(join(tmpdir(), 'uloop-skills-manager-'));
    temporaryRoots.push(temporaryRoot);

    const skillsRoot = join(temporaryRoot, 'skills');
    mkdirSync(skillsRoot, { recursive: true });
    return skillsRoot;
  }

  function writeSkill(skillDir: string, content: string = '---\nname: test-skill\n---\n'): void {
    mkdirSync(skillDir, { recursive: true });
    writeFileSync(join(skillDir, 'SKILL.md'), content, 'utf-8');
  }

  it('should resolve managed skills under the unity-cli-loop namespace', () => {
    expect(getManagedSkillsDir('/tmp/example/skills')).toBe(
      join('/tmp/example/skills', 'unity-cli-loop'),
    );
  });

  it('should resolve the flat install directory when grouping is disabled', () => {
    expect(getInstallDir(getTargetConfig('claude'), true, false)).toBe(
      join(homedir(), '.claude', 'skills'),
    );
  });

  it('should migrate only managed legacy skills into the unity-cli-loop namespace', () => {
    const skillsRoot = createSkillsRoot();

    writeSkill(join(skillsRoot, 'uloop-compile'));
    writeSkill(join(skillsRoot, 'acme-third-party'));
    writeSkill(join(skillsRoot, 'find-orphaned-meta'));

    const moved = migrateLegacyManagedSkills(skillsRoot, ['uloop-compile', 'acme-third-party']);
    const managedSkillsRoot = getManagedSkillsDir(skillsRoot);

    expect(moved).toBe(2);
    expect(existsSync(join(managedSkillsRoot, 'uloop-compile', 'SKILL.md'))).toBe(true);
    expect(existsSync(join(managedSkillsRoot, 'acme-third-party', 'SKILL.md'))).toBe(true);
    expect(existsSync(join(skillsRoot, 'uloop-compile'))).toBe(false);
    expect(existsSync(join(skillsRoot, 'acme-third-party'))).toBe(false);
    expect(existsSync(join(skillsRoot, 'find-orphaned-meta', 'SKILL.md'))).toBe(true);
  });

  it('should not overwrite an existing managed skill during migration', () => {
    const skillsRoot = createSkillsRoot();
    const managedSkillsRoot = getManagedSkillsDir(skillsRoot);

    writeSkill(join(skillsRoot, 'uloop-compile'), 'legacy');
    writeSkill(join(managedSkillsRoot, 'uloop-compile'), 'managed');

    const moved = migrateLegacyManagedSkills(skillsRoot, ['uloop-compile']);

    expect(moved).toBe(0);
    expect(readFileSync(join(managedSkillsRoot, 'uloop-compile', 'SKILL.md'), 'utf-8')).toBe(
      'managed',
    );
    expect(readFileSync(join(skillsRoot, 'uloop-compile', 'SKILL.md'), 'utf-8')).toBe('legacy');
  });

  it('should remove deprecated skills from both legacy and managed locations', () => {
    const skillsRoot = createSkillsRoot();
    const managedSkillsRoot = getManagedSkillsDir(skillsRoot);

    writeSkill(join(skillsRoot, 'uloop-unity-search'));
    writeSkill(join(managedSkillsRoot, 'uloop-unity-search'));

    const removed = removeDeprecatedSkillDirs(skillsRoot);

    expect(removed).toBe(2);
    expect(existsSync(join(skillsRoot, 'uloop-unity-search'))).toBe(false);
    expect(existsSync(join(managedSkillsRoot, 'uloop-unity-search'))).toBe(false);
  });

  it('should remove stale files when syncing an installed skill directory', () => {
    const skillsRoot = createSkillsRoot();
    const skillDir = join(skillsRoot, 'unity-cli-loop', 'uloop-execute-dynamic-code');

    mkdirSync(join(skillDir, 'references'), { recursive: true });
    writeFileSync(join(skillDir, 'SKILL.md'), 'stale', 'utf-8');
    writeFileSync(join(skillDir, 'references', 'stale.md'), 'stale', 'utf-8');

    syncInstalledSkillDirectory(skillDir, 'SKILL.md', 'fresh', {
      'references/reference.md': Buffer.from('reference', 'utf-8'),
    });

    expect(readFileSync(join(skillDir, 'SKILL.md'), 'utf-8')).toBe('fresh');
    expect(readFileSync(join(skillDir, 'references', 'reference.md'), 'utf-8')).toBe('reference');
    expect(existsSync(join(skillDir, 'references', 'stale.md'))).toBe(false);
  });
});
