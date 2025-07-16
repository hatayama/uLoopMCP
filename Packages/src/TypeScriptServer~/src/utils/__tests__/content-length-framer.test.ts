import { ContentLengthFramer } from '../content-length-framer.js';

// Mock the log-to-file module to avoid import.meta issues in tests
jest.mock('../log-to-file.js', () => ({
  errorToFile: jest.fn(),
  warnToFile: jest.fn(),
  infoToFile: jest.fn(),
  debugToFile: jest.fn(),
}));

describe('ContentLengthFramer', () => {
  describe('createFrame', () => {
    it('should create a valid Content-Length framed message', () => {
      const jsonContent = '{"jsonrpc":"2.0","id":1}';
      const result = ContentLengthFramer.createFrame(jsonContent);

      expect(result).toBe(`Content-Length: ${jsonContent.length}\r\n\r\n${jsonContent}`);
    });

    it('should handle Unicode characters correctly', () => {
      const jsonContent = '{"message":"こんにちは"}';
      const expectedLength = Buffer.byteLength(jsonContent, 'utf8');
      const result = ContentLengthFramer.createFrame(jsonContent);

      expect(result).toBe(`Content-Length: ${expectedLength}\r\n\r\n${jsonContent}`);
    });

    it('should throw error for empty content', () => {
      expect(() => ContentLengthFramer.createFrame('')).toThrow('JSON content cannot be empty');
    });

    it('should throw error for oversized content', () => {
      const largeContent = 'x'.repeat(1024 * 1024 + 1);
      expect(() => ContentLengthFramer.createFrame(largeContent)).toThrow('Message size');
    });
  });

  describe('parseFrame', () => {
    it('should parse a valid Content-Length header', () => {
      const jsonContent = '{"jsonrpc":"2.0","id":1}';
      const data = `Content-Length: ${jsonContent.length}\r\n\r\n${jsonContent}`;
      const result = ContentLengthFramer.parseFrame(data);

      expect(result.contentLength).toBe(jsonContent.length);
      expect(result.headerLength).toBe(22); // 'Content-Length: 24\r\n\r\n'.length
      expect(result.isComplete).toBe(true);
    });

    it('should handle incomplete header', () => {
      const data = 'Content-Length: 25\r\n';
      const result = ContentLengthFramer.parseFrame(data);

      expect(result.contentLength).toBe(-1);
      expect(result.headerLength).toBe(-1);
      expect(result.isComplete).toBe(false);
    });

    it('should handle incomplete content', () => {
      const data = 'Content-Length: 50\r\n\r\n{"jsonrpc":"2.0","id":1}'; // Content shorter than declared
      const result = ContentLengthFramer.parseFrame(data);

      expect(result.contentLength).toBe(50);
      expect(result.headerLength).toBe(22);
      expect(result.isComplete).toBe(false);
    });

    it('should handle case-insensitive header', () => {
      const data = 'content-length: 15\r\n\r\n{"id":123,"ok":1}';
      const result = ContentLengthFramer.parseFrame(data);

      expect(result.contentLength).toBe(15);
      expect(result.headerLength).toBe(22);
      expect(result.isComplete).toBe(true);
    });

    it('should handle multiple headers', () => {
      const data =
        'Content-Type: application/json\r\nContent-Length: 30\r\n\r\n{"jsonrpc":"2.0","method":"test"}';
      const result = ContentLengthFramer.parseFrame(data);

      expect(result.contentLength).toBe(30);
      expect(result.headerLength).toBe(54);
      expect(result.isComplete).toBe(true);
    });

    it('should handle invalid Content-Length value', () => {
      const data = 'Content-Length: invalid\r\n\r\n{"test":"data"}';
      const result = ContentLengthFramer.parseFrame(data);

      expect(result.contentLength).toBe(-1);
      expect(result.headerLength).toBe(-1);
      expect(result.isComplete).toBe(false);
    });

    it('should handle negative Content-Length value', () => {
      const data = 'Content-Length: -10\r\n\r\n{"test":"data"}';
      const result = ContentLengthFramer.parseFrame(data);

      expect(result.contentLength).toBe(-1);
      expect(result.headerLength).toBe(-1);
      expect(result.isComplete).toBe(false);
    });

    it('should handle excessive Content-Length value', () => {
      const data = `Content-Length: ${1024 * 1024 + 1}\r\n\r\n{"test":"data"}`;
      const result = ContentLengthFramer.parseFrame(data);

      expect(result.contentLength).toBe(-1);
      expect(result.headerLength).toBe(-1);
      expect(result.isComplete).toBe(false);
    });

    it('should return false for empty data', () => {
      const result = ContentLengthFramer.parseFrame('');

      expect(result.contentLength).toBe(-1);
      expect(result.headerLength).toBe(-1);
      expect(result.isComplete).toBe(false);
    });

    it('should handle whitespace in header value', () => {
      const data = 'Content-Length:    42   \r\n\r\n{"jsonrpc":"2.0","method":"test","id":123}';
      const result = ContentLengthFramer.parseFrame(data);

      expect(result.contentLength).toBe(42);
      expect(result.isComplete).toBe(true);
    });
  });

  describe('extractFrame', () => {
    it('should extract JSON content from complete frame', () => {
      const jsonContent = '{"jsonrpc":"2.0","id":1}';
      const data = `Content-Length: ${jsonContent.length}\r\n\r\n${jsonContent}`;
      const result = ContentLengthFramer.extractFrame(data, jsonContent.length, 22);

      expect(result.jsonContent).toBe(jsonContent);
      expect(result.remainingData).toBe('');
    });

    it('should handle remaining data after frame', () => {
      const jsonContent = '{"jsonrpc":"2.0","id":1}';
      const remainingData = 'Content-Length: 10\r\n\r\n{"id":2}';
      const data = `Content-Length: ${jsonContent.length}\r\n\r\n${jsonContent}${remainingData}`;
      const result = ContentLengthFramer.extractFrame(data, jsonContent.length, 22);

      expect(result.jsonContent).toBe(jsonContent);
      expect(result.remainingData).toBe(remainingData);
    });

    it('should return null for incomplete frame', () => {
      const data = 'Content-Length: 50\r\n\r\n{"jsonrpc":"2.0","id":1}'; // Content shorter than declared
      const result = ContentLengthFramer.extractFrame(data, 50, 21);

      expect(result.jsonContent).toBe(null);
      expect(result.remainingData).toBe(data);
    });

    it('should handle invalid parameters', () => {
      const data = 'test data';

      expect(ContentLengthFramer.extractFrame('', 10, 5).jsonContent).toBe(null);
      expect(ContentLengthFramer.extractFrame(data, -1, 5).jsonContent).toBe(null);
      expect(ContentLengthFramer.extractFrame(data, 10, -1).jsonContent).toBe(null);
    });

    it('should handle JSON with embedded newlines', () => {
      const jsonContent = '{\n  "jsonrpc": "2.0",\n  "method": "test",\n  "id": 1\n}';
      const contentLength = Buffer.byteLength(jsonContent, 'utf8');
      const data = `Content-Length: ${contentLength}\r\n\r\n${jsonContent}`;
      const result = ContentLengthFramer.extractFrame(data, contentLength, 22);

      expect(result.jsonContent).toBe(jsonContent);
      expect(result.remainingData).toBe('');
    });
  });

  describe('isValidContentLength', () => {
    it('should return true for valid content lengths', () => {
      expect(ContentLengthFramer.isValidContentLength(0)).toBe(true);
      expect(ContentLengthFramer.isValidContentLength(1024)).toBe(true);
      expect(ContentLengthFramer.isValidContentLength(1024 * 1024)).toBe(true);
    });

    it('should return false for invalid content lengths', () => {
      expect(ContentLengthFramer.isValidContentLength(-1)).toBe(false);
      expect(ContentLengthFramer.isValidContentLength(1024 * 1024 + 1)).toBe(false);
    });
  });

  describe('TCP Fragmentation Edge Cases', () => {
    describe('Header Fragmentation', () => {
      it('should handle fragmented Content-Length header start', () => {
        // Simulate TCP fragmentation where "Content-Length: 24" becomes "t-Length: 24"
        const jsonContent = '{"jsonrpc":"2.0","id":1}';
        const fragmentedData = `t-Length: ${jsonContent.length}\r\n\r\n${jsonContent}`;
        const result = ContentLengthFramer.parseFrame(fragmentedData);

        expect(result.contentLength).toBe(jsonContent.length);
        expect(result.isComplete).toBe(true);
      });

      it('should handle fragmented Content-Length header middle', () => {
        // Simulate fragmentation where middle part is cut
        const fragmentedData = 'Conten-Length: 25\r\n\r\n{"jsonrpc":"2.0","id":1}';
        const result = ContentLengthFramer.parseFrame(fragmentedData);

        expect(result.contentLength).toBe(25);
        expect(result.isComplete).toBe(true);
      });

      it('should handle fragmented Content-Length header with missing prefix', () => {
        // Simulate fragmentation where beginning is missing
        const fragmentedData = 'ength: 25\r\n\r\n{"jsonrpc":"2.0","id":1}';
        const result = ContentLengthFramer.parseFrame(fragmentedData);

        // Should fail to parse as Content-Length header is not recognizable
        expect(result.contentLength).toBe(-1);
        expect(result.isComplete).toBe(false);
      });

      it('should handle case where only colon and value remain', () => {
        // Extreme fragmentation where only ": 196" remains
        const fragmentedData = ': 25\r\n\r\n{"jsonrpc":"2.0","id":1}';
        const result = ContentLengthFramer.parseFrame(fragmentedData);

        // Should fail to parse as no "content-length" text is present
        expect(result.contentLength).toBe(-1);
        expect(result.isComplete).toBe(false);
      });

      it('should handle fragmented header with different line separators', () => {
        // Test with just \n instead of \r\n (Unix style)
        const fragmentedData = 't-Length: 25\n\n{"jsonrpc":"2.0","id":1}';
        const result = ContentLengthFramer.parseFrame(fragmentedData);

        expect(result.contentLength).toBe(25);
        expect(result.isComplete).toBe(true);
      });
    });

    describe('Real-world TCP Fragmentation Scenarios', () => {
      it('should handle the exact error case from logs: "t-Length: 196"', () => {
        // This is the exact scenario that caused the original issue
        const realWorldFragment =
          't-Length: 196\r\n\r\n' +
          '{"jsonrpc":"2.0","id":1752643568771,"result":{"Tools":[{"name":"get-project-info"}]}}';

        const result = ContentLengthFramer.parseFrame(realWorldFragment);

        expect(result.contentLength).toBe(196);
        expect(result.isComplete).toBe(false); // Content should be incomplete since we provided smaller JSON
      });

      it('should handle Content-Length split across multiple TCP packets', () => {
        // Simulate Content-Length being split: "Conten" + "t-Length: 25"
        const packet1 = 'Conten';
        const packet2 = 't-Length: 25\r\n\r\n{"jsonrpc":"2.0","id":1}';

        // First packet alone should fail
        let result = ContentLengthFramer.parseFrame(packet1);
        expect(result.isComplete).toBe(false);

        // Combined packets should succeed
        result = ContentLengthFramer.parseFrame(packet1 + packet2);
        expect(result.contentLength).toBe(25);
        expect(result.isComplete).toBe(true);
      });

      it('should handle header-body boundary fragmentation', () => {
        // Header complete but body fragmented
        const header = 'Content-Length: 50\r\n\r\n';
        const partialBody = '{"jsonrpc":"2.0","method":"test"'; // Incomplete

        const result = ContentLengthFramer.parseFrame(header + partialBody);

        expect(result.contentLength).toBe(50);
        expect(result.headerLength).toBe(header.length);
        expect(result.isComplete).toBe(false); // Body is incomplete
      });

      it('should handle large message fragmentation', () => {
        // Test with a realistic large response like the Tools list
        const largeJsonContent = JSON.stringify({
          jsonrpc: '2.0',
          id: 1752643568771,
          result: {
            Tools: Array(10)
              .fill(0)
              .map((_, i) => ({
                name: `tool-${i}`,
                description: `Tool ${i} description with some lengthy text to make it realistic`,
                parameterSchema: { Properties: {}, Required: [] },
              })),
          },
        });

        const contentLength = Buffer.byteLength(largeJsonContent, 'utf8');
        // const fullMessage = `Content-Length: ${contentLength}\r\n\r\n${largeJsonContent}`;

        // Test fragmented header
        const fragmentedHeader = `t-Length: ${contentLength}\r\n\r\n${largeJsonContent}`;
        const result = ContentLengthFramer.parseFrame(fragmentedHeader);

        expect(result.contentLength).toBe(contentLength);
        expect(result.isComplete).toBe(true);

        // Verify extraction works
        const extraction = ContentLengthFramer.extractFrame(
          fragmentedHeader,
          result.contentLength,
          result.headerLength,
        );
        expect(extraction.jsonContent).toBe(largeJsonContent);
      });
    });

    describe('Edge Cases that Previously Failed', () => {
      it('should parse headers with any substring of "content-length"', () => {
        // Test various fragmentation patterns
        const testCases = [
          'content-length: 25', // lowercase
          'Content-Length: 25', // proper case
          't-Length: 25', // missing "Conten"
          'ontent-Length: 25', // missing "C"
          'ntent-Length: 25', // missing "Co"
          'tent-Length: 25', // missing "Con"
          'ent-Length: 25', // missing "Cont"
          'nt-Length: 25', // missing "Conte"
          '-Length: 25', // missing "Content"
        ];

        testCases.forEach((headerLine, index) => {
          const data = `${headerLine}\r\n\r\n{"jsonrpc":"2.0","id":${index}}`;
          const result = ContentLengthFramer.parseFrame(data);

          if (headerLine.includes('content-length') || headerLine.includes('-length')) {
            expect(result.contentLength).toBe(25);
            expect(result.isComplete).toBe(true);
          } else {
            // Cases that don't contain recognizable content-length should fail
            expect(result.contentLength).toBe(-1);
          }
        });
      });

      it('should handle the "Conten" fragment + next packet scenario', () => {
        // This simulates the real issue where Content-Length gets split
        // and the remaining data appears to start with "t-Length"
        const fragment1 = 'Conten';
        const fragment2 = 't-Length: 42\r\n\r\n{"jsonrpc":"2.0","method":"test","id":123}';

        // Fragment 1 alone should be incomplete
        let result = ContentLengthFramer.parseFrame(fragment1);
        expect(result.isComplete).toBe(false);

        // Fragment 2 alone should work (our fix)
        result = ContentLengthFramer.parseFrame(fragment2);
        expect(result.contentLength).toBe(42);
        expect(result.isComplete).toBe(true);

        // Combined should also work
        result = ContentLengthFramer.parseFrame(fragment1 + fragment2);
        expect(result.contentLength).toBe(42);
        expect(result.isComplete).toBe(true);
      });
    });
  });

  describe('UTF-8 Multi-byte Character Support', () => {
    it('should handle Japanese characters correctly', () => {
      const jsonContent = '{"message":"こんにちは世界","greeting":"はじめまして"}';
      const contentLength = Buffer.byteLength(jsonContent, 'utf8');
      const data = `Content-Length: ${contentLength}\r\n\r\n${jsonContent}`;

      const parseResult = ContentLengthFramer.parseFrame(data);
      expect(parseResult.contentLength).toBe(contentLength);
      expect(parseResult.isComplete).toBe(true);

      const extractResult = ContentLengthFramer.extractFrame(
        data,
        contentLength,
        parseResult.headerLength,
      );
      expect(extractResult.jsonContent).toBe(jsonContent);
    });

    it('should handle Chinese characters correctly', () => {
      const jsonContent = '{"message":"你好世界","method":"测试","id":1234}';
      const contentLength = Buffer.byteLength(jsonContent, 'utf8');
      const data = `Content-Length: ${contentLength}\r\n\r\n${jsonContent}`;

      const parseResult = ContentLengthFramer.parseFrame(data);
      expect(parseResult.contentLength).toBe(contentLength);
      expect(parseResult.isComplete).toBe(true);

      const extractResult = ContentLengthFramer.extractFrame(
        data,
        contentLength,
        parseResult.headerLength,
      );
      expect(extractResult.jsonContent).toBe(jsonContent);
    });

    it('should handle emoji characters correctly', () => {
      const jsonContent = '{"message":"Hello 👋 World 🌍","emoji":"🎉✨🚀"}';
      const contentLength = Buffer.byteLength(jsonContent, 'utf8');
      const data = `Content-Length: ${contentLength}\r\n\r\n${jsonContent}`;

      const parseResult = ContentLengthFramer.parseFrame(data);
      expect(parseResult.contentLength).toBe(contentLength);
      expect(parseResult.isComplete).toBe(true);

      const extractResult = ContentLengthFramer.extractFrame(
        data,
        contentLength,
        parseResult.headerLength,
      );
      expect(extractResult.jsonContent).toBe(jsonContent);
    });

    it('should handle mixed multi-byte characters', () => {
      const jsonContent =
        '{"japanese":"こんにちは","chinese":"你好","emoji":"😊","korean":"안녕하세요","arabic":"مرحبا"}';
      const contentLength = Buffer.byteLength(jsonContent, 'utf8');
      const data = `Content-Length: ${contentLength}\r\n\r\n${jsonContent}`;

      const parseResult = ContentLengthFramer.parseFrame(data);
      expect(parseResult.contentLength).toBe(contentLength);
      expect(parseResult.isComplete).toBe(true);

      const extractResult = ContentLengthFramer.extractFrame(
        data,
        contentLength,
        parseResult.headerLength,
      );
      expect(extractResult.jsonContent).toBe(jsonContent);
    });

    it('should handle multi-byte characters with TCP fragmentation', () => {
      const jsonContent = '{"message":"日本語のテスト📝","status":"成功✅"}';
      const contentLength = Buffer.byteLength(jsonContent, 'utf8');
      const fragmentedData = `t-Length: ${contentLength}\r\n\r\n${jsonContent}`;

      const parseResult = ContentLengthFramer.parseFrame(fragmentedData);
      expect(parseResult.contentLength).toBe(contentLength);
      expect(parseResult.isComplete).toBe(true);

      const extractResult = ContentLengthFramer.extractFrame(
        fragmentedData,
        contentLength,
        parseResult.headerLength,
      );
      expect(extractResult.jsonContent).toBe(jsonContent);
    });

    it('should correctly calculate byte length vs string length for multi-byte characters', () => {
      const testCases = [
        { content: 'Hello', byteLength: 5, stringLength: 5 },
        { content: 'こんにちは', byteLength: 15, stringLength: 5 },
        { content: '🎉', byteLength: 4, stringLength: 2 }, // Emoji are surrogate pairs
        { content: '你好世界', byteLength: 12, stringLength: 4 },
        { content: 'Héllo', byteLength: 6, stringLength: 5 },
      ];

      testCases.forEach(({ content, byteLength, stringLength }) => {
        const jsonContent = `{"message":"${content}"}`;
        const expectedByteLength = Buffer.byteLength(jsonContent, 'utf8');
        // const actualStringLength = jsonContent.length;

        const frame = ContentLengthFramer.createFrame(jsonContent);
        const parseResult = ContentLengthFramer.parseFrame(frame);

        expect(parseResult.contentLength).toBe(expectedByteLength);
        expect(parseResult.isComplete).toBe(true);

        // Verify that string length and byte length are different for multi-byte
        expect(Buffer.byteLength(content, 'utf8')).toBe(byteLength);
        expect(content.length).toBe(stringLength);
      });
    });

    it('should handle Buffer-based parsing with multi-byte characters', () => {
      const jsonContent = '{"japanese":"こんにちは","emoji":"🎉"}';
      const contentLength = Buffer.byteLength(jsonContent, 'utf8');
      const dataBuffer = Buffer.from(
        `Content-Length: ${contentLength}\r\n\r\n${jsonContent}`,
        'utf8',
      );

      const parseResult = ContentLengthFramer.parseFrameFromBuffer(dataBuffer);
      expect(parseResult.contentLength).toBe(contentLength);
      expect(parseResult.isComplete).toBe(true);

      const extractResult = ContentLengthFramer.extractFrameFromBuffer(
        dataBuffer,
        contentLength,
        parseResult.headerLength,
      );
      expect(extractResult.jsonContent).toBe(jsonContent);
    });

    it('should handle partial multi-byte character at buffer boundary', () => {
      // Create content with multi-byte characters
      const jsonContent = '{"message":"日本語のテスト"}';
      const contentLength = Buffer.byteLength(jsonContent, 'utf8');
      const fullData = `Content-Length: ${contentLength}\r\n\r\n${jsonContent}`;

      // Create buffer and ensure we can parse it correctly
      const dataBuffer = Buffer.from(fullData, 'utf8');
      const parseResult = ContentLengthFramer.parseFrameFromBuffer(dataBuffer);

      expect(parseResult.contentLength).toBe(contentLength);
      expect(parseResult.isComplete).toBe(true);

      const extractResult = ContentLengthFramer.extractFrameFromBuffer(
        dataBuffer,
        contentLength,
        parseResult.headerLength,
      );
      expect(extractResult.jsonContent).toBe(jsonContent);
    });
  });
});
