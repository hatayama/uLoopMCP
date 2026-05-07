using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace io.github.hatayama.UnityCliLoop
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

    // Port for dispatching continuations onto Unity's main thread.
    public interface IMainThreadDispatcher
    {
        int MainThreadId { get; }
        bool IsMainThread { get; }
        void Initialize();
        void AddContinuation(Action continuation);
    }

    /// <summary>
    /// A class that provides functionality equivalent to UniTask's SwitchToMainThread.
    /// Handles switching to the main thread through the registered dispatcher.
    /// Reference: https://github.com/Cysharp/UniTask - PlayerLoopHelper implementation
    /// </summary>
    public static class MainThreadSwitcher
    {
        private static IMainThreadDispatcher ServiceValue;

        /// <summary>
        /// Gets the ID of the main thread.
        /// </summary>
        public static int MainThreadId => Service.MainThreadId;

        /// <summary>
        /// Determines whether the current thread is the main thread.
        /// </summary>
        public static bool IsMainThread => Service.IsMainThread;

        internal static void RegisterService(IMainThreadDispatcher service)
        {
            Debug.Assert(service != null, "service must not be null");

            ServiceValue = service ?? throw new ArgumentNullException(nameof(service));
        }

        internal static void InitializeForEditorStartup()
        {
            Service.Initialize();
        }

        /// <summary>
        /// Add a continuation to the queue to be executed on the main thread.
        /// </summary>
        internal static void AddContinuation(Action continuation)
        {
            Service.AddContinuation(continuation);
        }

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

        private static IMainThreadDispatcher Service
        {
            get
            {
                if (ServiceValue == null)
                {
                    throw new InvalidOperationException("Unity CLI Loop main-thread dispatcher is not registered.");
                }

                return ServiceValue;
            }
        }
    }

    /// <summary>
    /// An awaitable for switching to the main thread through MainThreadSwitcher.
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

                MainThreadSwitcher.AddContinuation(continuation);
            }
        }
    }

}
