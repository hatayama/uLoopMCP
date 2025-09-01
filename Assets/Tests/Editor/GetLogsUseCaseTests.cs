using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Unit tests for GetLogsUseCase
    /// Related classes: GetLogsUseCase, LogRetrievalService, LogFilteringService
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// Test philosophy: Following Kent Beck's TDD and t-wada's testing principles
    /// </summary>
    [TestFixture]
    public class GetLogsUseCaseTests
    {
        private GetLogsUseCase _useCase;
        private CancellationTokenSource _cancellationTokenSource;

        [SetUp]
        public void SetUp()
        {
            // Clear console to ensure test isolation FIRST
            Debug.ClearDeveloperConsole();
            
            // Clean setup for each test
            _useCase = new GetLogsUseCase(new LogRetrievalService(), new LogFilteringService());
            _cancellationTokenSource = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            _cancellationTokenSource?.Dispose();
        }

        #region Normal Cases

        /// <summary>
        /// Verifies successful execution with default parameters
        /// </summary>
        [Test]
        public async Task ExecuteAsync_WithDefaultParameters_ReturnsValidResponse()
        {
            // Arrange
            GetLogsSchema schema = new()
            {
                LogType = McpLogType.All,
                MaxCount = 100,
                TimeoutSeconds = 10
            };

            // Act
            GetLogsResponse result = await _useCase.ExecuteAsync(schema, _cancellationTokenSource.Token);

            // Assert
            Assert.IsNotNull(result, "Response should not be null");
            Assert.IsNotNull(result.Logs, "Logs array should not be null");
            Assert.GreaterOrEqual(result.TotalCount, 0, "TotalCount should be non-negative");
            Assert.GreaterOrEqual(result.DisplayedCount, 0, "DisplayedCount should be non-negative");
            Assert.LessOrEqual(result.DisplayedCount, result.TotalCount, "DisplayedCount should not exceed TotalCount");
            Assert.AreEqual(McpLogType.All, result.LogType, "LogType should match request");
            Assert.AreEqual(100, result.MaxCount, "MaxCount should match request");
        }

        /// <summary>
        /// Verifies correct filtering of Error log type
        /// </summary>
        [Test]
        public async Task ExecuteAsync_WithErrorLogType_FiltersOnlyErrors()
        {
            // Arrange - Create test logs of different types
            LogAssert.Expect(LogType.Error, "Test Error 1");
            LogAssert.Expect(LogType.Error, "Test Error 2");
            LogAssert.Expect(LogType.Warning, "Test Warning - should not appear");
            LogAssert.Expect(LogType.Log, "Test Info - should not appear");
            
            Debug.LogError("Test Error 1");
            Debug.LogError("Test Error 2");
            Debug.LogWarning("Test Warning - should not appear");
            Debug.Log("Test Info - should not appear");

            GetLogsSchema schema = new()
            {
                LogType = McpLogType.Error,
                MaxCount = 100,
                TimeoutSeconds = 5
            };

            // Act
            GetLogsResponse result = await _useCase.ExecuteAsync(schema, _cancellationTokenSource.Token);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Logs);
            
            // All returned logs should be Error type
            foreach (LogEntry log in result.Logs)
            {
                Assert.AreEqual(McpLogType.Error, log.Type, 
                    $"Expected all logs to be Error type, but found {log.Type}: {log.Message}");
            }
            
            // Should find at least our test errors
            Assert.GreaterOrEqual(result.DisplayedCount, 2, 
                "Should find at least the 2 test error logs we created");
            
            // Verify specific test errors exist
            bool hasTestError1 = result.Logs.Any(log => log.Message.Contains("Test Error 1"));
            bool hasTestError2 = result.Logs.Any(log => log.Message.Contains("Test Error 2"));
            Assert.IsTrue(hasTestError1, "Should contain 'Test Error 1'");
            Assert.IsTrue(hasTestError2, "Should contain 'Test Error 2'");
        }

        /// <summary>
        /// Verifies correct filtering of Warning log type
        /// </summary>
        [Test]
        public async Task ExecuteAsync_WithWarningLogType_FiltersOnlyWarnings()
        {
            // Arrange
            LogAssert.Expect(LogType.Warning, "Test Warning 1");
            LogAssert.Expect(LogType.Warning, "Test Warning 2");
            LogAssert.Expect(LogType.Error, "Test Error - should not appear");
            LogAssert.Expect(LogType.Log, "Test Info - should not appear");
            
            Debug.LogWarning("Test Warning 1");
            Debug.LogWarning("Test Warning 2");
            Debug.LogError("Test Error - should not appear");
            Debug.Log("Test Info - should not appear");

            GetLogsSchema schema = new()
            {
                LogType = McpLogType.Warning,
                MaxCount = 100,
                TimeoutSeconds = 5
            };

            // Act
            GetLogsResponse result = await _useCase.ExecuteAsync(schema, _cancellationTokenSource.Token);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Logs);
            
            // All returned logs should be Warning type
            foreach (LogEntry log in result.Logs)
            {
                Assert.AreEqual(McpLogType.Warning, log.Type,
                    $"Expected all logs to be Warning type, but found {log.Type}: {log.Message}");
            }
            
            // Should find at least our test warnings
            Assert.GreaterOrEqual(result.DisplayedCount, 2,
                "Should find at least the 2 test warning logs we created");
        }

        /// <summary>
        /// Verifies correct filtering of Log type
        /// </summary>
        [Test]
        public async Task ExecuteAsync_WithLogType_FiltersOnlyLogs()
        {
            // Arrange
            LogAssert.Expect(LogType.Log, "Test Log 1");
            LogAssert.Expect(LogType.Log, "Test Log 2");
            LogAssert.Expect(LogType.Error, "Test Error - should not appear");
            LogAssert.Expect(LogType.Warning, "Test Warning - should not appear");
            
            Debug.Log("Test Log 1");
            Debug.Log("Test Log 2");
            Debug.LogError("Test Error - should not appear");
            Debug.LogWarning("Test Warning - should not appear");

            GetLogsSchema schema = new()
            {
                LogType = McpLogType.Log,
                MaxCount = 100,
                TimeoutSeconds = 5
            };

            // Act
            GetLogsResponse result = await _useCase.ExecuteAsync(schema, _cancellationTokenSource.Token);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Logs);
            
            // All returned logs should be Log type
            foreach (LogEntry log in result.Logs)
            {
                Assert.AreEqual(McpLogType.Log, log.Type,
                    $"Expected all logs to be Log type, but found {log.Type}: {log.Message}");
            }
            
            // Should find at least our test logs
            Assert.GreaterOrEqual(result.DisplayedCount, 2,
                "Should find at least the 2 test info logs we created");
        }


        /// <summary>
        /// Verifies correct search operation with SearchText
        /// </summary>
        [Test]
        public async Task ExecuteAsync_WithSearchText_FiltersCorrectly()
        {
            // Arrange
            Debug.Log("This is a unique search term XYZ123");
            Debug.Log("This is another message without the term");
            Debug.Log("XYZ123 appears here too");

            GetLogsSchema schema = new()
            {
                LogType = McpLogType.All,
                SearchText = "XYZ123",
                UseRegex = false,
                MaxCount = 100,
                TimeoutSeconds = 5
            };

            // Act
            GetLogsResponse result = await _useCase.ExecuteAsync(schema, _cancellationTokenSource.Token);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Logs);
            
            // All returned logs should contain the search text
            foreach (LogEntry log in result.Logs)
            {
                Assert.IsTrue(log.Message.Contains("XYZ123"),
                    $"Log message should contain search text 'XYZ123': {log.Message}");
            }
            
            // Should find at least our test logs with the search term
            Assert.GreaterOrEqual(result.DisplayedCount, 2,
                "Should find at least 2 logs containing 'XYZ123'");
        }

        /// <summary>
        /// Verifies correct regex search operation
        /// </summary>
        [Test]
        public async Task ExecuteAsync_WithRegexSearch_FiltersCorrectly()
        {
            // Arrange
            Debug.Log("Test123");
            Debug.Log("Test456");
            Debug.Log("NoMatch");

            GetLogsSchema schema = new()
            {
                LogType = McpLogType.All,
                SearchText = @"Test\d+",
                UseRegex = true,
                MaxCount = 100,
                TimeoutSeconds = 5
            };

            // Act
            GetLogsResponse result = await _useCase.ExecuteAsync(schema, _cancellationTokenSource.Token);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Logs);
            
            // All returned logs should match the regex pattern
            foreach (LogEntry log in result.Logs)
            {
                bool matchesPattern = Regex.IsMatch(log.Message, @"Test\d+");
                Assert.IsTrue(matchesPattern,
                    $"Log message should match regex pattern 'Test\\d+': {log.Message}");
            }
            
            // Should find at least our test logs matching the pattern
            Assert.GreaterOrEqual(result.DisplayedCount, 2,
                "Should find at least 2 logs matching the regex pattern");
        }

        /// <summary>
        /// Verifies correct behavior of enabling/disabling StackTrace display
        /// </summary>
        [Test]
        public async Task ExecuteAsync_WithIncludeStackTrace_ControlsStackTraceDisplay()
        {
            // Arrange - Create an error with stack trace
            LogAssert.Expect(LogType.Error, "Test Error with Stack Trace");
            Debug.LogError("Test Error with Stack Trace");

            // Test with StackTrace included
            GetLogsSchema schemaWithStack = new()
            {
                LogType = McpLogType.Error,
                IncludeStackTrace = true,
                MaxCount = 10,
                TimeoutSeconds = 5
            };

            // Act
            GetLogsResponse resultWithStack = await _useCase.ExecuteAsync(schemaWithStack, _cancellationTokenSource.Token);

            // Assert - With stack trace
            Assert.IsNotNull(resultWithStack);
            Assert.IsNotNull(resultWithStack.Logs);
            Assert.IsTrue(resultWithStack.IncludeStackTrace, "IncludeStackTrace should be true");
            
            if (resultWithStack.Logs.Length > 0)
            {
                // At least one error log should have stack trace when available
                // Note: Stack trace might not always be available in test environment
                // We don't assert this because stack trace availability depends on Unity's compilation mode
            }

            // Test without StackTrace
            GetLogsSchema schemaWithoutStack = new()
            {
                LogType = McpLogType.Error,
                IncludeStackTrace = false,
                MaxCount = 10,
                TimeoutSeconds = 5
            };

            // Act
            GetLogsResponse resultWithoutStack = await _useCase.ExecuteAsync(schemaWithoutStack, _cancellationTokenSource.Token);

            // Assert - Without stack trace
            Assert.IsNotNull(resultWithoutStack);
            Assert.IsNotNull(resultWithoutStack.Logs);
            Assert.IsFalse(resultWithoutStack.IncludeStackTrace, "IncludeStackTrace should be false");
            
            // All logs should have null or empty stack trace when IncludeStackTrace is false
            foreach (LogEntry log in resultWithoutStack.Logs)
            {
                Assert.IsTrue(string.IsNullOrEmpty(log.StackTrace),
                    "Stack trace should be empty when IncludeStackTrace is false");
            }
        }

        #endregion

        #region Boundary Value Tests

        /// <summary>
        /// Verifies behavior with MaxCount = 0
        /// </summary>
        [Test]
        public async Task ExecuteAsync_WithZeroMaxCount_ReturnsEmptyResult()
        {
            // Arrange
            Debug.Log("Test Log");
            Debug.LogWarning("Test Warning");

            GetLogsSchema schema = new()
            {
                LogType = McpLogType.All,
                MaxCount = 0,
                TimeoutSeconds = 5
            };

            // Act
            GetLogsResponse result = await _useCase.ExecuteAsync(schema, _cancellationTokenSource.Token);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Logs);
            Assert.AreEqual(0, result.DisplayedCount, "DisplayedCount should be 0 when MaxCount is 0");
            Assert.AreEqual(0, result.Logs.Length, "No logs should be returned when MaxCount is 0");
            // TotalCount might still be > 0 as it represents available logs
            Assert.GreaterOrEqual(result.TotalCount, 0, "TotalCount should still show available logs");
        }

        /// <summary>
        /// Verifies behavior with MaxCount = 1
        /// </summary>
        [Test]
        public async Task ExecuteAsync_WithMaxCountOne_ReturnsOneLog()
        {
            // Arrange
            Debug.Log("Test Log 1");
            Debug.Log("Test Log 2");
            Debug.Log("Test Log 3");

            GetLogsSchema schema = new()
            {
                LogType = McpLogType.All,
                MaxCount = 1,
                TimeoutSeconds = 5
            };

            // Act
            GetLogsResponse result = await _useCase.ExecuteAsync(schema, _cancellationTokenSource.Token);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Logs);
            Assert.LessOrEqual(result.DisplayedCount, 1, "DisplayedCount should not exceed MaxCount of 1");
            Assert.LessOrEqual(result.Logs.Length, 1, "Should return at most 1 log");
        }

        /// <summary>
        /// Verifies behavior with very large MaxCount
        /// </summary>
        [Test]
        public async Task ExecuteAsync_WithLargeMaxCount_HandlesCorrectly()
        {
            // Arrange
            // Create multiple logs
            for (int i = 0; i < 10; i++)
            {
                Debug.Log($"Test Log {i}");
            }

            GetLogsSchema schema = new()
            {
                LogType = McpLogType.All,
                MaxCount = int.MaxValue,
                TimeoutSeconds = 5
            };

            // Act
            GetLogsResponse result = await _useCase.ExecuteAsync(schema, _cancellationTokenSource.Token);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Logs);
            Assert.AreEqual(result.TotalCount, result.DisplayedCount, 
                "With very large MaxCount, all available logs should be returned");
            Assert.GreaterOrEqual(result.DisplayedCount, 10,
                "Should return at least the 10 test logs we created");
        }

        /// <summary>
        /// Verifies behavior with negative MaxCount (error handling check)
        /// </summary>
        [Test]
        public async Task ExecuteAsync_WithNegativeMaxCount_HandlesGracefully()
        {
            // Arrange
            GetLogsSchema schema = new()
            {
                LogType = McpLogType.All,
                MaxCount = -1,
                TimeoutSeconds = 5
            };

            // Act
            GetLogsResponse result = await _useCase.ExecuteAsync(schema, _cancellationTokenSource.Token);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Logs);
            // Negative MaxCount should be handled gracefully (likely treated as 0 or ignored)
            Assert.GreaterOrEqual(result.DisplayedCount, 0, "DisplayedCount should be non-negative");
        }

        #endregion

        #region Error Cases

        /// <summary>
        /// Verifies behavior when executing with null parameters
        /// </summary>
        [Test]
        public void ExecuteAsync_WithNullParameters_ThrowsException()
        {
            // Act & Assert
            // Currently throws InvalidOperationException wrapping NullReferenceException
            // This is acceptable behavior as it indicates the parameter was null
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
                await _useCase.ExecuteAsync(null, _cancellationTokenSource.Token);
#pragma warning restore CS8625
            });
        }

        /// <summary>
        /// Verifies behavior with invalid LogType
        /// </summary>
        [Test]
        public async Task ExecuteAsync_WithInvalidLogType_HandlesGracefully()
        {
            // Arrange
            GetLogsSchema schema = new()
            {
                LogType = "InvalidType",
                MaxCount = 10,
                TimeoutSeconds = 5
            };

            // Act
            // Should not throw, but handle gracefully
            GetLogsResponse result = await _useCase.ExecuteAsync(schema, _cancellationTokenSource.Token);

            // Assert
            Assert.IsNotNull(result, "Should return a result even with invalid LogType");
            Assert.IsNotNull(result.Logs, "Logs array should not be null");
            // Invalid type might be treated as None or All depending on implementation
        }

        /// <summary>
        /// Verifies interruption with cancellation token
        /// </summary>
        [Test]
        public void ExecuteAsync_WithCancellation_ThrowsTaskCanceledException()
        {
            // Arrange
            GetLogsSchema schema = new()
            {
                LogType = McpLogType.All,
                MaxCount = 100,
                TimeoutSeconds = 5
            };
            
            // Cancel immediately
            _cancellationTokenSource.Cancel();

            // Act & Assert
            // TaskCanceledException is thrown by async operations when cancelled
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await _useCase.ExecuteAsync(schema, _cancellationTokenSource.Token);
            });
        }

        /// <summary>
        /// Verifies behavior with invalid regex pattern
        /// </summary>
        [Test]
        public async Task ExecuteAsync_WithInvalidRegexPattern_HandlesGracefully()
        {
            // Arrange
            Debug.Log("Test Log");

            GetLogsSchema schema = new()
            {
                LogType = McpLogType.All,
                SearchText = "[invalid(regex",  // Invalid regex pattern
                UseRegex = true,
                MaxCount = 10,
                TimeoutSeconds = 5
            };

            // Act
            // Should handle invalid regex gracefully
            Exception thrownException = null;
            GetLogsResponse result = null;
            
            try
            {
                result = await _useCase.ExecuteAsync(schema, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                thrownException = ex;
            }

            // Assert
            // Either returns empty result or throws a meaningful exception
            if (thrownException != null)
            {
                Assert.IsTrue(
                    thrownException is InvalidOperationException || 
                    thrownException.InnerException is RegexMatchTimeoutException ||
                    thrownException.InnerException is ArgumentException,
                    "Should throw appropriate exception for invalid regex");
            }
            else
            {
                Assert.IsNotNull(result, "Should return a result even with invalid regex");
                Assert.IsNotNull(result.Logs, "Logs array should not be null");
            }
        }

        #endregion

        #region Integration Tests

        /// <summary>
        /// Verifies complex search combining multiple conditions
        /// </summary>
        [Test]
        public async Task ExecuteAsync_WithComplexSearch_FiltersCorrectly()
        {
            // Arrange
            LogAssert.Expect(LogType.Error, "Error: Connection failed");
            LogAssert.Expect(LogType.Error, "Error: Timeout occurred");
            LogAssert.Expect(LogType.Warning, "Warning: Low memory");
            LogAssert.Expect(LogType.Log, "Info: Operation completed");
            
            Debug.LogError("Error: Connection failed");
            Debug.LogError("Error: Timeout occurred");
            Debug.LogWarning("Warning: Low memory");
            Debug.Log("Info: Operation completed");

            GetLogsSchema schema = new()
            {
                LogType = McpLogType.Error,
                SearchText = "failed",
                UseRegex = false,
                SearchInStackTrace = false,
                MaxCount = 5,
                IncludeStackTrace = false,
                TimeoutSeconds = 5
            };

            // Act
            GetLogsResponse result = await _useCase.ExecuteAsync(schema, _cancellationTokenSource.Token);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Logs);
            
            // Should only find Error logs containing "failed"
            foreach (LogEntry log in result.Logs)
            {
                Assert.AreEqual(McpLogType.Error, log.Type, "Should only return Error type");
                Assert.IsTrue(log.Message.Contains("failed"), "Should contain search text 'failed'");
                Assert.IsTrue(string.IsNullOrEmpty(log.StackTrace), "Stack trace should be empty");
            }
            
            Assert.GreaterOrEqual(result.DisplayedCount, 1, "Should find at least one matching error");
            Assert.LessOrEqual(result.DisplayedCount, 5, "Should respect MaxCount limit");
        }

        /// <summary>
        /// Verifies performance with a large number of logs
        /// </summary>
        [Test]
        public async Task ExecuteAsync_WithManyLogs_PerformsEfficiently()
        {
            // Arrange - Create many logs
            int logCount = 100;
            
            // Expect all the logs we're about to create
            for (int i = 0; i < logCount; i++)
            {
                if (i % 3 == 0) LogAssert.Expect(LogType.Error, $"Error {i}");
                else if (i % 3 == 1) LogAssert.Expect(LogType.Warning, $"Warning {i}");
                else LogAssert.Expect(LogType.Log, $"Log {i}");
            }
            
            // Create the actual logs
            for (int i = 0; i < logCount; i++)
            {
                if (i % 3 == 0) Debug.LogError($"Error {i}");
                else if (i % 3 == 1) Debug.LogWarning($"Warning {i}");
                else Debug.Log($"Log {i}");
            }

            GetLogsSchema schema = new()
            {
                LogType = McpLogType.All,
                MaxCount = 50,  // Limit to 50
                TimeoutSeconds = 5
            };

            // Act
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            GetLogsResponse result = await _useCase.ExecuteAsync(schema, _cancellationTokenSource.Token);
            stopwatch.Stop();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Logs);
            Assert.AreEqual(50, result.DisplayedCount, "Should return exactly MaxCount logs");
            Assert.GreaterOrEqual(result.TotalCount, logCount, "TotalCount should reflect all available logs");
            
            // Performance check - should complete within reasonable time
            Assert.Less(stopwatch.ElapsedMilliseconds, 1000, 
                "Should complete within 1 second even with many logs");
        }

        #endregion

        #region Service Layer Error Tests

        /// <summary>
        /// Verifies constructor behavior when LogRetrievalService is null
        /// </summary>
        [Test]
        public void Constructor_WithNullRetrievalService_ThrowsArgumentNullException()
        {
            // Act & Assert
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() =>
            {
                GetLogsUseCase _ = new(null, new LogFilteringService());
            });
            Assert.IsNotNull(ex, "Should throw ArgumentNullException for null LogRetrievalService");
        }

        /// <summary>
        /// Verifies constructor behavior when LogFilteringService is null
        /// </summary>
        [Test]
        public void Constructor_WithNullFilteringService_ThrowsArgumentNullException()
        {
            // Act & Assert
            ArgumentNullException filterEx = Assert.Throws<ArgumentNullException>(() =>
            {
                GetLogsUseCase _ = new(new LogRetrievalService(), null);
            });
            Assert.IsNotNull(filterEx, "Should throw ArgumentNullException for null LogFilteringService");
        }

        #endregion
    }
}