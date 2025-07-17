using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Frame-based asynchronous waiting (main thread non-blocking)
    /// UniTask.Delay()-style implementation for Unity Editor
    /// </summary>
    public static class EditorDelay
    {
        /// <summary>
        /// Wait for the specified number of frames
        /// </summary>
        /// <param name="frameCount">Number of frames to wait (default: 1)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Awaitable structure</returns>
        public static DelayFrameAwaitable DelayFrame(int frameCount = 1, CancellationToken cancellationToken = default)
        {
            return new DelayFrameAwaitable(frameCount, cancellationToken);
        }
    }
    
    /// <summary>
    /// Awaitable structure for frame waiting
    /// Can be used with async/await
    /// </summary>
    public struct DelayFrameAwaitable
    {
        private readonly int frameCount;
        private readonly CancellationToken cancellationToken;
        
        public DelayFrameAwaitable(int frameCount, CancellationToken cancellationToken)
        {
            this.frameCount = frameCount;
            this.cancellationToken = cancellationToken;
        }
        
        public Awaiter GetAwaiter() => new(frameCount, cancellationToken);
        
        /// <summary>
        /// Awaiter structure for async/await
        /// </summary>
        public struct Awaiter : INotifyCompletion
        {
            private readonly int frameCount;
            private readonly CancellationToken cancellationToken;
            
            public Awaiter(int frameCount, CancellationToken cancellationToken)
            {
                this.frameCount = frameCount;
                this.cancellationToken = cancellationToken;
            }
            
            /// <summary>
            /// Whether waiting is completed
            /// </summary>
            public bool IsCompleted
            {
                get
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return frameCount <= 0; // Complete immediately if 0 frames or less
                }
            }
            
            /// <summary>
            /// Get result when waiting is completed
            /// </summary>
            public void GetResult()
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            
            /// <summary>
            /// Register continuation processing
            /// Code after await is passed as continuation
            /// </summary>
            public void OnCompleted(Action continuation)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (IsCompleted)
                {
                    continuation();
                    return;
                }
                
                // Register continuation processing to EditorDelayManager
                EditorDelayManager.RegisterDelay(continuation, frameCount, cancellationToken);
            }
        }
    }
}