using System;
using System.Text;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Parses Content-Length framed messages for JSON-RPC 2.0 communication.
    /// Handles HTTP-style headers with format: "Content-Length: <n>\r\n\r\n<json_content>"
    /// </summary>
    public class FrameParser
    {
        private const string CONTENT_LENGTH_HEADER = "Content-Length:";
        private const string HEADER_SEPARATOR = "\r\n\r\n";
        private const string LINE_SEPARATOR = "\r\n";
        
        /// <summary>
        /// Attempts to parse a Content-Length framed message from the buffer.
        /// </summary>
        /// <param name="buffer">The buffer containing the data</param>
        /// <param name="length">The length of valid data in the buffer</param>
        /// <param name="contentLength">The parsed content length, or -1 if parsing failed</param>
        /// <param name="headerLength">The total length of the header including separators, or -1 if parsing failed</param>
        /// <returns>True if the header was successfully parsed, false otherwise</returns>
        public bool TryParseFrame(byte[] buffer, int length, out int contentLength, out int headerLength)
        {
            contentLength = -1;
            headerLength = -1;
            
            if (buffer == null || length <= 0)
            {
                return false;
            }
            
            try
            {
                // Convert buffer to string for header parsing
                string data = Encoding.UTF8.GetString(buffer, 0, length);
                
                // Find the header separator
                int separatorIndex = data.IndexOf(HEADER_SEPARATOR, StringComparison.Ordinal);
                if (separatorIndex == -1)
                {
                    // Header not complete yet
                    return false;
                }
                
                // Extract header section
                string headerSection = data.Substring(0, separatorIndex);
                int tempHeaderLength = separatorIndex + HEADER_SEPARATOR.Length;
                
                // Parse Content-Length from header
                bool parseResult = TryParseContentLength(headerSection, out contentLength);
                
                // Only set headerLength if parsing was successful
                if (parseResult)
                {
                    headerLength = tempHeaderLength;
                    return true;
                }
                else
                {
                    // Reset values on failure
                    contentLength = -1;
                    headerLength = -1;
                    return false;
                }
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"[FrameParser] Error parsing frame: {ex.Message}");
                contentLength = -1;
                headerLength = -1;
                return false;
            }
        }
        
        /// <summary>
        /// Checks if a complete frame is available in the buffer.
        /// </summary>
        /// <param name="buffer">The buffer containing the data</param>
        /// <param name="length">The length of valid data in the buffer</param>
        /// <param name="contentLength">The expected content length</param>
        /// <param name="headerLength">The header length including separators</param>
        /// <returns>True if the complete frame is available, false otherwise</returns>
        public bool IsCompleteFrame(byte[] buffer, int length, int contentLength, int headerLength)
        {
            if (buffer == null || length <= 0 || contentLength < 0 || headerLength < 0)
            {
                return false;
            }
            
            int expectedTotalLength = headerLength + contentLength;
            return length >= expectedTotalLength;
        }
        
        /// <summary>
        /// Extracts the JSON content from a complete frame.
        /// </summary>
        /// <param name="buffer">The buffer containing the complete frame</param>
        /// <param name="contentLength">The content length</param>
        /// <param name="headerLength">The header length including separators</param>
        /// <returns>The JSON content as a string, or null if extraction failed</returns>
        public string ExtractJsonContent(byte[] buffer, int contentLength, int headerLength)
        {
            if (buffer == null || contentLength <= 0 || headerLength < 0)
            {
                return null;
            }
            
            try
            {
                // Extract JSON content after the header
                return Encoding.UTF8.GetString(buffer, headerLength, contentLength);
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"[FrameParser] Error extracting JSON content: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Validates that the Content-Length value is within acceptable limits.
        /// </summary>
        /// <param name="contentLength">The content length to validate</param>
        /// <returns>True if the content length is valid, false otherwise</returns>
        public bool IsValidContentLength(int contentLength)
        {
            return contentLength >= 0 && contentLength <= BufferConfig.MAX_MESSAGE_SIZE;
        }
        
        /// <summary>
        /// Parses the Content-Length value from the header section.
        /// </summary>
        /// <param name="headerSection">The header section string</param>
        /// <param name="contentLength">The parsed content length</param>
        /// <returns>True if parsing was successful, false otherwise</returns>
        private bool TryParseContentLength(string headerSection, out int contentLength)
        {
            contentLength = -1;
            
            if (string.IsNullOrEmpty(headerSection))
            {
                return false;
            }
            
            // Split header into lines
            string[] lines = headerSection.Split(new[] { LINE_SEPARATOR }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                
                // Check if this line contains Content-Length header
                if (trimmedLine.StartsWith(CONTENT_LENGTH_HEADER, StringComparison.OrdinalIgnoreCase))
                {
                    // Extract the value after the colon
                    int colonIndex = trimmedLine.IndexOf(':');
                    if (colonIndex == -1 || colonIndex >= trimmedLine.Length - 1)
                    {
                        continue;
                    }
                    
                    string valueString = trimmedLine.Substring(colonIndex + 1).Trim();
                    
                    // Try to parse the integer value
                    if (int.TryParse(valueString, out int parsedValue))
                    {
                        if (IsValidContentLength(parsedValue))
                        {
                            contentLength = parsedValue;
                            return true;
                        }
                        else
                        {
                            McpLogger.LogError($"[FrameParser] Content-Length value {parsedValue} exceeds maximum allowed size {BufferConfig.MAX_MESSAGE_SIZE}");
                            // Return false but don't set headerLength to -1 in calling method
                            contentLength = -1;
                            return false;
                        }
                    }
                    else
                    {
                        McpLogger.LogError($"[FrameParser] Invalid Content-Length value: '{valueString}'");
                        // Return false but don't set headerLength to -1 in calling method
                        contentLength = -1;
                        return false;
                    }
                }
            }
            
            McpLogger.LogError($"[FrameParser] Content-Length header not found in: {headerSection}");
            return false;
        }
    }
    
    /// <summary>
    /// Configuration constants for buffer management and frame parsing.
    /// </summary>
    public static class BufferConfig
    {
        /// <summary>
        /// Initial buffer size for new connections.
        /// </summary>
        public const int INITIAL_BUFFER_SIZE = 4096;
        
        /// <summary>
        /// Maximum buffer size to prevent memory exhaustion.
        /// </summary>
        public const int MAX_BUFFER_SIZE = 1024 * 1024; // 1MB
        
        /// <summary>
        /// Maximum allowed message size.
        /// </summary>
        public const int MAX_MESSAGE_SIZE = 1024 * 1024; // 1MB
        
        /// <summary>
        /// Buffer growth factor when resizing is needed.
        /// </summary>
        public const int BUFFER_GROWTH_FACTOR = 2;
        
        /// <summary>
        /// Minimum buffer size to maintain.
        /// </summary>
        public const int MIN_BUFFER_SIZE = 1024;
    }
}