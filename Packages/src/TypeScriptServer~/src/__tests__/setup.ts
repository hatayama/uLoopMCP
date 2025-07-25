/**
 * Jest Setup Configuration
 *
 * Global test setup for mocking problematic modules that use ESM features
 * not supported in Jest testing environment.
 */

// Mock VibeLogger to avoid import.meta issues in tests
jest.mock('../utils/vibe-logger.js', () => ({
  VibeLogger: {
    logInfo: jest.fn(),
    logError: jest.fn(),
    logWarning: jest.fn(),
    logDebug: jest.fn(),
    generateCorrelationId: jest.fn().mockReturnValue('test-correlation-id'),
  },
}));

// Mock dynamic imports and ES modules that cause issues in Jest
jest.mock('../tools/dynamic-unity-command-tool.js', () => ({
  DynamicUnityCommandTool: jest.fn().mockImplementation(() => ({
    execute: jest.fn().mockResolvedValue({
      content: [{ type: 'text', text: 'Mock tool execution result' }],
    }),
    name: 'mock-tool',
    description: 'Mock tool for testing',
  })),
}));

// Suppress console warnings during tests unless explicitly testing them
// eslint-disable-next-line no-console
const originalConsoleWarn = console.warn;
// eslint-disable-next-line no-console
const originalConsoleError = console.error;

beforeEach(() => {
  // Reset all mocks before each test
  jest.clearAllMocks();

  // Suppress console noise during tests (can be overridden in individual tests)
  // eslint-disable-next-line no-console
  console.warn = jest.fn();
  // eslint-disable-next-line no-console
  console.error = jest.fn();
});

afterEach(() => {
  // Restore console methods after each test
  // eslint-disable-next-line no-console
  console.warn = originalConsoleWarn;
  // eslint-disable-next-line no-console
  console.error = originalConsoleError;
});
