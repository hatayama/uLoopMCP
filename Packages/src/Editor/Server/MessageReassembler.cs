using System;
using System.Collections.Generic;
using System.Text;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Reassembles fragmented TCP messages using Content-Length framing.
    /// Handles partial frames and maintains state across multiple data chunks.
    /// </summary>
    public class MessageReassembler : IDisposable
    {
        private readonly DynamicBufferManager bufferManager;
        private byte[] assemblyBuffer;
        private int currentDataLength = 0;
        private bool disposed = false;
        
        // Frame parsing state
        private int expectedContentLength = -1;
        private int headerLength = -1;
        private bool headerParsed = false;
        
        // Statistics
        private int totalMessagesReassembled = 0;
        private int totalDataChunksProcessed = 0;
        
        /// <summary>
        /// Initializes a new instance of the MessageReassembler.
        /// </summary>
        /// <param name="bufferManager">The buffer manager to use for memory management</param>
        public MessageReassembler(DynamicBufferManager bufferManager = null)
        {
            this.bufferManager = bufferManager ?? new DynamicBufferManager();
            this.assemblyBuffer = this.bufferManager.GetBuffer(BufferConfig.INITIAL_BUFFER_SIZE);
        }
        
        /// <summary>
        /// Gets whether there is incomplete data waiting for more chunks.
        /// </summary>
        public bool HasIncompleteData => currentDataLength > 0;
        
        /// <summary>
        /// Gets the current amount of data in the assembly buffer.
        /// </summary>
        public int CurrentDataLength => currentDataLength;
        
        /// <summary>
        /// Gets whether the header has been parsed and we're waiting for content.
        /// </summary>
        public bool IsWaitingForContent => headerParsed && expectedContentLength > 0;
        
        /// <summary>
        /// Adds new data to the reassembly buffer.
        /// </summary>
        /// <param name="data">The data chunk to add</param>
        /// <param name="length">The length of valid data in the chunk</param>
        public void AddData(byte[] data, int length)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(MessageReassembler));
            }
            
            if (data == null || length <= 0)
            {
                return;
            }
            
            if (length > data.Length)
            {
                throw new ArgumentException("Length cannot exceed data array size", nameof(length));
            }
            
            totalDataChunksProcessed++;
            
            // Ensure we have enough space in the assembly buffer
            int requiredSize = currentDataLength + length;
            if (requiredSize > assemblyBuffer.Length)
            {
                bufferManager.ResizeBuffer(ref assemblyBuffer, currentDataLength, requiredSize);
            }
            
            // Copy new data to assembly buffer
            Array.Copy(data, 0, assemblyBuffer, currentDataLength, length);
            currentDataLength += length;
            
            McpLogger.LogDebug($"[MessageReassembler] Added {length} bytes, total: {currentDataLength}");
        }
        
        /// <summary>
        /// Attempts to extract complete messages from the assembly buffer.
        /// </summary>
        /// <returns>Array of complete JSON messages, or empty array if none available</returns>
        public string[] ExtractCompleteMessages()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(MessageReassembler));
            }
            
            var completeMessages = new List<string>();
            
            while (currentDataLength > 0)
            {
                string extractedMessage = TryExtractSingleMessage();
                if (extractedMessage == null)
                {
                    // No complete message available
                    break;
                }
                
                completeMessages.Add(extractedMessage);
                totalMessagesReassembled++;
            }
            
            return completeMessages.ToArray();
        }
        
        /// <summary>
        /// Attempts to extract a single complete message from the buffer.
        /// </summary>
        /// <returns>The extracted JSON message, or null if not complete</returns>
        private string TryExtractSingleMessage()
        {
            // If we haven't parsed the header yet, try to parse it
            if (!headerParsed)
            {
                if (!TryParseHeader())
                {
                    // Header not complete yet
                    return null;
                }
            }
            
            // Early detection of abnormal states - throw exceptions immediately
            if (expectedContentLength < 0)
            {
                throw new InvalidOperationException($"Invalid Content-Length value: {expectedContentLength}. Message framing is corrupted.");
            }
            
            if (headerLength < 0)
            {
                throw new InvalidOperationException($"Invalid header length: {headerLength}. Message framing is corrupted.");
            }
            
            if (expectedContentLength > BufferConfig.MAX_MESSAGE_SIZE)
            {
                throw new InvalidOperationException($"Content-Length {expectedContentLength} exceeds maximum message size {BufferConfig.MAX_MESSAGE_SIZE}.");
            }
            
            // Check if we have the complete message
            int expectedTotalLength = headerLength + expectedContentLength;
            if (currentDataLength < expectedTotalLength)
            {
                // Message not complete yet
                return null;
            }
            
            // Validate buffer bounds before extraction
            if (headerLength + expectedContentLength > currentDataLength)
            {
                throw new InvalidOperationException($"Buffer underflow: trying to read {expectedContentLength} bytes at offset {headerLength}, but only {currentDataLength} bytes available.");
            }
            
            // Extract the JSON content
            string jsonContent = Encoding.UTF8.GetString(assemblyBuffer, headerLength, expectedContentLength);
            
            // Log before resetting state
            McpLogger.LogDebug($"[MessageReassembler] Extracted complete message of {expectedContentLength} bytes");
            
            // Remove the processed message from the buffer
            RemoveProcessedData(expectedTotalLength);
            
            // Reset parsing state for next message
            ResetParsingState();
            
            return jsonContent;
        }
        
        /// <summary>
        /// Attempts to parse the Content-Length header from the current buffer.
        /// </summary>
        /// <returns>True if header was successfully parsed, false otherwise</returns>
        private bool TryParseHeader()
        {
            var frameParser = new FrameParser();
            
            bool parseResult = frameParser.TryParseFrame(assemblyBuffer, currentDataLength, 
                out expectedContentLength, out headerLength);
            
            if (parseResult)
            {
                // Early validation of parsed values
                if (expectedContentLength < 0)
                {
                    throw new InvalidOperationException($"FrameParser returned invalid Content-Length: {expectedContentLength}");
                }
                
                if (headerLength < 0)
                {
                    throw new InvalidOperationException($"FrameParser returned invalid header length: {headerLength}");
                }
                
                headerParsed = true;
                McpLogger.LogDebug($"[MessageReassembler] Parsed header: ContentLength={expectedContentLength}, HeaderLength={headerLength}");
            }
            
            return parseResult;
        }
        
        /// <summary>
        /// Removes processed data from the beginning of the assembly buffer.
        /// </summary>
        /// <param name="bytesToRemove">Number of bytes to remove from the beginning</param>
        private void RemoveProcessedData(int bytesToRemove)
        {
            if (bytesToRemove <= 0 || bytesToRemove > currentDataLength)
            {
                return;
            }
            
            // Move remaining data to the beginning of the buffer
            int remainingDataLength = currentDataLength - bytesToRemove;
            if (remainingDataLength > 0)
            {
                Array.Copy(assemblyBuffer, bytesToRemove, assemblyBuffer, 0, remainingDataLength);
            }
            
            currentDataLength = remainingDataLength;
            
            McpLogger.LogDebug($"[MessageReassembler] Removed {bytesToRemove} processed bytes, remaining: {currentDataLength}");
        }
        
        /// <summary>
        /// Resets the parsing state for the next message.
        /// </summary>
        private void ResetParsingState()
        {
            headerParsed = false;
            expectedContentLength = -1;
            headerLength = -1;
        }
        
        /// <summary>
        /// Clears all data from the assembly buffer.
        /// Useful for error recovery or connection reset.
        /// </summary>
        public void Clear()
        {
            if (disposed)
            {
                return;
            }
            
            currentDataLength = 0;
            ResetParsingState();
            McpLogger.LogDebug("[MessageReassembler] Cleared assembly buffer");
        }
        
        /// <summary>
        /// Gets statistics about the reassembler's performance.
        /// </summary>
        /// <returns>Statistics about message reassembly</returns>
        public MessageReassemblerStats GetStats()
        {
            return new MessageReassemblerStats
            {
                TotalMessagesReassembled = totalMessagesReassembled,
                TotalDataChunksProcessed = totalDataChunksProcessed,
                CurrentBufferSize = assemblyBuffer?.Length ?? 0,
                CurrentDataLength = currentDataLength,
                HasIncompleteData = HasIncompleteData,
                IsWaitingForContent = IsWaitingForContent,
                ExpectedContentLength = expectedContentLength,
                HeaderLength = headerLength
            };
        }
        
        /// <summary>
        /// Validates the current state of the reassembler.
        /// </summary>
        /// <returns>True if state is valid, false if cleanup is needed</returns>
        public bool ValidateState()
        {
            if (disposed)
            {
                return false;
            }
            
            // Check for reasonable buffer size - throw exception instead of silently clearing
            if (currentDataLength > BufferConfig.MAX_MESSAGE_SIZE)
            {
                throw new InvalidOperationException($"Buffer size {currentDataLength} exceeds maximum message size {BufferConfig.MAX_MESSAGE_SIZE}. Data corruption detected.");
            }
            
            // Check for stale incomplete data - throw exception for corrupted state
            if (headerParsed && expectedContentLength > 0)
            {
                int expectedTotalLength = headerLength + expectedContentLength;
                if (expectedTotalLength > BufferConfig.MAX_MESSAGE_SIZE)
                {
                    throw new InvalidOperationException($"Expected message size {expectedTotalLength} exceeds maximum {BufferConfig.MAX_MESSAGE_SIZE}. Message framing is corrupted.");
                }
            }
            
            // Validate consistency of parsing state
            if (headerParsed && (expectedContentLength < 0 || headerLength < 0))
            {
                throw new InvalidOperationException($"Inconsistent parsing state: headerParsed={headerParsed}, expectedContentLength={expectedContentLength}, headerLength={headerLength}");
            }
            
            return true;
        }
        
        /// <summary>
        /// Gets a preview of the current buffer content for debugging.
        /// </summary>
        /// <param name="maxLength">Maximum length of preview</param>
        /// <returns>String representation of buffer content</returns>
        public string GetBufferPreview(int maxLength = 100)
        {
            if (disposed || currentDataLength == 0)
            {
                return string.Empty;
            }
            
            // Remove try-catch that hides exceptions - let encoding errors bubble up
            int previewLength = Math.Min(currentDataLength, maxLength);
            string preview = Encoding.UTF8.GetString(assemblyBuffer, 0, previewLength);
            
            if (currentDataLength > maxLength)
            {
                preview += "...";
            }
            
            return preview;
        }
        
        /// <summary>
        /// Releases all resources used by the MessageReassembler.
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                if (assemblyBuffer != null)
                {
                    bufferManager.ReturnBuffer(assemblyBuffer);
                    assemblyBuffer = null;
                }
                
                bufferManager?.Dispose();
                disposed = true;
                
                McpLogger.LogInfo("[MessageReassembler] Disposed");
            }
        }
    }
    
    /// <summary>
    /// Statistics about message reassembler performance and state.
    /// </summary>
    public class MessageReassemblerStats
    {
        /// <summary>
        /// Total number of complete messages reassembled.
        /// </summary>
        public int TotalMessagesReassembled { get; set; }
        
        /// <summary>
        /// Total number of data chunks processed.
        /// </summary>
        public int TotalDataChunksProcessed { get; set; }
        
        /// <summary>
        /// Current size of the assembly buffer.
        /// </summary>
        public int CurrentBufferSize { get; set; }
        
        /// <summary>
        /// Current amount of data in the buffer.
        /// </summary>
        public int CurrentDataLength { get; set; }
        
        /// <summary>
        /// Whether there is incomplete data in the buffer.
        /// </summary>
        public bool HasIncompleteData { get; set; }
        
        /// <summary>
        /// Whether the reassembler is waiting for content after parsing header.
        /// </summary>
        public bool IsWaitingForContent { get; set; }
        
        /// <summary>
        /// Expected content length from parsed header.
        /// </summary>
        public int ExpectedContentLength { get; set; }
        
        /// <summary>
        /// Length of the parsed header.
        /// </summary>
        public int HeaderLength { get; set; }
        
        /// <summary>
        /// Returns a formatted string representation of the statistics.
        /// </summary>
        public override string ToString()
        {
            return $"MessageReassembler Stats: Messages={TotalMessagesReassembled}, " +
                   $"Chunks={TotalDataChunksProcessed}, BufferSize={CurrentDataLength}/{CurrentBufferSize}, " +
                   $"Incomplete={HasIncompleteData}, WaitingForContent={IsWaitingForContent}";
        }
    }
}