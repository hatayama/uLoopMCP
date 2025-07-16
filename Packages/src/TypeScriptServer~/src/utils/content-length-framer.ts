/**
 * Content-Length framing utility for JSON-RPC 2.0 communication.
 * Handles HTTP-style headers with format: "Content-Length: <n>\r\n\r\n<json_content>"
 */

import { errorToFile } from './log-to-file.js';

export interface FrameParseResult {
  contentLength: number;
  headerLength: number;
  isComplete: boolean;
}

export interface FrameExtractionResult {
  jsonContent: string | null;
  remainingData: string;
}

/**
 * Utility class for creating and parsing Content-Length framed messages.
 */
export class ContentLengthFramer {
  private static readonly CONTENT_LENGTH_HEADER = 'Content-Length:';
  private static readonly HEADER_SEPARATOR = '\r\n\r\n';
  private static readonly LINE_SEPARATOR = '\r\n';
  private static readonly MAX_MESSAGE_SIZE = 1024 * 1024; // 1MB

  /**
   * Creates a Content-Length framed message from JSON content.
   * @param jsonContent The JSON content to frame
   * @returns The framed message with Content-Length header
   */
  static createFrame(jsonContent: string): string {
    if (!jsonContent) {
      throw new Error('JSON content cannot be empty');
    }

    const contentLength = Buffer.byteLength(jsonContent, 'utf8');

    if (contentLength > this.MAX_MESSAGE_SIZE) {
      throw new Error(
        `Message size ${contentLength} exceeds maximum allowed size ${this.MAX_MESSAGE_SIZE}`,
      );
    }

    return `${this.CONTENT_LENGTH_HEADER} ${contentLength}${this.HEADER_SEPARATOR}${jsonContent}`;
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
      const separatorIndex = data.indexOf(this.HEADER_SEPARATOR);
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
      const headerLength = separatorIndex + this.HEADER_SEPARATOR.length;

      // Parse Content-Length from header
      const contentLength = this.parseContentLength(headerSection);
      if (contentLength === -1) {
        return {
          contentLength: -1,
          headerLength: -1,
          isComplete: false,
        };
      }

      // Check if the complete frame is available
      const expectedTotalLength = headerLength + contentLength;
      const isComplete = data.length >= expectedTotalLength;

      return {
        contentLength,
        headerLength,
        isComplete,
      };
    } catch (error) {
      errorToFile('[ContentLengthFramer] Error parsing frame:', error);
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

      if (data.length < expectedTotalLength) {
        // Frame is not complete yet
        return {
          jsonContent: null,
          remainingData: data,
        };
      }

      // Extract JSON content
      const jsonContent = data.substring(headerLength, headerLength + contentLength);

      // Extract remaining data after this frame
      const remainingData = data.substring(expectedTotalLength);

      return {
        jsonContent,
        remainingData,
      };
    } catch (error) {
      errorToFile('[ContentLengthFramer] Error extracting frame:', error);
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
    return contentLength >= 0 && contentLength <= this.MAX_MESSAGE_SIZE;
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

    // Split header into lines
    const lines = headerSection.split(this.LINE_SEPARATOR);

    for (const line of lines) {
      const trimmedLine = line.trim();

      // Check if this line contains Content-Length header (case-insensitive)
      if (trimmedLine.toLowerCase().startsWith(this.CONTENT_LENGTH_HEADER.toLowerCase())) {
        // Extract the value after the colon
        const colonIndex = trimmedLine.indexOf(':');
        if (colonIndex === -1 || colonIndex >= trimmedLine.length - 1) {
          continue;
        }

        const valueString = trimmedLine.substring(colonIndex + 1).trim();

        // Try to parse the integer value
        const parsedValue = parseInt(valueString, 10);
        if (isNaN(parsedValue)) {
          errorToFile(`[ContentLengthFramer] Invalid Content-Length value: '${valueString}'`);
          return -1;
        }

        if (!this.isValidContentLength(parsedValue)) {
          errorToFile(
            `[ContentLengthFramer] Content-Length value ${parsedValue} exceeds maximum allowed size ${this.MAX_MESSAGE_SIZE}`,
          );
          return -1;
        }

        return parsedValue;
      }
    }

    errorToFile(`[ContentLengthFramer] Content-Length header not found in: ${headerSection}`);
    return -1;
  }
}
