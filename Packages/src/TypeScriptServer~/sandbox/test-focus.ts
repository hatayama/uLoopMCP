#!/usr/bin/env node
/**
 * Test script for Unity window focus functionality
 * Usage:
 *   npx tsx sandbox/test-focus.ts                    # Focus any Unity
 *   npx tsx sandbox/test-focus.ts /path/to/project   # Focus specific project's Unity
 */

import {
  focusAnyUnityWindow,
  focusUnityWindowByProjectPath,
  listUnityProcesses,
} from '../src/utils/unity-window-focus.js';

async function main(): Promise<void> {
  const targetProject = process.argv[2];

  console.log('Searching for Unity processes...');

  const processes = await listUnityProcesses();

  if (processes.length === 0) {
    console.log('No Unity processes found.');
    return;
  }

  console.log(`Found ${processes.length} Unity process(es):`);
  for (const proc of processes) {
    console.log(`  PID: ${proc.pid}, Project: ${proc.projectPath}`);
  }

  if (targetProject) {
    console.log(`\nAttempting to focus Unity for project: ${targetProject}`);
    const result = await focusUnityWindowByProjectPath(targetProject);
    if (result.success) {
      console.log(`Success: ${result.message}`);
    } else {
      console.log(`Failed: ${result.message}`);
    }
  } else {
    console.log('\nAttempting to focus first Unity window...');
    const result = await focusAnyUnityWindow();
    if (result.success) {
      console.log(`Success: ${result.message}`);
    } else {
      console.log(`Failed: ${result.message}`);
    }
  }
}

main().catch(console.error);
