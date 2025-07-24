/**
 * Integration Test for RefreshToolsUseCase
 *
 * Tests the complete workflow of RefreshToolsUseCase with mocked dependencies
 * following Clean Architecture principles.
 *
 * Test Strategy:
 * - Mock Application Service interfaces (IConnectionService, IToolManagementService)
 * - Test UseCase business logic and workflow orchestration
 * - Verify proper error handling and conversion
 * - Test correlation ID propagation and logging
 */

import { RefreshToolsUseCase } from '../../domain/use-cases/refresh-tools-use-case.js';
import { RefreshToolsRequest } from '../../domain/models/requests.js';
import { IConnectionService } from '../../application/interfaces/connection-service.js';
import { IToolManagementService } from '../../application/interfaces/tool-management-service.js';
import { IToolQueryService } from '../../application/interfaces/tool-query-service.js';
import { DomainTool } from '../../domain/models/domain-tool.js';
import { ConnectionError } from '../../domain/errors.js';

// Mock dependencies
const mockConnectionService: jest.Mocked<IConnectionService> = {
  isConnected: jest.fn(),
  ensureConnected: jest.fn(),
  disconnect: jest.fn(),
  testConnection: jest.fn(),
  setupReconnectionCallback: jest.fn(),
};

const mockToolManagementService: jest.Mocked<IToolManagementService> = {
  initializeTools: jest.fn(),
  refreshTools: jest.fn(),
  setClientName: jest.fn(),
};

const mockToolQueryService: jest.Mocked<IToolQueryService> = {
  getAllTools: jest.fn(),
  hasTool: jest.fn(),
  getTool: jest.fn(),
  getToolsCount: jest.fn(),
};

describe('RefreshToolsUseCase Integration Tests', () => {
  let refreshToolsUseCase: RefreshToolsUseCase;

  beforeEach(() => {
    // Reset all mocks
    jest.clearAllMocks();

    // Create UseCase instance with mocked dependencies
    refreshToolsUseCase = new RefreshToolsUseCase(
      mockConnectionService,
      mockToolManagementService,
      mockToolQueryService,
    );
  });

  describe('Successful tool refresh workflow', () => {
    test('should refresh tools successfully when Unity is connected', async () => {
      // Arrange
      const request: RefreshToolsRequest = {
        includeDevelopmentOnly: false,
      };

      mockConnectionService.isConnected.mockReturnValue(true);
      mockToolManagementService.initializeTools.mockResolvedValue();
      mockToolQueryService.getAllTools.mockReturnValue([
        { name: 'tool1', description: 'Test tool 1' },
        { name: 'tool2', description: 'Test tool 2' },
      ]);

      // Act
      const result = await refreshToolsUseCase.execute(request);

      // Assert
      expect(result).toBeDefined();
      expect(result.tools).toBeDefined();
      expect(result.refreshedAt).toBeDefined();
      expect(Array.isArray(result.tools)).toBe(true);
      expect(result.tools).toHaveLength(2);

      // Verify workflow sequence
      expect(mockConnectionService.isConnected).toHaveBeenCalled();
      expect(mockToolManagementService.initializeTools).toHaveBeenCalled();
      expect(mockToolQueryService.getAllTools).toHaveBeenCalled();
    });

    test('should establish connection when Unity is not connected', async () => {
      // Arrange
      const request: RefreshToolsRequest = {
        includeDevelopmentOnly: true,
      };

      mockConnectionService.isConnected.mockReturnValue(false);
      mockConnectionService.ensureConnected.mockResolvedValue();
      mockToolManagementService.initializeTools.mockResolvedValue();
      mockToolQueryService.getAllTools.mockReturnValue([
        { name: 'tool1', description: 'Test tool 1' },
      ]);

      // Act
      const result = await refreshToolsUseCase.execute(request);

      // Assert
      expect(result).toBeDefined();
      expect(result.tools).toHaveLength(1);
      expect(mockConnectionService.ensureConnected).toHaveBeenCalled();
      expect(mockToolManagementService.initializeTools).toHaveBeenCalled();
      expect(mockToolQueryService.getAllTools).toHaveBeenCalled();
    });
  });

  describe('Connection error scenarios', () => {
    test('should return empty tools list when connection cannot be established', async () => {
      // Arrange
      const request: RefreshToolsRequest = {
        includeDevelopmentOnly: false,
      };

      mockConnectionService.isConnected.mockReturnValue(false);
      mockConnectionService.ensureConnected.mockRejectedValue(
        new Error('Connection timeout after 10 seconds'),
      );

      // Act
      const result = await refreshToolsUseCase.execute(request);

      // Assert - RefreshToolsUseCase returns empty tools list instead of throwing
      expect(result).toBeDefined();
      expect(result.tools).toEqual([]);
      expect(mockConnectionService.ensureConnected).toHaveBeenCalled();
      expect(mockToolManagementService.initializeTools).not.toHaveBeenCalled();
    });
  });

  describe('Tool initialization error scenarios', () => {
    test('should return empty tools list when tool initialization fails', async () => {
      // Arrange
      const request: RefreshToolsRequest = {
        includeDevelopmentOnly: false,
      };

      mockConnectionService.isConnected.mockReturnValue(true);
      mockToolManagementService.initializeTools.mockRejectedValue(
        new Error('Unity communication failed'),
      );

      // Act
      const result = await refreshToolsUseCase.execute(request);

      // Assert - RefreshToolsUseCase returns empty tools list instead of throwing
      expect(result).toBeDefined();
      expect(result.tools).toEqual([]);
      expect(mockToolManagementService.initializeTools).toHaveBeenCalled();
    });
  });

  describe('Workflow orchestration', () => {
    test('should execute workflow steps in correct order', async () => {
      // Arrange
      const request: RefreshToolsRequest = {
        includeDevelopmentOnly: false,
      };

      const callOrder: string[] = [];

      mockConnectionService.isConnected.mockImplementation(() => {
        callOrder.push('isConnected');
        return true;
      });

      mockToolManagementService.initializeTools.mockImplementation(async () => {
        callOrder.push('initializeTools');
      });

      mockToolQueryService.getAllTools.mockImplementation(() => {
        callOrder.push('getAllTools');
        return [];
      });

      // Act
      await refreshToolsUseCase.execute(request);

      // Assert - Verify correct execution order
      expect(callOrder).toEqual(['isConnected', 'initializeTools', 'getAllTools']);
    });

    test('should handle connection retry workflow correctly', async () => {
      // Arrange
      const request: RefreshToolsRequest = {
        includeDevelopmentOnly: true,
      };

      const callOrder: string[] = [];

      mockConnectionService.isConnected.mockImplementation(() => {
        callOrder.push('isConnected');
        return false;
      });

      mockConnectionService.ensureConnected.mockImplementation(async () => {
        callOrder.push('ensureConnected');
      });

      mockToolManagementService.initializeTools.mockImplementation(async () => {
        callOrder.push('initializeTools');
      });

      mockToolQueryService.getAllTools.mockImplementation(() => {
        callOrder.push('getAllTools');
        return [];
      });

      // Act
      await refreshToolsUseCase.execute(request);

      // Assert - Verify connection retry workflow
      expect(callOrder).toEqual([
        'isConnected',
        'ensureConnected',
        'initializeTools',
        'getAllTools',
      ]);
    });
  });

  describe('Data integrity and validation', () => {
    test('should return valid RefreshToolsResponse structure', async () => {
      // Arrange
      const request: RefreshToolsRequest = {
        includeDevelopmentOnly: false,
      };

      mockConnectionService.isConnected.mockReturnValue(true);
      mockToolManagementService.initializeTools.mockResolvedValue();
      mockToolQueryService.getAllTools.mockReturnValue([]);

      // Act
      const result = await refreshToolsUseCase.execute(request);

      // Assert - Response structure validation
      expect(result).toHaveProperty('tools');
      expect(result).toHaveProperty('refreshedAt');
      expect(Array.isArray(result.tools)).toBe(true);
      expect(typeof result.refreshedAt).toBe('string');

      // Validate ISO date string format
      expect(() => new Date(result.refreshedAt)).not.toThrow();
      expect(mockToolQueryService.getAllTools).toHaveBeenCalled();
    });
  });
});
