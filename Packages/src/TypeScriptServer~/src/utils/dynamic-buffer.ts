import { ContentLengthFramer, FrameExtractionResult } from './content-length-framer.js';

/**
 * Dynamic buffer for handling Content-Length framed messages with proper UTF-8 support.
 * Manages buffer growth and frame extraction for TCP communication using Buffer internally.
 */
export class DynamicBuffer {
  // 定数定義
  private static readonly ENCODING_UTF8 = 'utf8';
  private static readonly HEADER_SEPARATOR = '\r\n\r\n';
  private static readonly CONTENT_LENGTH_HEADER = 'content-length:';
  private static readonly LOG_PREFIX = '[DynamicBuffer]';
  private static readonly PREVIEW_SUFFIX = '...';
  private static readonly PREVIEW_LENGTH_FRAME_ERROR = 200;
  private static readonly LARGE_BUFFER_THRESHOLD = 1024;
  private static readonly BUFFER_UTILIZATION_THRESHOLD = 0.8;
  private static readonly DEFAULT_PREVIEW_LENGTH = 100;
  private static readonly STATS_PREVIEW_LENGTH = 50;
  private buffer: Buffer = Buffer.alloc(0);
  private readonly maxBufferSize: number;
  private readonly initialBufferSize: number;

  constructor(maxBufferSize: number = 1024 * 1024, initialBufferSize: number = 4096) {
    this.maxBufferSize = maxBufferSize;
    this.initialBufferSize = initialBufferSize;
  }

  /**
   * Appends new data to the buffer.
   * @param data The data to append (Buffer or string)
   * @throws Error if buffer would exceed maximum size
   */
  append(data: Buffer | string): void {
    if (!data) {
      return;
    }

    // Convert string to Buffer if needed, preserving UTF-8 encoding
    const dataBuffer = Buffer.isBuffer(data)
      ? data
      : Buffer.from(data, DynamicBuffer.ENCODING_UTF8);

    const newSize = this.buffer.length + dataBuffer.length;
    if (newSize > this.maxBufferSize) {
      throw new Error(
        `Buffer size would exceed maximum allowed size: ${newSize} > ${this.maxBufferSize}`,
      );
    }

    this.buffer = Buffer.concat([this.buffer, dataBuffer]);
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
      // Parse the frame header using Buffer-based parsing
      const parseResult = ContentLengthFramer.parseFrameFromBuffer(this.buffer);

      if (!parseResult.isComplete) {
        // Frame is not complete yet
        return { frame: null, extracted: false };
      }

      // Extract the complete frame using Buffer-based extraction
      const extractionResult: FrameExtractionResult = ContentLengthFramer.extractFrameFromBuffer(
        this.buffer,
        parseResult.contentLength,
        parseResult.headerLength,
      );

      if (!extractionResult.jsonContent) {
        return { frame: null, extracted: false };
      }

      // Update buffer with remaining data (convert to Buffer if needed)
      this.buffer = Buffer.isBuffer(extractionResult.remainingData)
        ? extractionResult.remainingData
        : Buffer.from(extractionResult.remainingData, DynamicBuffer.ENCODING_UTF8);

      return {
        frame: extractionResult.jsonContent,
        extracted: true,
      };
    } catch {
      // Error extracting frame from buffer
      return { frame: null, extracted: false };
    }
  }

  /**
   * Extracts all available complete frames from the buffer.
   * @returns Array of extracted JSON frames
   */
  extractAllFrames(): string[] {
    const frames: string[] = [];

    while (this.buffer.length > 0) {
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
    return this.buffer.includes(
      Buffer.from(DynamicBuffer.HEADER_SEPARATOR, DynamicBuffer.ENCODING_UTF8),
    );
  }

  /**
   * Clears the buffer.
   */
  clear(): void {
    this.buffer = Buffer.alloc(0);
  }

  /**
   * Gets a preview of the buffer content for debugging.
   * @param maxLength Maximum length of preview (default: 100)
   * @returns Truncated buffer content for debugging
   */
  getPreview(maxLength: number = DynamicBuffer.DEFAULT_PREVIEW_LENGTH): string {
    if (this.buffer.length <= maxLength) {
      return this.buffer.toString(DynamicBuffer.ENCODING_UTF8);
    }
    return (
      this.buffer.subarray(0, maxLength).toString(DynamicBuffer.ENCODING_UTF8) +
      DynamicBuffer.PREVIEW_SUFFIX
    );
  }

  /**
   * Validates the buffer state and performs cleanup if necessary.
   * @returns True if buffer is in valid state, false if cleanup was performed
   */
  validateAndCleanup(): boolean {
    // Check for extremely large buffer without complete frames
    if (
      this.buffer.length > this.maxBufferSize * DynamicBuffer.BUFFER_UTILIZATION_THRESHOLD &&
      !this.hasCompleteFrameHeader()
    ) {
      // Large buffer without complete frame header, clearing buffer
      this.clear();
      return false;
    }

    // Check for malformed data (no header separator after reasonable amount of data)
    const headerSeparatorBuffer = Buffer.from(
      DynamicBuffer.HEADER_SEPARATOR,
      DynamicBuffer.ENCODING_UTF8,
    );
    const headerSeparatorIndex = this.buffer.indexOf(headerSeparatorBuffer);
    if (headerSeparatorIndex === -1 && this.buffer.length > DynamicBuffer.LARGE_BUFFER_THRESHOLD) {
      // Look for potential malformed headers
      const contentLengthBuffer = Buffer.from(
        DynamicBuffer.CONTENT_LENGTH_HEADER,
        DynamicBuffer.ENCODING_UTF8,
      );
      const contentLengthIndex = this.buffer.indexOf(contentLengthBuffer);
      if (contentLengthIndex === -1) {
        // No Content-Length header found in large buffer, clearing buffer
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
      preview: this.getPreview(DynamicBuffer.STATS_PREVIEW_LENGTH),
    };
  }
}
