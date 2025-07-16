import { ContentLengthFramer } from './content-length-framer.js';

describe('ContentLengthFramer', () => {
  describe('createFrame', () => {
    it('should create a valid Content-Length framed message', () => {
      const jsonContent = '{"jsonrpc":"2.0","id":1}';
      const result = ContentLengthFramer.createFrame(jsonContent);

      expect(result).toBe(`Content-Length: 25\r\n\r\n${jsonContent}`);
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
      const data = 'Content-Length: 25\r\n\r\n{"jsonrpc":"2.0","id":1}';
      const result = ContentLengthFramer.parseFrame(data);

      expect(result.contentLength).toBe(25);
      expect(result.headerLength).toBe(21);
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
      expect(result.headerLength).toBe(21);
      expect(result.isComplete).toBe(false);
    });

    it('should handle case-insensitive header', () => {
      const data = 'content-length: 15\r\n\r\n{"id":123,"ok":1}';
      const result = ContentLengthFramer.parseFrame(data);

      expect(result.contentLength).toBe(15);
      expect(result.headerLength).toBe(21);
      expect(result.isComplete).toBe(true);
    });

    it('should handle multiple headers', () => {
      const data =
        'Content-Type: application/json\r\nContent-Length: 30\r\n\r\n{"jsonrpc":"2.0","method":"test"}';
      const result = ContentLengthFramer.parseFrame(data);

      expect(result.contentLength).toBe(30);
      expect(result.headerLength).toBe(52);
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
      const result = ContentLengthFramer.extractFrame(data, jsonContent.length, 21);

      expect(result.jsonContent).toBe(jsonContent);
      expect(result.remainingData).toBe('');
    });

    it('should handle remaining data after frame', () => {
      const jsonContent = '{"jsonrpc":"2.0","id":1}';
      const remainingData = 'Content-Length: 10\r\n\r\n{"id":2}';
      const data = `Content-Length: ${jsonContent.length}\r\n\r\n${jsonContent}${remainingData}`;
      const result = ContentLengthFramer.extractFrame(data, jsonContent.length, 21);

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
      const result = ContentLengthFramer.extractFrame(data, contentLength, 21);

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
});
