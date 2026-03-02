using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Dispatches actions from background threads to Unity's main thread via Update loop.
    /// Attached to a DontDestroyOnLoad GameObject by DeviceAgentBootstrap.
    /// </summary>
    public sealed class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher _instance;
        private static int _mainThreadId;

        private readonly ConcurrentQueue<Action> _queue = new();

        public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        public static MainThreadDispatcher Instance => _instance;

        internal static void Initialize(MainThreadDispatcher instance)
        {
            Debug.Assert(instance != null, "instance must not be null");
            _instance = instance;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public void Enqueue(Action action)
        {
            Debug.Assert(action != null, "action must not be null");
            _queue.Enqueue(action);
        }

        private void Update()
        {
            while (_queue.TryDequeue(out Action action))
            {
                action.Invoke();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
