#!/usr/bin/env node

import { McpConnectionValidator } from './mcp-connection.test.js';

/**
 * Standalone test runner for MCP connection validation
 * Implements fail-fast approach with contract-based design
 */
function runMcpValidationTests(): void {
  const validator = new McpConnectionValidator();

  try {
    // Run all validations - will throw on first failure
    validator.runAllValidations();

    process.exit(0);
  } catch (error) {
    console.error('Fail-fast triggered:', error instanceof Error ? error.message : String(error));

    process.exit(1);
  }
}

// Run if called directly
if (import.meta.url === `file://${process.argv[1]}`) {
  try {
    runMcpValidationTests();
  } catch (error) {
    console.error('Fatal error:', error instanceof Error ? error.message : String(error));
    process.exit(1);
  }
}
