import { DynamicBuffer } from '../dynamic-buffer.js';

// Mock the log-to-file module to avoid import.meta issues in tests
jest.mock('../log-to-file.js', () => ({
  errorToFile: jest.fn(),
  warnToFile: jest.fn(),
  infoToFile: jest.fn(),
  debugToFile: jest.fn(),
}));

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

  describe('TCP Fragmentation Scenarios', () => {
    describe('Header Fragmentation in DynamicBuffer', () => {
      it('should handle Content-Length header split across multiple appends', () => {
        const jsonContent = '{"jsonrpc":"2.0","id":1}';

        // Simulate TCP fragmentation: "Content-Length: 25" gets split
        buffer.append('Conten');
        buffer.append('t-Length: ');
        buffer.append(`${jsonContent.length}\r\n\r\n`);
        buffer.append(jsonContent);

        const result = buffer.extractFrame();
        expect(result.extracted).toBe(true);
        expect(result.frame).toBe(jsonContent);
      });

      it('should handle the exact fragmentation that caused the bug: "t-Length: 196"', () => {
        // This simulates the real-world scenario from the error logs
        const largeJsonContent = JSON.stringify({
          jsonrpc: '2.0',
          id: 1752643568771,
          result: {
            Tools: [
              { name: 'get-project-info', description: 'Get detailed Unity project information' },
              { name: 'compile', description: 'Execute Unity project compilation' },
              { name: 'ping', description: 'Connection test and message echo' },
            ],
          },
        });

        const contentLength = Buffer.byteLength(largeJsonContent, 'utf8');

        // First append: Fragmented header starting with "t-Length"
        buffer.append(`t-Length: ${contentLength}\r\n\r\n`);
        // Second append: The actual JSON content
        buffer.append(largeJsonContent);

        const result = buffer.extractFrame();
        expect(result.extracted).toBe(true);
        expect(result.frame).toBe(largeJsonContent);
      });

      it('should handle extreme header fragmentation (single character appends)', () => {
        const jsonContent = '{"id":1}';
        const headerText = `Content-Length: ${jsonContent.length}\r\n\r\n`;

        // Add header character by character
        for (let i = 0; i < headerText.length; i++) {
          buffer.append(headerText.charAt(i));
        }

        // Add content
        buffer.append(jsonContent);

        const result = buffer.extractFrame();
        expect(result.extracted).toBe(true);
        expect(result.frame).toBe(jsonContent);
      });
    });

    describe('Multi-packet TCP Scenarios', () => {
      it('should handle header spread across 3 packets', () => {
        const jsonContent = '{"jsonrpc":"2.0","method":"test","id":123}';

        // Packet 1: Part of header
        buffer.append('Content-Len');
        expect(buffer.extractFrame().extracted).toBe(false);

        // Packet 2: Rest of header
        buffer.append(`gth: ${jsonContent.length}\r\n\r\n`);
        expect(buffer.extractFrame().extracted).toBe(false);

        // Packet 3: Content
        buffer.append(jsonContent);

        const result = buffer.extractFrame();
        expect(result.extracted).toBe(true);
        expect(result.frame).toBe(jsonContent);
      });

      it('should handle content split across multiple packets', () => {
        const jsonContent =
          '{"jsonrpc":"2.0","method":"very-long-method-name","id":123,"params":{"key":"value"}}';
        const contentLength = Buffer.byteLength(jsonContent, 'utf8');

        // Add complete header
        buffer.append(`Content-Length: ${contentLength}\r\n\r\n`);

        // Add content in chunks
        const chunkSize = 10;
        for (let i = 0; i < jsonContent.length; i += chunkSize) {
          const chunk = jsonContent.substring(i, i + chunkSize);
          buffer.append(chunk);

          // Should not extract until complete
          if (i + chunkSize < jsonContent.length) {
            expect(buffer.extractFrame().extracted).toBe(false);
          }
        }

        const result = buffer.extractFrame();
        expect(result.extracted).toBe(true);
        expect(result.frame).toBe(jsonContent);
      });
    });

    describe('Mixed Complete and Fragmented Messages', () => {
      it('should extract complete frames while keeping fragmented ones in buffer', () => {
        const json1 = '{"id":1}';
        const json2 = '{"id":2,"method":"test"}';

        // Add first complete frame
        const frame1 = `Content-Length: ${json1.length}\r\n\r\n${json1}`;
        buffer.append(frame1);

        // Add partial second frame (header only)
        buffer.append(`Content-Length: ${json2.length}\r\n\r\n`);

        // First extraction should get first frame
        let result = buffer.extractFrame();
        expect(result.extracted).toBe(true);
        expect(result.frame).toBe(json1);

        // Second extraction should fail (incomplete)
        result = buffer.extractFrame();
        expect(result.extracted).toBe(false);

        // Complete the second frame
        buffer.append(json2);

        // Now second extraction should succeed
        result = buffer.extractFrame();
        expect(result.extracted).toBe(true);
        expect(result.frame).toBe(json2);
      });

      it('should handle corrupted data gracefully', () => {
        // Add some invalid data
        buffer.append('Invalid data without proper header');

        // Should not crash and should return no frames
        const frames = buffer.extractAllFrames();
        expect(frames).toHaveLength(0);

        // Buffer should still be usable after clearing
        buffer.clear();

        const jsonContent = '{"id":1}';
        const validFrame = `Content-Length: ${jsonContent.length}\r\n\r\n${jsonContent}`;
        buffer.append(validFrame);

        const result = buffer.extractFrame();
        expect(result.extracted).toBe(true);
        expect(result.frame).toBe(jsonContent);
      });
    });

    describe('Buffer Type Handling', () => {
      it('should handle Buffer objects correctly', () => {
        const jsonContent = '{"jsonrpc":"2.0","id":1}';
        const frameData = `Content-Length: ${jsonContent.length}\r\n\r\n${jsonContent}`;

        // Append as Buffer instead of string
        buffer.append(Buffer.from(frameData, 'utf8'));

        const result = buffer.extractFrame();
        expect(result.extracted).toBe(true);
        expect(result.frame).toBe(jsonContent);
      });

      it('should handle mixed Buffer and string appends', () => {
        const jsonContent = '{"jsonrpc":"2.0","id":1}';

        // Mix Buffer and string appends
        buffer.append(Buffer.from('Content-Length: ', 'utf8'));
        buffer.append(`${jsonContent.length}`);
        buffer.append(Buffer.from('\r\n\r\n', 'utf8'));
        buffer.append(jsonContent);

        const result = buffer.extractFrame();
        expect(result.extracted).toBe(true);
        expect(result.frame).toBe(jsonContent);
      });
    });
  });

  describe('UTF-8 Multi-byte Character Handling', () => {
    it('should handle Japanese characters in DynamicBuffer', () => {
      const jsonContent = '{"message":"こんにちは世界","status":"成功"}';
      const contentLength = Buffer.byteLength(jsonContent, 'utf8');
      const frameData = `Content-Length: ${contentLength}\r\n\r\n${jsonContent}`;

      buffer.append(frameData);
      const result = buffer.extractFrame();

      expect(result.extracted).toBe(true);
      expect(result.frame).toBe(jsonContent);
      expect(buffer.getSize()).toBe(0);
    });

    it('should handle emoji characters in DynamicBuffer', () => {
      const jsonContent = '{"message":"Hello 👋 World 🌍","emoji":"🎉✨🚀"}';
      const contentLength = Buffer.byteLength(jsonContent, 'utf8');
      const frameData = `Content-Length: ${contentLength}\r\n\r\n${jsonContent}`;

      buffer.append(frameData);
      const result = buffer.extractFrame();

      expect(result.extracted).toBe(true);
      expect(result.frame).toBe(jsonContent);
      expect(buffer.getSize()).toBe(0);
    });

    it('should handle mixed multi-byte characters with fragmentation', () => {
      const jsonContent =
        '{"japanese":"こんにちは","chinese":"你好","emoji":"😊","korean":"안녕하세요"}';
      const contentLength = Buffer.byteLength(jsonContent, 'utf8');

      // Simulate fragmentation in header
      buffer.append('t-Length: ');
      buffer.append(`${contentLength}\r\n\r\n`);
      buffer.append(jsonContent);

      const result = buffer.extractFrame();
      expect(result.extracted).toBe(true);
      expect(result.frame).toBe(jsonContent);
    });

    it('should handle Buffer objects with multi-byte characters', () => {
      const jsonContent = '{"message":"日本語のテスト📝","status":"成功✅"}';
      const contentLength = Buffer.byteLength(jsonContent, 'utf8');
      const frameData = `Content-Length: ${contentLength}\r\n\r\n${jsonContent}`;

      // Append as Buffer
      buffer.append(Buffer.from(frameData, 'utf8'));

      const result = buffer.extractFrame();
      expect(result.extracted).toBe(true);
      expect(result.frame).toBe(jsonContent);
    });

    it('should handle multi-byte characters across append boundaries', () => {
      const jsonContent = '{"message":"日本語のテスト"}';
      const contentLength = Buffer.byteLength(jsonContent, 'utf8');

      // Split at a point that might break multi-byte character
      const header = `Content-Length: ${contentLength}\r\n\r\n`;
      const jsonBuffer = Buffer.from(jsonContent, 'utf8');

      // Add header
      buffer.append(header);

      // Add JSON content in chunks that might split multi-byte chars
      const chunkSize = 3; // This might split UTF-8 sequences
      for (let i = 0; i < jsonBuffer.length; i += chunkSize) {
        const chunk = jsonBuffer.subarray(i, i + chunkSize);
        buffer.append(chunk);
      }

      const result = buffer.extractFrame();
      expect(result.extracted).toBe(true);
      expect(result.frame).toBe(jsonContent);
    });

    it('should handle large multi-byte content correctly', () => {
      // Create a larger content with many multi-byte characters
      const multiByteText = '日本語のテストです。これは長いメッセージです。'.repeat(10);
      const jsonContent = `{"message":"${multiByteText}","length":${multiByteText.length}}`;
      const contentLength = Buffer.byteLength(jsonContent, 'utf8');
      const frameData = `Content-Length: ${contentLength}\r\n\r\n${jsonContent}`;

      buffer.append(frameData);
      const result = buffer.extractFrame();

      expect(result.extracted).toBe(true);
      expect(result.frame).toBe(jsonContent);
      expect(buffer.getSize()).toBe(0);
    });

    it('should handle byte vs string length differences correctly', () => {
      // Create content where byte length != string length
      const content = 'こんにちは'; // 5 characters, 15 bytes
      const jsonContent = `{"message":"${content}"}`;
      const contentLength = Buffer.byteLength(jsonContent, 'utf8');
      const frameData = `Content-Length: ${contentLength}\r\n\r\n${jsonContent}`;

      // Verify our assumption
      expect(content.length).toBe(5); // 5 characters
      expect(Buffer.byteLength(content, 'utf8')).toBe(15); // 15 bytes

      buffer.append(frameData);
      const result = buffer.extractFrame();

      expect(result.extracted).toBe(true);
      expect(result.frame).toBe(jsonContent);
    });

    it('should handle preview with multi-byte characters', () => {
      const jsonContent = '{"message":"日本語のテスト📝"}';
      const contentLength = Buffer.byteLength(jsonContent, 'utf8');
      const frameData = `Content-Length: ${contentLength}\r\n\r\n${jsonContent}`;

      buffer.append(frameData);

      // Preview should handle multi-byte characters correctly
      const preview = buffer.getPreview(20);
      expect(preview).toMatch(/^Content-Length: \d+/);
      expect(preview.length).toBeLessThanOrEqual(23); // 20 + '...'
    });

    it('should validate buffer cleanup with multi-byte headers', () => {
      const largeContent = '日本語のテスト'.repeat(200);

      // Add content with multi-byte but no valid header
      buffer.append(largeContent);

      // Should clean up large buffer without proper header
      expect(buffer.validateAndCleanup()).toBe(false);
      expect(buffer.hasData()).toBe(false);

      // Now add valid multi-byte content with header
      const jsonContent = `{"message":"${largeContent}"}`;
      const contentLength = Buffer.byteLength(jsonContent, 'utf8');
      const frameData = `Content-Length: ${contentLength}\r\n\r\n${jsonContent}`;

      buffer.append(frameData);
      expect(buffer.validateAndCleanup()).toBe(true);
      expect(buffer.hasData()).toBe(true);
    });
  });
});
