/**
 * Large message test for ContentLengthFramer bug reproduction
 * Tests the specific issue where 14.6KB messages result in isComplete=false
 */

import { ContentLengthFramer } from '../content-length-framer.js';

// Mock log functions to avoid ESM issues in tests
jest.mock('../log-to-file.js', () => ({
  errorToFile: jest.fn(),
  debugToFile: jest.fn(),
}));

describe('ContentLengthFramer Large Message Bug', () => {
  test('14.6KB message should be properly parsed as complete', () => {
    // Create a large JSON content similar to get-tool-details response
    const largeJsonContent = JSON.stringify({
      jsonrpc: '2.0',
      id: 1752649077901,
      result: {
        Tools: Array.from({ length: 16 }, (_, i) => ({
          name: `tool-${i}`,
          description: `Description for tool ${i}`.repeat(50), // Make it large
          parameterSchema: {
            Properties: {
              Parameter1: {
                Type: 'string',
                Description: 'A very long description that takes up space'.repeat(10),
                DefaultValue: 'default value',
                Enum: null,
              },
              Parameter2: {
                Type: 'number',
                Description: 'Another long description for testing purposes'.repeat(8),
                DefaultValue: 15,
                Enum: null,
              },
              Parameter3: {
                Type: 'boolean',
                Description: 'Boolean parameter with extensive documentation'.repeat(12),
                DefaultValue: true,
                Enum: null,
              },
            },
            Required: [],
          },
          displayDevelopmentOnly: false,
        })),
        StartedAt: '2025-07-16T15:54:50.383',
        EndedAt: '2025-07-16T15:54:50.388',
        ExecutionTimeMs: 4,
      },
    });

    // Ensure the content is close to 14.6KB (14576 bytes)
    expect(largeJsonContent.length).toBeGreaterThan(14000);

    // Create Content-Length frame
    const contentLength = Buffer.byteLength(largeJsonContent, 'utf8');
    const frame = `Content-Length: ${contentLength}\r\n\r\n${largeJsonContent}`;

    // Verify frame size is appropriate
    expect(frame.length).toBeGreaterThan(contentLength);

    // Test parseFrame method
    const parseResult = ContentLengthFramer.parseFrame(frame);

    // Verify parse result structure
    expect(parseResult.contentLength).toBe(contentLength);
    expect(parseResult.headerLength).toBeGreaterThan(0);
    expect(parseResult.isComplete).toBe(true);
    expect(parseResult.headerLength + parseResult.contentLength).toBe(frame.length);
    expect(parseResult.isComplete).toBe(true); // This should be true but currently fails

    // Test extractFrame method
    const extractResult = ContentLengthFramer.extractFrame(
      frame,
      parseResult.contentLength,
      parseResult.headerLength,
    );

    expect(extractResult.jsonContent).toBe(largeJsonContent);
    expect(extractResult.remainingData).toBe('');
  });

  test('Exact 14601 bytes frame (from Unity log) should be complete', () => {
    // Create exact scenario from Unity log
    const exactContentLength = 14576; // From Unity log
    const exactFrameSize = 14601; // From Unity log

    // Calculate exact JSON content size
    // const headerSize = exactFrameSize - exactContentLength;
    const jsonContent = 'x'.repeat(exactContentLength); // Simple content for testing

    const frame = `Content-Length: ${exactContentLength}\r\n\r\n${jsonContent}`;

    // Verify frame size matches expected size
    expect(frame.length).toBe(exactFrameSize);

    const parseResult = ContentLengthFramer.parseFrame(frame);

    // Verify exact scenario parse result
    expect(parseResult.contentLength).toBe(exactContentLength);
    expect(parseResult.headerLength).toBeGreaterThan(0);
    expect(parseResult.isComplete).toBe(true);
    expect(parseResult.headerLength + parseResult.contentLength).toBe(frame.length);
  });

  test('Frame size calculation edge cases', () => {
    const testCases = [
      { contentLength: 14576, description: 'Unity scenario' },
      { contentLength: 10000, description: '10KB message' },
      { contentLength: 20000, description: '20KB message' },
      { contentLength: 1, description: 'Tiny message' },
    ];

    testCases.forEach(({ contentLength }) => {
      const jsonContent = 'x'.repeat(contentLength);
      const frame = `Content-Length: ${contentLength}\r\n\r\n${jsonContent}`;

      const parseResult = ContentLengthFramer.parseFrame(frame);

      // Verify frame parsing results
      expect(parseResult.isComplete).toBe(true);
      expect(parseResult.contentLength).toBe(contentLength);
    });
  });
});
