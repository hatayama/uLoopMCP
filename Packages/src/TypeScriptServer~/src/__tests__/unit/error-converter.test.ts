/**
 * Unit Test for ErrorConverter
 *
 * Tests the error conversion logic from Infrastructure errors to Domain errors
 * following Clean Architecture principles.
 *
 * Test Strategy:
 * - Test conversion of each Infrastructure error type to appropriate Domain error
 * - Verify technical details are properly logged before conversion
 * - Test generic Error and unknown error handling
 * - Verify error recoverability assessment
 */

/* eslint-disable @typescript-eslint/unbound-method */

import { ErrorConverter } from '../../application/error-converter.js';
import {
  DomainError,
  ConnectionError,
  ToolExecutionError,
  ValidationError,
  DiscoveryError,
  ClientCompatibilityError,
} from '../../domain/errors.js';
import {
  UnityCommunicationError,
  ToolManagementError,
  ServiceResolutionError,
  NetworkError,
  McpProtocolError,
} from '../../infrastructure/errors.js';

// Mock VibeLogger to avoid actual logging during tests
jest.mock('../../utils/vibe-logger.js', () => ({
  VibeLogger: {
    logError: jest.fn(),
    generateCorrelationId: jest.fn().mockReturnValue('test-correlation-id'),
  },
}));

import { VibeLogger } from '../../utils/vibe-logger.js';
const mockVibeLogger = VibeLogger as jest.Mocked<typeof VibeLogger>;

describe('ErrorConverter Unit Tests', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Infrastructure Error Conversion', () => {
    test('should convert UnityCommunicationError to ConnectionError', () => {
      // Arrange
      const infraError = new UnityCommunicationError('Unity connection failed', 'localhost:8700', {
        timeout: 5000,
      });

      // Act
      const result = ErrorConverter.convertToDomainError(
        infraError,
        'test_operation',
        'test-correlation-id',
      );

      // Assert
      expect(result).toBeInstanceOf(ConnectionError);
      expect(result.message).toContain('Unity communication failed');
      expect(result.details).toHaveProperty('original_category', 'UNITY_COMMUNICATION');
      expect(result.details).toHaveProperty('unity_endpoint', 'localhost:8700');
      expect(mockVibeLogger.logError).toHaveBeenCalledWith(
        'test_operation_infrastructure_error',
        'Infrastructure error during test_operation',
        expect.any(Object),
        'test-correlation-id',
        expect.any(String),
      );
    });

    test('should convert ToolManagementError to ToolExecutionError', () => {
      // Arrange
      const infraError = new ToolManagementError('Tool creation failed', 'test-tool', {
        schema: 'invalid',
      });

      // Act
      const result = ErrorConverter.convertToDomainError(
        infraError,
        'tool_management',
        'tool-correlation-id',
      );

      // Assert
      expect(result).toBeInstanceOf(ToolExecutionError);
      expect(result.message).toContain('Tool management failed');
      expect(result.details).toHaveProperty('original_category', 'TOOL_MANAGEMENT');
      expect(result.details).toHaveProperty('tool_name', 'test-tool');
    });

    test('should convert ServiceResolutionError to ValidationError', () => {
      // Arrange
      const infraError = new ServiceResolutionError('Service not found', 'TEST_SERVICE_TOKEN', [
        'TokenA',
        'TokenB',
      ]);

      // Act
      const result = ErrorConverter.convertToDomainError(infraError, 'service_resolution');

      // Assert
      expect(result).toBeInstanceOf(ValidationError);
      expect(result.message).toContain('Service resolution failed');
      expect(result.details).toHaveProperty('original_category', 'SERVICE_RESOLUTION');
      expect(result.details).toHaveProperty('service_token', 'TEST_SERVICE_TOKEN');
    });

    test('should convert NetworkError to DiscoveryError', () => {
      // Arrange
      const infraError = new NetworkError('Port binding failed', 'localhost', 8700);

      // Act
      const result = ErrorConverter.convertToDomainError(infraError, 'network_operation');

      // Assert
      expect(result).toBeInstanceOf(DiscoveryError);
      expect(result.message).toContain('Network operation failed');
      expect(result.details).toHaveProperty('original_category', 'NETWORK');
      expect(result.details).toHaveProperty('endpoint', 'localhost');
      expect(result.details).toHaveProperty('port', 8700);
    });

    test('should convert McpProtocolError to ClientCompatibilityError', () => {
      // Arrange
      const infraError = new McpProtocolError('Protocol version mismatch', '2024-11-05', {
        clientVersion: '2024-10-01',
      });

      // Act
      const result = ErrorConverter.convertToDomainError(infraError, 'protocol_validation');

      // Assert
      expect(result).toBeInstanceOf(ClientCompatibilityError);
      expect(result.message).toContain('MCP protocol error');
      expect(result.details).toHaveProperty('original_category', 'MCP_PROTOCOL');
      expect(result.details).toHaveProperty('protocol_version', '2024-11-05');
    });
  });

  describe('Generic Error Conversion', () => {
    test('should convert generic Error to appropriate Domain error based on message', () => {
      // Test connection-related error
      const connectionError = new Error('Connection timeout occurred');
      const result1 = ErrorConverter.convertToDomainError(connectionError, 'test_op');
      expect(result1).toBeInstanceOf(ConnectionError);

      // Test tool-related error
      const toolError = new Error('Tool execution failed');
      const result2 = ErrorConverter.convertToDomainError(toolError, 'test_op');
      expect(result2).toBeInstanceOf(ToolExecutionError);

      // Test validation error
      const validationError = new Error('Invalid parameter provided');
      const result3 = ErrorConverter.convertToDomainError(validationError, 'test_op');
      expect(result3).toBeInstanceOf(ValidationError);

      // Test discovery error
      const discoveryError = new Error('Network discovery failed');
      const result4 = ErrorConverter.convertToDomainError(discoveryError, 'test_op');
      expect(result4).toBeInstanceOf(DiscoveryError);
    });

    test('should default to ToolExecutionError for unrecognized generic errors', () => {
      // Arrange
      const genericError = new Error('Some unexpected error occurred');

      // Act
      const result = ErrorConverter.convertToDomainError(genericError, 'unknown_operation');

      // Assert
      expect(result).toBeInstanceOf(ToolExecutionError);
      expect(result.message).toContain('Unexpected error');
      expect(mockVibeLogger.logError).toHaveBeenCalled();
    });
  });

  describe('Unknown Error Handling', () => {
    test('should handle string errors', () => {
      // Act
      const result = ErrorConverter.convertToDomainError(
        'String error message',
        'string_error_test',
      );

      // Assert
      expect(result).toBeInstanceOf(ToolExecutionError);
      expect(result.message).toContain('Unknown error occurred: String error message');
    });

    test('should handle object errors', () => {
      // Arrange
      const objectError = { code: 'UNKNOWN', message: 'Object error' };

      // Act
      const result = ErrorConverter.convertToDomainError(objectError, 'object_error_test');

      // Assert
      expect(result).toBeInstanceOf(ToolExecutionError);
      expect(result.message).toContain('Unknown error occurred');
      expect(mockVibeLogger.logError).toHaveBeenCalledWith(
        'object_error_test_unknown_error',
        expect.any(String),
        expect.objectContaining({
          error_value: objectError,
          error_type: 'object',
        }),
        undefined,
        expect.any(String),
      );
    });

    test('should handle null and undefined errors', () => {
      const nullResult = ErrorConverter.convertToDomainError(null, 'null_test');
      expect(nullResult).toBeInstanceOf(ToolExecutionError);

      const undefinedResult = ErrorConverter.convertToDomainError(undefined, 'undefined_test');
      expect(undefinedResult).toBeInstanceOf(ToolExecutionError);
    });
  });

  describe('Domain Error Pass-through', () => {
    test('should return existing Domain errors unchanged', () => {
      // Arrange
      const existingDomainError = new ConnectionError('Existing connection error');

      // Act
      const result = ErrorConverter.convertToDomainError(existingDomainError, 'passthrough_test');

      // Assert
      expect(result).toBe(existingDomainError);
      expect(result.message).toBe('Existing connection error');
      expect(mockVibeLogger.logError).not.toHaveBeenCalled();
    });
  });

  describe('Error Recoverability Assessment', () => {
    test('should correctly identify recoverable errors', () => {
      const connectionError = new ConnectionError('Connection lost');
      expect(ErrorConverter.isRecoverable(connectionError)).toBe(true);

      const discoveryError = new DiscoveryError('Unity not found');
      expect(ErrorConverter.isRecoverable(discoveryError)).toBe(true);

      const toolError = new ToolExecutionError('Tool failed');
      expect(ErrorConverter.isRecoverable(toolError)).toBe(true);
    });

    test('should correctly identify non-recoverable errors', () => {
      const validationError = new ValidationError('Invalid input');
      expect(ErrorConverter.isRecoverable(validationError)).toBe(false);

      const compatibilityError = new ClientCompatibilityError('Unsupported client');
      expect(ErrorConverter.isRecoverable(compatibilityError)).toBe(false);
    });

    test('should default to non-recoverable for unknown error codes', () => {
      // Create a custom error with unknown code
      class CustomDomainError extends DomainError {
        readonly code = 'CUSTOM_UNKNOWN_ERROR';
      }

      const customError = new CustomDomainError('Custom error');
      expect(ErrorConverter.isRecoverable(customError)).toBe(false);
    });
  });

  describe('Logging Integration', () => {
    test('should log technical details for Infrastructure errors', () => {
      // Arrange
      const infraError = new UnityCommunicationError(
        'Test error',
        'test-endpoint',
        { key: 'value' },
        new Error('Original cause'),
      );

      // Act
      ErrorConverter.convertToDomainError(infraError, 'logging_test', 'log-correlation');

      // Assert
      expect(mockVibeLogger.logError).toHaveBeenCalledWith(
        'logging_test_infrastructure_error',
        'Infrastructure error during logging_test',
        expect.objectContaining({
          message: 'Test error',
          category: 'UNITY_COMMUNICATION',
          technicalDetails: expect.any(Object) as unknown,
          originalError: 'Original cause',
          stack: expect.any(String) as unknown,
        }),
        'log-correlation',
        'Error Converter logging technical details before domain conversion',
      );
    });
  });
});
