/**
 * Bundled skill definitions for uloop CLI.
 * These skills are embedded at build time via esbuild --loader:.md=text
 */

import compileSkill from './skill-definitions/uloop-compile/SKILL.md';
import getLogsSkill from './skill-definitions/uloop-get-logs/SKILL.md';
import runTestsSkill from './skill-definitions/uloop-run-tests/SKILL.md';
import clearConsoleSkill from './skill-definitions/uloop-clear-console/SKILL.md';
import focusWindowSkill from './skill-definitions/uloop-focus-window/SKILL.md';
import getHierarchySkill from './skill-definitions/uloop-get-hierarchy/SKILL.md';
import unitySearchSkill from './skill-definitions/uloop-unity-search/SKILL.md';
import getMenuItemsSkill from './skill-definitions/uloop-get-menu-items/SKILL.md';
import executeMenuItemSkill from './skill-definitions/uloop-execute-menu-item/SKILL.md';
import findGameObjectsSkill from './skill-definitions/uloop-find-game-objects/SKILL.md';
import captureGameviewSkill from './skill-definitions/uloop-capture-gameview/SKILL.md';
import executeDynamicCodeSkill from './skill-definitions/uloop-execute-dynamic-code/SKILL.md';
import getProviderDetailsSkill from './skill-definitions/uloop-get-provider-details/SKILL.md';

export interface BundledSkill {
  name: string;
  dirName: string;
  content: string;
}

export const BUNDLED_SKILLS: BundledSkill[] = [
  { name: 'uloop-compile', dirName: 'uloop-compile', content: compileSkill },
  { name: 'uloop-get-logs', dirName: 'uloop-get-logs', content: getLogsSkill },
  { name: 'uloop-run-tests', dirName: 'uloop-run-tests', content: runTestsSkill },
  { name: 'uloop-clear-console', dirName: 'uloop-clear-console', content: clearConsoleSkill },
  { name: 'uloop-focus-window', dirName: 'uloop-focus-window', content: focusWindowSkill },
  { name: 'uloop-get-hierarchy', dirName: 'uloop-get-hierarchy', content: getHierarchySkill },
  { name: 'uloop-unity-search', dirName: 'uloop-unity-search', content: unitySearchSkill },
  { name: 'uloop-get-menu-items', dirName: 'uloop-get-menu-items', content: getMenuItemsSkill },
  {
    name: 'uloop-execute-menu-item',
    dirName: 'uloop-execute-menu-item',
    content: executeMenuItemSkill,
  },
  {
    name: 'uloop-find-game-objects',
    dirName: 'uloop-find-game-objects',
    content: findGameObjectsSkill,
  },
  {
    name: 'uloop-capture-gameview',
    dirName: 'uloop-capture-gameview',
    content: captureGameviewSkill,
  },
  {
    name: 'uloop-execute-dynamic-code',
    dirName: 'uloop-execute-dynamic-code',
    content: executeDynamicCodeSkill,
  },
  {
    name: 'uloop-get-provider-details',
    dirName: 'uloop-get-provider-details',
    content: getProviderDetailsSkill,
  },
];

export function getBundledSkillByName(name: string): BundledSkill | undefined {
  return BUNDLED_SKILLS.find((skill) => skill.name === name);
}
