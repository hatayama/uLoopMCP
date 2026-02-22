/**
 * Target configuration for multi-AI tool support.
 * Supports Claude Code and Codex CLI, with extensibility for future targets.
 */

export type TargetId = 'claude' | 'codex' | 'cursor' | 'gemini';

export interface TargetConfig {
  id: TargetId;
  displayName: string;
  projectDir: string;
  skillFileName: string;
}

export const TARGET_CONFIGS: Record<TargetId, TargetConfig> = {
  claude: {
    id: 'claude',
    displayName: 'Claude Code',
    projectDir: '.claude',
    skillFileName: 'SKILL.md',
  },
  codex: {
    id: 'codex',
    displayName: 'Codex CLI',
    projectDir: '.codex',
    skillFileName: 'SKILL.md',
  },
  cursor: {
    id: 'cursor',
    displayName: 'Cursor',
    projectDir: '.cursor',
    skillFileName: 'SKILL.md',
  },
  gemini: {
    id: 'gemini',
    displayName: 'Gemini CLI',
    projectDir: '.gemini',
    skillFileName: 'SKILL.md',
  },
};

export const ALL_TARGET_IDS: TargetId[] = ['claude', 'codex', 'cursor', 'gemini'];

export function getTargetConfig(id: TargetId): TargetConfig {
  // eslint-disable-next-line security/detect-object-injection -- id is type-constrained to TargetId union type
  return TARGET_CONFIGS[id];
}
