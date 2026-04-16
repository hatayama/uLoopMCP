using System;
using System.Globalization;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    internal sealed class DynamicCodePrewarmLogSilencer : IDisposable
    {
        private const string PrewarmMessage = "Unity CLI Loop dynamic code prewarm";
        private readonly ILogger _logger;
        private readonly ILogHandler _previousHandler;
        private bool _disposed;

        public DynamicCodePrewarmLogSilencer()
        {
            // Why: the prewarm snippet has to stay as close as possible to the user-visible
            // execute-dynamic-code shape that removed the startup spike in measurements.
            // Why not suppress inside the snippet itself: helper calls and wrapper code changed
            // the snippet shape enough that the first real request became slower again.
            _logger = Debug.unityLogger;
            _previousHandler = _logger.logHandler ?? throw new ArgumentNullException(nameof(_logger.logHandler));
            _logger.logHandler = new FilteringLogHandler(_previousHandler);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _logger.logHandler = _previousHandler;
            _disposed = true;
        }

        private sealed class FilteringLogHandler : ILogHandler
        {
            private readonly ILogHandler _innerHandler;

            public FilteringLogHandler(ILogHandler innerHandler)
            {
                _innerHandler = innerHandler ?? throw new ArgumentNullException(nameof(innerHandler));
            }

            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
                if (ShouldSuppress(logType, format, args))
                {
                    return;
                }

                _innerHandler.LogFormat(logType, context, format, args);
            }

            public void LogException(Exception exception, UnityEngine.Object context)
            {
                _innerHandler.LogException(exception, context);
            }

            private static bool ShouldSuppress(LogType logType, string format, object[] args)
            {
                if (logType != LogType.Log)
                {
                    return false;
                }

                string renderedMessage = RenderMessage(format, args);
                return string.Equals(renderedMessage, PrewarmMessage, StringComparison.Ordinal);
            }

            private static string RenderMessage(string format, object[] args)
            {
                if (string.IsNullOrEmpty(format))
                {
                    return string.Empty;
                }

                if (args == null || args.Length == 0)
                {
                    return format;
                }

                return string.Format(CultureInfo.InvariantCulture, format, args);
            }
        }
    }
}
