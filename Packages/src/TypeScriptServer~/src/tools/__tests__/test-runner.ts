#!/usr/bin/env node

import { McpConnectionValidator } from './mcp-connection.test.js';

/**
 * Standalone test runner for MCP connection validation
 * Implements fail-fast approach with contract-based design
 */
function runMcpValidationTests(): void {
  // infoToFile('Starting MCP Connection Validation Suite');
  // infoToFile('Contract-based testing with fail-fast approach');
  // infoToFile('='.repeat(50));

  const validator = new McpConnectionValidator();

  try {
    // Run all validations - will throw on first failure
    validator.runAllValidations();

    // infoToFile('='.repeat(50));
    // infoToFile('All MCP connection tests PASSED');
    // infoToFile('Server is ready for Cursor integration');

    process.exit(0);
  } catch (error) {
    // infoToFile('='.repeat(50));
    // errorToFile('MCP connection tests FAILED');
    console.error('Fail-fast triggered:', error instanceof Error ? error.message : String(error));
    // infoToFile('Fix the issue before proceeding');

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
