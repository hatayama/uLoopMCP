/**
 * Content-Length framing utility for JSON-RPC 2.0 communication.
 * Handles HTTP-style headers with format: "Content-Length: <n>\r\n\r\n<json_content>"
 */

import { VibeLogger } from './vibe-logger.js';

export interface FrameParseResult {
  contentLength: number;
  headerLength: number;
  isComplete: boolean;
}

export interface FrameExtractionResult {
  jsonContent: string | null;
  remainingData: string | Buffer;
}

/**
 * Utility class for creating and parsing Content-Length framed messages.
 */
export class ContentLengthFramer {
  private static readonly CONTENT_LENGTH_HEADER = 'Content-Length:';
  private static readonly HEADER_SEPARATOR = '\r\n\r\n';
  private static readonly LINE_SEPARATOR = '\r\n';
  private static readonly MAX_MESSAGE_SIZE = 1024 * 1024; // 1MB
  private static readonly ENCODING_UTF8 = 'utf8';
  private static readonly LOG_PREFIX = '[ContentLengthFramer]';
  private static readonly ERROR_MESSAGE_JSON_EMPTY = 'JSON content cannot be empty';
  private static readonly DEBUG_PREVIEW_LENGTH = 50;
  private static readonly PREVIEW_SUFFIX = '...';

  /**
   * Creates a Content-Length framed message from JSON content.
   * @param jsonContent The JSON content to frame
   * @returns The framed message with Content-Length header
   */
  static createFrame(jsonContent: string): string {
    if (!jsonContent) {
      throw new Error(ContentLengthFramer.ERROR_MESSAGE_JSON_EMPTY);
    }

    const contentLength = Buffer.byteLength(jsonContent, ContentLengthFramer.ENCODING_UTF8);

    if (contentLength > ContentLengthFramer.MAX_MESSAGE_SIZE) {
      throw new Error(
        `Message size ${contentLength} exceeds maximum allowed size ${ContentLengthFramer.MAX_MESSAGE_SIZE}`,
      );
    }

    return `${ContentLengthFramer.CONTENT_LENGTH_HEADER} ${contentLength}${ContentLengthFramer.HEADER_SEPARATOR}${jsonContent}`;
  }

  /**
   * Parses a Content-Length header from incoming data.
   * @param data The incoming data string
   * @returns Parse result with content length, header length, and completion status
   */
  static parseFrame(data: string): FrameParseResult {
    if (!data) {
      return {
        contentLength: -1,
        headerLength: -1,
        isComplete: false,
      };
    }

    try {
      // Find the header separator
      const separatorIndex = data.indexOf(ContentLengthFramer.HEADER_SEPARATOR);
      if (separatorIndex === -1) {
        // Header not complete yet
        return {
          contentLength: -1,
          headerLength: -1,
          isComplete: false,
        };
      }

      // Extract header section
      const headerSection = data.substring(0, separatorIndex);
      const headerLength = separatorIndex + ContentLengthFramer.HEADER_SEPARATOR.length;

      // Parse Content-Length from header
      const contentLength = ContentLengthFramer.parseContentLength(headerSection);
      if (contentLength === -1) {
        return {
          contentLength: -1,
          headerLength: -1,
          isComplete: false,
        };
      }

      // Check if the complete frame is available using byte length
      const expectedTotalLength = headerLength + contentLength;
      const actualByteLength = Buffer.byteLength(data, ContentLengthFramer.ENCODING_UTF8);
      const isComplete = actualByteLength >= expectedTotalLength;

      VibeLogger.logDebug(
        'frame_analysis',
        `Frame analysis: dataLength=${data.length}, actualByteLength=${actualByteLength}, contentLength=${contentLength}, headerLength=${headerLength}, expectedTotal=${expectedTotalLength}, isComplete=${isComplete}`,
        {
          dataLength: data.length,
          actualByteLength,
          contentLength,
          headerLength,
          expectedTotalLength,
          isComplete,
        },
      );

      return {
        contentLength,
        headerLength,
        isComplete,
      };
    } catch (error) {
      VibeLogger.logError(
        'parse_frame_error',
        `Error parsing frame: ${error instanceof Error ? error.message : String(error)}`,
        {
          error:
            error instanceof Error
              ? {
                  name: error.name,
                  message: error.message,
                  stack: error.stack,
                }
              : { raw: String(error) },
        },
      );
      return {
        contentLength: -1,
        headerLength: -1,
        isComplete: false,
      };
    }
  }

  /**
   * Parses a Content-Length header from incoming Buffer data.
   * @param data The incoming data Buffer
   * @returns Parse result with content length, header length, and completion status
   */
  static parseFrameFromBuffer(data: Buffer): FrameParseResult {
    if (!data || data.length === 0) {
      return {
        contentLength: -1,
        headerLength: -1,
        isComplete: false,
      };
    }

    try {
      // Find the header separator in Buffer
      const separatorBuffer = Buffer.from(
        ContentLengthFramer.HEADER_SEPARATOR,
        ContentLengthFramer.ENCODING_UTF8,
      );
      const separatorIndex = data.indexOf(separatorBuffer);
      if (separatorIndex === -1) {
        // Header not complete yet
        return {
          contentLength: -1,
          headerLength: -1,
          isComplete: false,
        };
      }

      // Extract header section
      const headerSection = data
        .subarray(0, separatorIndex)
        .toString(ContentLengthFramer.ENCODING_UTF8);
      const headerLength = separatorIndex + separatorBuffer.length;

      // Parse Content-Length from header
      const contentLength = ContentLengthFramer.parseContentLength(headerSection);
      if (contentLength === -1) {
        return {
          contentLength: -1,
          headerLength: -1,
          isComplete: false,
        };
      }

      // Check if the complete frame is available using byte length
      const expectedTotalLength = headerLength + contentLength;
      const actualByteLength = data.length;
      const isComplete = actualByteLength >= expectedTotalLength;

      VibeLogger.logDebug(
        'frame_analysis_buffer',
        `Frame analysis: dataLength=${data.length}, actualByteLength=${actualByteLength}, contentLength=${contentLength}, headerLength=${headerLength}, expectedTotal=${expectedTotalLength}, isComplete=${isComplete}`,
        {
          dataLength: data.length,
          actualByteLength,
          contentLength,
          headerLength,
          expectedTotalLength,
          isComplete,
        },
      );

      return {
        contentLength,
        headerLength,
        isComplete,
      };
    } catch (error) {
      VibeLogger.logError(
        'parse_frame_buffer_error',
        `Error parsing frame: ${error instanceof Error ? error.message : String(error)}`,
        {
          error:
            error instanceof Error
              ? {
                  name: error.name,
                  message: error.message,
                  stack: error.stack,
                }
              : { raw: String(error) },
        },
      );
      return {
        contentLength: -1,
        headerLength: -1,
        isComplete: false,
      };
    }
  }

  /**
   * Extracts a complete frame from the data buffer.
   * @param data The data buffer containing the frame
   * @param contentLength The expected content length
   * @param headerLength The header length including separators
   * @returns The extracted JSON content and remaining data
   */
  static extractFrame(
    data: string,
    contentLength: number,
    headerLength: number,
  ): FrameExtractionResult {
    if (!data || contentLength < 0 || headerLength < 0) {
      return {
        jsonContent: null,
        remainingData: data || '',
      };
    }

    try {
      const expectedTotalLength = headerLength + contentLength;
      const actualByteLength = Buffer.byteLength(data, ContentLengthFramer.ENCODING_UTF8);

      if (actualByteLength < expectedTotalLength) {
        // Frame is not complete yet
        return {
          jsonContent: null,
          remainingData: data,
        };
      }

      // Extract JSON content by byte length
      const dataBuffer = Buffer.from(data, ContentLengthFramer.ENCODING_UTF8);
      const jsonContent = dataBuffer
        .subarray(headerLength, headerLength + contentLength)
        .toString(ContentLengthFramer.ENCODING_UTF8);

      // Extract remaining data after this frame
      const remainingData = dataBuffer
        .subarray(expectedTotalLength)
        .toString(ContentLengthFramer.ENCODING_UTF8);

      return {
        jsonContent,
        remainingData,
      };
    } catch (error) {
      VibeLogger.logError(
        'extract_frame_error',
        `Error extracting frame: ${error instanceof Error ? error.message : String(error)}`,
        {
          error:
            error instanceof Error
              ? {
                  name: error.name,
                  message: error.message,
                  stack: error.stack,
                }
              : { raw: String(error) },
        },
      );
      return {
        jsonContent: null,
        remainingData: data,
      };
    }
  }

  /**
   * Extracts a complete frame from the Buffer data.
   * @param data The Buffer containing the frame
   * @param contentLength The expected content length
   * @param headerLength The header length including separators
   * @returns The extracted JSON content and remaining data
   */
  static extractFrameFromBuffer(
    data: Buffer,
    contentLength: number,
    headerLength: number,
  ): FrameExtractionResult {
    if (!data || data.length === 0 || contentLength < 0 || headerLength < 0) {
      return {
        jsonContent: null,
        remainingData: data || Buffer.alloc(0),
      };
    }

    try {
      const expectedTotalLength = headerLength + contentLength;

      if (data.length < expectedTotalLength) {
        // Frame is not complete yet
        return {
          jsonContent: null,
          remainingData: data,
        };
      }

      // Extract JSON content
      const jsonContent = data
        .subarray(headerLength, headerLength + contentLength)
        .toString(ContentLengthFramer.ENCODING_UTF8);

      // Extract remaining data after this frame
      const remainingData = data.subarray(expectedTotalLength);

      return {
        jsonContent,
        remainingData,
      };
    } catch (error) {
      VibeLogger.logError(
        'extract_frame_buffer_error',
        `Error extracting frame: ${error instanceof Error ? error.message : String(error)}`,
        {
          error:
            error instanceof Error
              ? {
                  name: error.name,
                  message: error.message,
                  stack: error.stack,
                }
              : { raw: String(error) },
        },
      );
      return {
        jsonContent: null,
        remainingData: data,
      };
    }
  }

  /**
   * Validates that the Content-Length value is within acceptable limits.
   * @param contentLength The content length to validate
   * @returns True if the content length is valid, false otherwise
   */
  static isValidContentLength(contentLength: number): boolean {
    return contentLength >= 0 && contentLength <= ContentLengthFramer.MAX_MESSAGE_SIZE;
  }

  /**
   * Parses the Content-Length value from the header section.
   * @param headerSection The header section string
   * @returns The parsed content length, or -1 if parsing failed
   */
  private static parseContentLength(headerSection: string): number {
    if (!headerSection) {
      return -1;
    }

    // Handle both standard line separators and potential fragmentation
    const lines = headerSection.split(/\r?\n/);

    for (const line of lines) {
      const trimmedLine = line.trim();

      // Check for Content-Length header (case-insensitive)
      // Use more precise matching to avoid false positives
      const lowerLine = trimmedLine.toLowerCase();
      if (lowerLine.startsWith('content-length:')) {
        // Extract the value after the colon
        const colonIndex = trimmedLine.indexOf(':');
        if (colonIndex === -1 || colonIndex >= trimmedLine.length - 1) {
          continue;
        }

        const valueString = trimmedLine.substring(colonIndex + 1).trim();

        // Try to parse the integer value
        const parsedValue = parseInt(valueString, 10);
        if (isNaN(parsedValue)) {
          // Invalid Content-Length value
          return -1;
        }

        if (!ContentLengthFramer.isValidContentLength(parsedValue)) {
          // Content-Length value exceeds maximum allowed size
          return -1;
        }

        return parsedValue;
      }
    }

    // Content-Length header not found in header section
    return -1;
  }
}
