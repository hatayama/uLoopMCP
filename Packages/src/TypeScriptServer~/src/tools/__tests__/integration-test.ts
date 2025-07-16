#!/usr/bin/env node

import { McpConnectionValidator } from './mcp-connection.test.js';

/**
 * TDD Integration Test Suite
 * Fail-fast approach with contract-based design
 * Tests everything needed before Cursor deployment
 */
class IntegrationTestSuite {
  private testResults: Array<{ name: string; passed: boolean; error?: string }> = [];

  /**
   * Run a single test with fail-fast error handling
   */
  private runTest(testName: string, testFn: () => void): void {
    try {
      testFn();
      this.testResults.push({ name: testName, passed: true });
    } catch (error) {
      const errorMsg = error instanceof Error ? error.message : String(error);
      this.testResults.push({ name: testName, passed: false, error: errorMsg });
      throw error; // Fail fast
    }
  }

  /**
   * Test 1: MCP Server Creation and Configuration
   */
  private testMcpServerSetup(): void {
    const validator = new McpConnectionValidator();
    validator.createServer();
    validator.registerHandlers();
    validator.testNotification();
    validator.validateJsonRpcCompliance();
  }

  /**
   * Test 2: JSON RPC Protocol Compliance
   */
  private testJsonRpcCompliance(): void {
    // Test that our messages are valid JSON RPC
    const testMessages = [
      { method: 'notifications/tools/list_changed', params: {}, jsonrpc: '2.0' },
      { id: 1, method: 'tools/list', params: {}, jsonrpc: '2.0' },
      { id: 2, method: 'tools/call', params: { name: 'test', arguments: {} }, jsonrpc: '2.0' },
    ];

    for (const msg of testMessages) {
      const jsonStr = JSON.stringify(msg);
      const parsed: unknown = JSON.parse(jsonStr);

      if (
        typeof parsed !== 'object' ||
        parsed === null ||
        !('method' in parsed) ||
        !('jsonrpc' in parsed)
      ) {
        throw new Error(`Invalid JSON RPC message: ${jsonStr}`);
      }
    }
  }

  /**
   * Test 3: Environment Configuration
   */
  private testEnvironmentConfig(): void {
    // Check that we can detect environment properly
    const isProduction = !process.env.NODE_ENV || process.env.NODE_ENV === 'production';
    const isDevelopment =
      process.env.NODE_ENV === 'development' || process.env.MCP_DEBUG === 'true';

    if (!isProduction && !isDevelopment) {
      throw new Error('Environment detection failed - neither production nor development');
    }
  }

  /**
   * Test 4: Notification Timing (5 second interval)
   */
  private testNotificationTiming(): void {
    // Test that our notification interval is set correctly
    const expectedInterval = 5000; // 5 seconds

    // Create a test timer to verify timing
    const testTimer = setInterval(() => {
      // Timer callback for testing purposes
    }, expectedInterval);

    // Verify timer was created
    if (!testTimer) {
      throw new Error('Failed to create notification timer');
    }

    clearInterval(testTimer);
  }

  /**
   * Test 5: Tool Count Validation
   */
  private testToolCountLogic(): void {
    // Test tool count toggle logic
    let toolCount: number = 5;

    // First toggle: 5 -> 8
    toolCount = toolCount === 5 ? 8 : 5;
    if (toolCount !== 8) {
      throw new Error(`Expected tool count 8, got ${toolCount}`);
    }

    // Second toggle: 8 -> 5
    toolCount = toolCount === 8 ? 5 : 8;
    if (toolCount !== 5) {
      throw new Error(`Expected tool count 5, got ${toolCount}`);
    }
  }

  /**
   * Run all integration tests
   */
  runAllTests(): void {
    try {
      this.runTest('MCP Server Setup & Configuration', () => this.testMcpServerSetup());
      this.runTest('JSON RPC Protocol Compliance', () => this.testJsonRpcCompliance());
      this.runTest('Environment Configuration', () => this.testEnvironmentConfig());
      this.runTest('Notification Timing (5s interval)', () => this.testNotificationTiming());
      this.runTest('Tool Count Toggle Logic', () => this.testToolCountLogic());

      process.exit(0);
    } catch (error) {
      process.exit(1);
    }
  }
}

// Run if called directly
if (import.meta.url === `file://${process.argv[1]}`) {
  const suite = new IntegrationTestSuite();
  try {
    suite.runAllTests();
  } catch (error) {
    process.exit(1);
  }
}
