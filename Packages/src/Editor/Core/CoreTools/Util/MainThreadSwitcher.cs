using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Enum for PlayerLoopTiming (dummy implementation in Editor version).
    /// </summary>
    public enum PlayerLoopTiming
    {
        Initialization = 0,
        EarlyUpdate = 1,
        FixedUpdate = 2,
        PreUpdate = 3,
        Update = 4,
        PreLateUpdate = 5,
        PostLateUpdate = 6
    }
    /// <summary>
    /// A class that provides functionality equivalent to UniTask's SwitchToMainThread.
    /// Handles switching to the main thread.
    /// </summary>
    public static class MainThreadSwitcher
    {
        private static int _mainThreadId;
        private static SynchronizationContext _unitySynchronizationContext;
        
        /// <summary>
        /// Gets the ID of the main thread.
        /// </summary>
        public static int MainThreadId => _mainThreadId;
        
        /// <summary>
        /// Determines whether the current thread is the main thread.
        /// </summary>
        public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        /// <summary>
        /// Gets the UnitySynchronizationContext.
        /// </summary>
        public static SynchronizationContext UnitySynchronizationContext => _unitySynchronizationContext;

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            // Record the main thread ID and SynchronizationContext.
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _unitySynchronizationContext = SynchronizationContext.Current;
        }

        /// <summary>
        /// Switches to the main thread (SynchronizationContext version).
        /// </summary>
        public static SwitchToMainThreadAwaitable SwitchToMainThread()
        {
            return new SwitchToMainThreadAwaitable(CancellationToken.None);
        }
        
        /// <summary>
        /// Switches to the main thread (with CancellationToken).
        /// </summary>
        public static SwitchToMainThreadAwaitable SwitchToMainThread(CancellationToken cancellationToken)
        {
            return new SwitchToMainThreadAwaitable(cancellationToken);
        }
        
        /// <summary>
        /// Switches to the main thread (with PlayerLoopTiming specified).
        /// PlayerLoopTiming is ignored in the Editor version.
        /// </summary>
        public static SwitchToMainThreadAwaitable SwitchToMainThread(PlayerLoopTiming timing)
        {
            return new SwitchToMainThreadAwaitable(CancellationToken.None);
        }
        
        /// <summary>
        /// Switches to the main thread (with PlayerLoopTiming and CancellationToken specified).
        /// PlayerLoopTiming is ignored in the Editor version.
        /// </summary>
        public static SwitchToMainThreadAwaitable SwitchToMainThread(PlayerLoopTiming timing, CancellationToken cancellationToken)
        {
            return new SwitchToMainThreadAwaitable(cancellationToken);
        }

        /// <summary>
        /// Switches to the main thread (EditorApplication.delayCall version).
        /// </summary>
        public static SwitchToMainThreadDelayCallAwaitable SwitchToMainThreadDelayCall()
        {
            return new SwitchToMainThreadDelayCallAwaitable();
        }
    }

    /// <summary>
    /// An awaitable for switching to the main thread using SynchronizationContext.
    /// </summary>
    public struct SwitchToMainThreadAwaitable
    {
        private readonly CancellationToken cancellationToken;
        
        public SwitchToMainThreadAwaitable(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
        }
        
        public Awaiter GetAwaiter() => new(cancellationToken);

        public struct Awaiter : INotifyCompletion
        {
            private readonly CancellationToken cancellationToken;
            
            public Awaiter(CancellationToken cancellationToken)
            {
                this.cancellationToken = cancellationToken;
            }
            
            public bool IsCompleted
            {
                get
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return MainThreadSwitcher.IsMainThread;
                }
            }

            public void GetResult()
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            public void OnCompleted(Action continuation)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (MainThreadSwitcher.IsMainThread)
                {
                    continuation();
                    return;
                }

                // Prioritize using the saved UnitySynchronizationContext.
                if (MainThreadSwitcher.UnitySynchronizationContext != null)
                {
                    MainThreadSwitcher.UnitySynchronizationContext.Post(_ => continuation(), null);
                }
                else
                {
                    // Fallback: Use EditorApplication.delayCall.
                    EditorApplication.delayCall += () => continuation();
                }
            }
        }
    }

    /// <summary>
    /// An awaitable for switching to the main thread using EditorApplication.delayCall.
    /// </summary>
    public struct SwitchToMainThreadDelayCallAwaitable
    {
        public Awaiter GetAwaiter() => new();

        public struct Awaiter : INotifyCompletion
        {
            public bool IsCompleted => MainThreadSwitcher.IsMainThread;

            public void GetResult() { }

            public void OnCompleted(Action continuation)
            {
                if (MainThreadSwitcher.IsMainThread)
                {
                    continuation();
                    return;
                }

                // Execute on the main thread using EditorApplication.delayCall.
                EditorApplication.delayCall += () => continuation();
            }
        }
    }

} 