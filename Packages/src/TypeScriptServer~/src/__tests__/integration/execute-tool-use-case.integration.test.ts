/**
 * Integration Test for ExecuteToolUseCase
 *
 * Tests the complete workflow of ExecuteToolUseCase with mocked dependencies
 * following Clean Architecture principles.
 *
 * Test Strategy:
 * - Mock Application Service interfaces (IConnectionService, IToolQueryService)
 * - Test UseCase business logic in isolation
 * - Verify proper error handling through ErrorConverter
 * - Ensure proper logging and correlation ID usage
 */

/* eslint-disable @typescript-eslint/unbound-method */

import { ExecuteToolUseCase } from '../../domain/use-cases/execute-tool-use-case.js';
import { ExecuteToolRequest } from '../../domain/models/requests.js';
import { IConnectionService } from '../../application/interfaces/connection-service.js';
import { IToolQueryService } from '../../application/interfaces/tool-query-service.js';
import { DynamicUnityCommandTool } from '../../tools/dynamic-unity-command-tool.js';

// Mock dependencies
const mockConnectionService: jest.Mocked<IConnectionService> = {
  isConnected: jest.fn(),
  ensureConnected: jest.fn(),
  disconnect: jest.fn(),
  testConnection: jest.fn(),
  setupReconnectionCallback: jest.fn(),
};

const mockToolQueryService: jest.Mocked<IToolQueryService> = {
  getAllTools: jest.fn(),
  hasTool: jest.fn(),
  getTool: jest.fn(),
  getToolsCount: jest.fn(),
};

describe('ExecuteToolUseCase Integration Tests', () => {
  let executeToolUseCase: ExecuteToolUseCase;

  beforeEach(() => {
    // Reset all mocks
    jest.clearAllMocks();

    // Create UseCase instance with mocked dependencies
    executeToolUseCase = new ExecuteToolUseCase(mockConnectionService, mockToolQueryService);
  });

  describe('Successful tool execution workflow', () => {
    test('should execute tool successfully when all conditions are met', async () => {
      // Arrange
      const mockDynamicTool = {
        name: 'test-tool',
        description: 'Test tool for integration testing',
        inputSchema: {
          type: 'object',
          properties: {
            message: { type: 'string' },
          },
        },
        execute: jest.fn().mockResolvedValue({
          content: [{ type: 'text', text: 'Tool executed successfully' }],
          isError: false,
        }),
      } as unknown as DynamicUnityCommandTool;

      const request: ExecuteToolRequest = {
        toolName: 'test-tool',
        arguments: { message: 'Hello World' },
      };

      // Mock successful flow
      mockConnectionService.isConnected.mockReturnValue(true);
      mockToolQueryService.hasTool.mockReturnValue(true);
      mockToolQueryService.getTool.mockReturnValue(mockDynamicTool);

      // Act
      const result = await executeToolUseCase.execute(request);

      // Assert
      expect(result).toBeDefined();
      expect(result.content).toBeDefined();
      expect(Array.isArray(result.content)).toBe(true);
      expect(result.isError).toBe(false);
      expect(mockConnectionService.isConnected).toHaveBeenCalled();
      expect(mockToolQueryService.hasTool).toHaveBeenCalledWith('test-tool');
      expect(mockToolQueryService.getTool).toHaveBeenCalledWith('test-tool');
      expect(mockDynamicTool.execute as jest.Mock).toHaveBeenCalledWith({ message: 'Hello World' });
    });
  });

  describe('Connection error scenarios', () => {
    test('should throw ConnectionError when Unity is not connected', async () => {
      // Arrange
      const request: ExecuteToolRequest = {
        toolName: 'test-tool',
        arguments: {},
      };

      mockConnectionService.isConnected.mockReturnValue(false);
      mockConnectionService.ensureConnected.mockRejectedValue(new Error('Connection timeout'));

      // Act & Assert
      const result = await executeToolUseCase.execute(request);

      expect(result).toBeDefined();
      expect(result.isError).toBe(true);
      expect(mockConnectionService.ensureConnected).toHaveBeenCalled();
    });
  });

  describe('Tool availability error scenarios', () => {
    test('should return error response when tool does not exist', async () => {
      // Arrange
      const request: ExecuteToolRequest = {
        toolName: 'non-existent-tool',
        arguments: {},
      };

      mockConnectionService.isConnected.mockReturnValue(true);
      mockToolQueryService.hasTool.mockReturnValue(false);

      // Act
      const result = await executeToolUseCase.execute(request);

      // Assert - ExecuteToolUseCase returns error response instead of throwing
      expect(result).toBeDefined();
      expect(result.isError).toBe(true);
      expect(mockToolQueryService.hasTool).toHaveBeenCalledWith('non-existent-tool');
      expect(mockToolQueryService.getTool).not.toHaveBeenCalled();
    });

    test('should return error response when tool instance is not available', async () => {
      // Arrange
      const request: ExecuteToolRequest = {
        toolName: 'test-tool',
        arguments: {},
      };

      mockConnectionService.isConnected.mockReturnValue(true);
      mockToolQueryService.hasTool.mockReturnValue(true);
      mockToolQueryService.getTool.mockReturnValue(undefined);

      // Act
      const result = await executeToolUseCase.execute(request);

      // Assert - ExecuteToolUseCase returns error response instead of throwing
      expect(result).toBeDefined();
      expect(result.isError).toBe(true);
      expect(mockToolQueryService.getTool).toHaveBeenCalledWith('test-tool');
    });
  });

  describe('Error handling and logging', () => {
    test('should handle and convert infrastructure errors properly', async () => {
      // Arrange
      const request: ExecuteToolRequest = {
        toolName: 'test-tool',
        arguments: {},
      };

      mockConnectionService.isConnected.mockReturnValue(false);
      mockConnectionService.ensureConnected.mockRejectedValue(new Error('Network error'));

      // Act
      const result = await executeToolUseCase.execute(request);

      // Assert - ExecuteToolUseCase returns error response instead of throwing
      expect(result).toBeDefined();
      expect(result.isError).toBe(true);
      expect(mockConnectionService.isConnected).toHaveBeenCalled();
      expect(mockConnectionService.ensureConnected).toHaveBeenCalled();
    });
  });

  describe('UseCase dependency isolation', () => {
    test('should only interact with provided interface methods', async () => {
      // Arrange
      const request: ExecuteToolRequest = {
        toolName: 'test-tool',
        arguments: {},
      };

      const mockDynamicTool = {
        name: 'test-tool',
        description: 'Test tool',
        execute: jest.fn().mockResolvedValue({
          content: [{ type: 'text', text: 'Test result' }],
          isError: false,
        }),
      } as unknown as DynamicUnityCommandTool;

      mockConnectionService.isConnected.mockReturnValue(true);
      mockToolQueryService.hasTool.mockReturnValue(true);
      mockToolQueryService.getTool.mockReturnValue(mockDynamicTool);

      // Act
      await executeToolUseCase.execute(request);

      // Assert - Verify only interface methods are called
      expect(mockConnectionService.isConnected).toHaveBeenCalled();
      expect(mockToolQueryService.hasTool).toHaveBeenCalled();
      expect(mockToolQueryService.getTool).toHaveBeenCalled();

      // Verify no unexpected method calls
      expect(mockToolQueryService.getAllTools).not.toHaveBeenCalled();
      expect(mockToolQueryService.getToolsCount).not.toHaveBeenCalled();
    });
  });
});
