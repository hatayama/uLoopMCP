import { ContentLengthFramer, FrameExtractionResult } from './content-length-framer.js';
import { errorToFile, warnToFile } from './log-to-file.js';

/**
 * Dynamic buffer for handling Content-Length framed messages.
 * Manages buffer growth and frame extraction for TCP communication.
 */
export class DynamicBuffer {
  private buffer: string = '';
  private readonly maxBufferSize: number;
  private readonly initialBufferSize: number;

  constructor(maxBufferSize: number = 1024 * 1024, initialBufferSize: number = 4096) {
    this.maxBufferSize = maxBufferSize;
    this.initialBufferSize = initialBufferSize;
  }

  /**
   * Appends new data to the buffer.
   * @param data The data to append
   * @throws Error if buffer would exceed maximum size
   */
  append(data: string): void {
    if (!data) {
      return;
    }

    const newSize = this.buffer.length + data.length;
    if (newSize > this.maxBufferSize) {
      throw new Error(
        `Buffer size would exceed maximum allowed size: ${newSize} > ${this.maxBufferSize}`,
      );
    }

    this.buffer += data;
  }

  /**
   * Attempts to extract a complete frame from the buffer.
   * @returns The extracted frame and whether extraction was successful
   */
  extractFrame(): { frame: string | null; extracted: boolean } {
    if (!this.buffer) {
      return { frame: null, extracted: false };
    }

    try {
      // Parse the frame header
      const parseResult = ContentLengthFramer.parseFrame(this.buffer);

      if (!parseResult.isComplete) {
        // Frame is not complete yet
        return { frame: null, extracted: false };
      }

      // Extract the complete frame
      const extractionResult: FrameExtractionResult = ContentLengthFramer.extractFrame(
        this.buffer,
        parseResult.contentLength,
        parseResult.headerLength,
      );

      if (!extractionResult.jsonContent) {
        return { frame: null, extracted: false };
      }

      // Update buffer with remaining data
      this.buffer = extractionResult.remainingData;

      return {
        frame: extractionResult.jsonContent,
        extracted: true,
      };
    } catch (error) {
      errorToFile('[DynamicBuffer] Error extracting frame:', error);
      return { frame: null, extracted: false };
    }
  }

  /**
   * Extracts all available complete frames from the buffer.
   * @returns Array of extracted JSON frames
   */
  extractAllFrames(): string[] {
    const frames: string[] = [];

    // eslint-disable-next-line no-constant-condition
    while (true) {
      const result = this.extractFrame();
      if (!result.extracted || !result.frame) {
        break;
      }
      frames.push(result.frame);
    }

    return frames;
  }

  /**
   * Checks if the buffer has any data.
   * @returns True if buffer contains data, false otherwise
   */
  hasData(): boolean {
    return this.buffer.length > 0;
  }

  /**
   * Gets the current buffer size.
   * @returns The current buffer size in characters
   */
  getSize(): number {
    return this.buffer.length;
  }

  /**
   * Checks if a complete frame might be available.
   * This is a quick check without full parsing.
   * @returns True if header separator is found, false otherwise
   */
  hasCompleteFrameHeader(): boolean {
    return this.buffer.includes('\r\n\r\n');
  }

  /**
   * Clears the buffer.
   */
  clear(): void {
    this.buffer = '';
  }

  /**
   * Gets a preview of the buffer content for debugging.
   * @param maxLength Maximum length of preview (default: 100)
   * @returns Truncated buffer content for debugging
   */
  getPreview(maxLength: number = 100): string {
    if (this.buffer.length <= maxLength) {
      return this.buffer;
    }
    return this.buffer.substring(0, maxLength) + '...';
  }

  /**
   * Validates the buffer state and performs cleanup if necessary.
   * @returns True if buffer is in valid state, false if cleanup was performed
   */
  validateAndCleanup(): boolean {
    // Check for extremely large buffer without complete frames
    if (this.buffer.length > this.maxBufferSize * 0.8 && !this.hasCompleteFrameHeader()) {
      warnToFile('[DynamicBuffer] Large buffer without complete frame header, clearing buffer');
      this.clear();
      return false;
    }

    // Check for malformed data (no header separator after reasonable amount of data)
    const headerSeparatorIndex = this.buffer.indexOf('\r\n\r\n');
    if (headerSeparatorIndex === -1 && this.buffer.length > 1024) {
      // Look for potential malformed headers
      const contentLengthIndex = this.buffer.toLowerCase().indexOf('content-length:');
      if (contentLengthIndex === -1) {
        warnToFile(
          '[DynamicBuffer] No Content-Length header found in large buffer, clearing buffer',
        );
        this.clear();
        return false;
      }
    }

    return true;
  }

  /**
   * Gets buffer statistics for monitoring and debugging.
   * @returns Buffer statistics object
   */
  getStats(): {
    size: number;
    maxSize: number;
    utilization: number;
    hasCompleteHeader: boolean;
    preview: string;
  } {
    return {
      size: this.buffer.length,
      maxSize: this.maxBufferSize,
      utilization: (this.buffer.length / this.maxBufferSize) * 100,
      hasCompleteHeader: this.hasCompleteFrameHeader(),
      preview: this.getPreview(50),
    };
  }
}
