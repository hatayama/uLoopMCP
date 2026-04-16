using System;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    internal sealed class DynamicCodePrewarmLogSilencer : IDisposable
    {
        private readonly ILogger _logger;
        private readonly bool _previousLogEnabled;
        private bool _disposed;

        public DynamicCodePrewarmLogSilencer()
        {
            // Why: the prewarm snippet has to stay as close as possible to the user-visible
            // execute-dynamic-code shape that removed the startup spike in measurements.
            // Why not suppress inside the snippet itself: helper calls and wrapper code changed
            // the snippet shape enough that the first real request became slower again.
            _logger = Debug.unityLogger;
            _previousLogEnabled = _logger.logEnabled;
            _logger.logEnabled = false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _logger.logEnabled = _previousLogEnabled;
            _disposed = true;
        }
    }
}
