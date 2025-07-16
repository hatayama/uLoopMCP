import { DynamicBuffer } from './dynamic-buffer.js';

describe('DynamicBuffer', () => {
  let buffer: DynamicBuffer;

  beforeEach(() => {
    buffer = new DynamicBuffer();
  });

  describe('append', () => {
    it('should append data to buffer', () => {
      buffer.append('test data');
      expect(buffer.getSize()).toBe(9);
      expect(buffer.hasData()).toBe(true);
    });

    it('should handle empty data', () => {
      buffer.append('');
      expect(buffer.getSize()).toBe(0);
      expect(buffer.hasData()).toBe(false);
    });

    it('should throw error when exceeding max buffer size', () => {
      const smallBuffer = new DynamicBuffer(10);
      expect(() => smallBuffer.append('this is too long')).toThrow(
        'Buffer size would exceed maximum allowed size',
      );
    });

    it('should accumulate multiple appends', () => {
      buffer.append('Content-Length: 25');
      buffer.append('\r\n\r\n');
      buffer.append('{"jsonrpc":"2.0","id":1}');

      expect(buffer.getSize()).toBe(46);
      expect(buffer.hasCompleteFrameHeader()).toBe(true);
    });
  });

  describe('extractFrame', () => {
    it('should extract a complete frame', () => {
      const jsonContent = '{"jsonrpc":"2.0","id":1}';
      const frameData = `Content-Length: ${jsonContent.length}\r\n\r\n${jsonContent}`;

      buffer.append(frameData);
      const result = buffer.extractFrame();

      expect(result.extracted).toBe(true);
      expect(result.frame).toBe(jsonContent);
      expect(buffer.getSize()).toBe(0);
    });

    it('should return null for incomplete frame', () => {
      buffer.append('Content-Length: 25\r\n\r\n{"jsonrpc"');
      const result = buffer.extractFrame();

      expect(result.extracted).toBe(false);
      expect(result.frame).toBe(null);
      expect(buffer.getSize()).toBeGreaterThan(0);
    });

    it('should handle multiple frames', () => {
      const json1 = '{"id":1}';
      const json2 = '{"id":2}';
      const frame1 = `Content-Length: ${json1.length}\r\n\r\n${json1}`;
      const frame2 = `Content-Length: ${json2.length}\r\n\r\n${json2}`;

      buffer.append(frame1 + frame2);

      const result1 = buffer.extractFrame();
      expect(result1.extracted).toBe(true);
      expect(result1.frame).toBe(json1);

      const result2 = buffer.extractFrame();
      expect(result2.extracted).toBe(true);
      expect(result2.frame).toBe(json2);

      expect(buffer.getSize()).toBe(0);
    });

    it('should handle partial frame followed by complete frame', () => {
      // Add incomplete frame first
      buffer.append('Content-Length: 50\r\n\r\n{"incomplete');

      let result = buffer.extractFrame();
      expect(result.extracted).toBe(false);

      // Complete the frame
      buffer.clear();
      const jsonContent = '{"jsonrpc":"2.0","id":1}';
      const completeFrame = `Content-Length: ${jsonContent.length}\r\n\r\n${jsonContent}`;
      buffer.append(completeFrame);

      result = buffer.extractFrame();
      expect(result.extracted).toBe(true);
      expect(result.frame).toBe(jsonContent);
    });
  });

  describe('extractAllFrames', () => {
    it('should extract all available frames', () => {
      const json1 = '{"id":1}';
      const json2 = '{"id":2}';
      const json3 = '{"id":3}';
      const frames = [
        `Content-Length: ${json1.length}\r\n\r\n${json1}`,
        `Content-Length: ${json2.length}\r\n\r\n${json2}`,
        `Content-Length: ${json3.length}\r\n\r\n${json3}`,
      ].join('');

      buffer.append(frames);
      const results = buffer.extractAllFrames();

      expect(results).toHaveLength(3);
      expect(results[0]).toBe(json1);
      expect(results[1]).toBe(json2);
      expect(results[2]).toBe(json3);
      expect(buffer.getSize()).toBe(0);
    });

    it('should return empty array when no complete frames', () => {
      buffer.append('Content-Length: 25\r\n\r\n{"incomplete');
      const results = buffer.extractAllFrames();

      expect(results).toHaveLength(0);
      expect(buffer.getSize()).toBeGreaterThan(0);
    });

    it('should handle mix of complete and incomplete frames', () => {
      const json1 = '{"id":1}';
      const completeFrame = `Content-Length: ${json1.length}\r\n\r\n${json1}`;
      const incompleteFrame = 'Content-Length: 25\r\n\r\n{"incomplete';

      buffer.append(completeFrame + incompleteFrame);
      const results = buffer.extractAllFrames();

      expect(results).toHaveLength(1);
      expect(results[0]).toBe(json1);
      expect(buffer.getSize()).toBeGreaterThan(0); // Incomplete frame remains
    });
  });

  describe('hasCompleteFrameHeader', () => {
    it('should return true when header separator is present', () => {
      buffer.append('Content-Length: 25\r\n\r\n');
      expect(buffer.hasCompleteFrameHeader()).toBe(true);
    });

    it('should return false when header separator is missing', () => {
      buffer.append('Content-Length: 25\r\n');
      expect(buffer.hasCompleteFrameHeader()).toBe(false);
    });

    it('should return false for empty buffer', () => {
      expect(buffer.hasCompleteFrameHeader()).toBe(false);
    });
  });

  describe('clear', () => {
    it('should clear the buffer', () => {
      buffer.append('test data');
      expect(buffer.getSize()).toBeGreaterThan(0);

      buffer.clear();
      expect(buffer.getSize()).toBe(0);
      expect(buffer.hasData()).toBe(false);
    });
  });

  describe('getPreview', () => {
    it('should return full content when shorter than max length', () => {
      const data = 'short data';
      buffer.append(data);
      expect(buffer.getPreview(100)).toBe(data);
    });

    it('should truncate content when longer than max length', () => {
      const data = 'this is a very long string that should be truncated';
      buffer.append(data);
      const preview = buffer.getPreview(10);

      expect(preview).toBe('this is a ...');
      expect(preview.length).toBe(13); // 10 + '...'
    });
  });

  describe('validateAndCleanup', () => {
    it('should return true for valid buffer state', () => {
      const jsonContent = '{"jsonrpc":"2.0","id":1}';
      const frameData = `Content-Length: ${jsonContent.length}\r\n\r\n${jsonContent}`;
      buffer.append(frameData);

      expect(buffer.validateAndCleanup()).toBe(true);
      expect(buffer.hasData()).toBe(true);
    });

    it('should clear buffer when too large without complete header', () => {
      const largeBuffer = new DynamicBuffer(1000);
      const largeData = 'x'.repeat(900); // 90% of max size
      largeBuffer.append(largeData);

      expect(largeBuffer.validateAndCleanup()).toBe(false);
      expect(largeBuffer.hasData()).toBe(false);
    });

    it('should clear buffer when no Content-Length header found in large buffer', () => {
      const largeData = 'invalid data without proper header '.repeat(30);
      buffer.append(largeData);

      expect(buffer.validateAndCleanup()).toBe(false);
      expect(buffer.hasData()).toBe(false);
    });

    it('should keep buffer when Content-Length header is present', () => {
      const largeData = 'some data '.repeat(100) + 'Content-Length: 25';
      buffer.append(largeData);

      expect(buffer.validateAndCleanup()).toBe(true);
      expect(buffer.hasData()).toBe(true);
    });
  });

  describe('getStats', () => {
    it('should return correct buffer statistics', () => {
      const data = 'Content-Length: 25\r\n\r\n{"jsonrpc":"2.0","id":1}';
      buffer.append(data);

      const stats = buffer.getStats();

      expect(stats.size).toBe(data.length);
      expect(stats.maxSize).toBe(1024 * 1024);
      expect(stats.utilization).toBeCloseTo((data.length / (1024 * 1024)) * 100);
      expect(stats.hasCompleteHeader).toBe(true);
      expect(stats.preview).toBe(data.substring(0, 50));
    });

    it('should handle empty buffer stats', () => {
      const stats = buffer.getStats();

      expect(stats.size).toBe(0);
      expect(stats.utilization).toBe(0);
      expect(stats.hasCompleteHeader).toBe(false);
      expect(stats.preview).toBe('');
    });
  });

  describe('edge cases', () => {
    it('should handle Unicode characters correctly', () => {
      const jsonContent = '{"message":"こんにちは世界"}';
      const contentLength = Buffer.byteLength(jsonContent, 'utf8');
      const frameData = `Content-Length: ${contentLength}\r\n\r\n${jsonContent}`;

      buffer.append(frameData);
      const result = buffer.extractFrame();

      expect(result.extracted).toBe(true);
      expect(result.frame).toBe(jsonContent);
    });

    it('should handle frames with embedded newlines', () => {
      const jsonContent = '{\n  "jsonrpc": "2.0",\n  "method": "test",\n  "id": 1\n}';
      const contentLength = Buffer.byteLength(jsonContent, 'utf8');
      const frameData = `Content-Length: ${contentLength}\r\n\r\n${jsonContent}`;

      buffer.append(frameData);
      const result = buffer.extractFrame();

      expect(result.extracted).toBe(true);
      expect(result.frame).toBe(jsonContent);
    });

    it('should handle fragmented frame data', () => {
      const jsonContent = '{"jsonrpc":"2.0","id":1}';
      const frameData = `Content-Length: ${jsonContent.length}\r\n\r\n${jsonContent}`;

      // Add data in small chunks
      for (let i = 0; i < frameData.length; i += 5) {
        buffer.append(frameData.substring(i, i + 5));
      }

      const result = buffer.extractFrame();
      expect(result.extracted).toBe(true);
      expect(result.frame).toBe(jsonContent);
    });
  });
});
