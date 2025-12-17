/**
 * Simple Content-Length framer for CLI usage.
 * Minimal implementation without external dependencies.
 */

const CONTENT_LENGTH_HEADER = 'Content-Length:';
const HEADER_SEPARATOR = '\r\n\r\n';

export function createFrame(jsonContent: string): string {
  const contentLength = Buffer.byteLength(jsonContent, 'utf8');
  return `${CONTENT_LENGTH_HEADER} ${contentLength}${HEADER_SEPARATOR}${jsonContent}`;
}

export interface FrameParseResult {
  contentLength: number;
  headerLength: number;
  isComplete: boolean;
}

export function parseFrameFromBuffer(data: Buffer): FrameParseResult {
  if (!data || data.length === 0) {
    return { contentLength: -1, headerLength: -1, isComplete: false };
  }

  const separatorBuffer = Buffer.from(HEADER_SEPARATOR, 'utf8');
  const separatorIndex = data.indexOf(separatorBuffer);

  if (separatorIndex === -1) {
    return { contentLength: -1, headerLength: -1, isComplete: false };
  }

  const headerSection = data.subarray(0, separatorIndex).toString('utf8');
  const headerLength = separatorIndex + separatorBuffer.length;

  const contentLength = parseContentLength(headerSection);
  if (contentLength === -1) {
    return { contentLength: -1, headerLength: -1, isComplete: false };
  }

  const expectedTotalLength = headerLength + contentLength;
  const isComplete = data.length >= expectedTotalLength;

  return { contentLength, headerLength, isComplete };
}

export interface FrameExtractionResult {
  jsonContent: string | null;
  remainingData: Buffer;
}

export function extractFrameFromBuffer(
  data: Buffer,
  contentLength: number,
  headerLength: number,
): FrameExtractionResult {
  if (!data || data.length === 0 || contentLength < 0 || headerLength < 0) {
    return { jsonContent: null, remainingData: data || Buffer.alloc(0) };
  }

  const expectedTotalLength = headerLength + contentLength;

  if (data.length < expectedTotalLength) {
    return { jsonContent: null, remainingData: data };
  }

  const jsonContent = data.subarray(headerLength, headerLength + contentLength).toString('utf8');
  const remainingData = data.subarray(expectedTotalLength);

  return { jsonContent, remainingData };
}

function parseContentLength(headerSection: string): number {
  const lines = headerSection.split(/\r?\n/);

  for (const line of lines) {
    const trimmedLine = line.trim();
    const lowerLine = trimmedLine.toLowerCase();

    if (lowerLine.startsWith('content-length:')) {
      const colonIndex = trimmedLine.indexOf(':');
      if (colonIndex === -1 || colonIndex >= trimmedLine.length - 1) {
        continue;
      }

      const valueString = trimmedLine.substring(colonIndex + 1).trim();
      const parsedValue = parseInt(valueString, 10);

      if (isNaN(parsedValue) || parsedValue < 0) {
        return -1;
      }

      return parsedValue;
    }
  }

  return -1;
}
