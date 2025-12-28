/**
 * Unit tests for skills-manager.ts
 *
 * Tests pure functions that don't require Unity connection.
 */

import { parseFrontmatter } from '../skills/skills-manager.js';

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
