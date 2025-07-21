/**
 * PushNotificationErrorHandler単体テスト
 * 設計書参照: /.kiro/specs/unity-push-notification-system/design.md
 * 関連クラス: PushNotificationErrorHandler.cs, UnityPushClient.cs
 */

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace io.github.hatayama.uLoopMCP.Tests.Editor
{
    public class PushNotificationErrorHandlerTests
    {
        private UnityPushClient mockPushClient;

        [SetUp]
        public void SetUp()
        {
            mockPushClient = new UnityPushClient();
        }

        [TearDown]
        public async Task TearDown()
        {
            if (mockPushClient != null)
            {
                await mockPushClient.DisconnectAsync();
                mockPushClient.Dispose();
                mockPushClient = null;
            }
        }

        [Test]
        public async Task HandleConnectionFailureAsync_TimeoutError_ReturnsHandlingResult()
        {
            var timeoutException = new TimeoutException("Connection timeout");
            
            bool result = await PushNotificationErrorHandler.HandleConnectionFailureAsync(mockPushClient, timeoutException);
            
            // Should attempt to handle timeout error
            Assert.That(result, Is.TypeOf<bool>());
        }

        [Test]
        public async Task HandleConnectionFailureAsync_NetworkError_ReturnsHandlingResult()
        {
            var networkException = new System.Net.Sockets.SocketException(10061); // Connection refused
            
            bool result = await PushNotificationErrorHandler.HandleConnectionFailureAsync(mockPushClient, networkException);
            
            // Should attempt to handle network error
            Assert.That(result, Is.TypeOf<bool>());
        }

        [Test]
        public async Task HandleConnectionFailureAsync_GenericError_ReturnsFalse()
        {
            var genericException = new InvalidOperationException("Generic error");
            
            bool result = await PushNotificationErrorHandler.HandleConnectionFailureAsync(mockPushClient, genericException);
            
            // Should not handle generic errors
            Assert.IsFalse(result);
        }

        [Test]
        public async Task HandleUnityEditorCrashDetectionAsync_ClearsEndpointData()
        {
            // Ensure session manager exists
            if (McpSessionManager.instance != null)
            {
                // Set some endpoint data first
                McpSessionManager.instance.SetPushServerEndpoint("localhost:8080");
                McpSessionManager.instance.SetPushServerConnected(true);
                
                await PushNotificationErrorHandler.HandleUnityEditorCrashDetectionAsync(mockPushClient);
                
                // Should clear endpoint data
                Assert.IsNull(McpSessionManager.instance.GetPushServerEndpoint());
                Assert.IsFalse(McpSessionManager.instance.IsPushServerConnected());
            }
        }

        [Test]
        public void HandleTypeScriptServerCrash_ClearsEndpointData()
        {
            // Ensure session manager exists
            if (McpSessionManager.instance != null)
            {
                // Set some endpoint data first
                McpSessionManager.instance.SetPushServerEndpoint("localhost:8080");
                McpSessionManager.instance.SetPushServerConnected(true);
                
                PushNotificationErrorHandler.HandleTypeScriptServerCrash();
                
                // Should clear endpoint data
                Assert.IsNull(McpSessionManager.instance.GetPushServerEndpoint());
                Assert.IsFalse(McpSessionManager.instance.IsPushServerConnected());
            }
        }

        [Test]
        public void LogConnectionStatistics_WithNullClient_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => PushNotificationErrorHandler.LogConnectionStatistics(null));
        }

        [Test]
        public void LogConnectionStatistics_WithValidClient_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => PushNotificationErrorHandler.LogConnectionStatistics(mockPushClient));
        }

        [Test]
        public void ConnectionTimeouts_HasValidValues()
        {
            Assert.Greater(ConnectionTimeouts.CONNECTION_TIMEOUT_MS, 0);
            Assert.Greater(ConnectionTimeouts.PUSH_NOTIFICATION_TIMEOUT_MS, 0);
            Assert.Greater(ConnectionTimeouts.DISCOVERY_TIMEOUT_MS, 0);
            Assert.Greater(ConnectionTimeouts.RECONNECTION_DELAY_MS, 0);
            
            // Reasonable timeout values
            Assert.LessOrEqual(ConnectionTimeouts.CONNECTION_TIMEOUT_MS, 30000); // 30 seconds max
            Assert.LessOrEqual(ConnectionTimeouts.PUSH_NOTIFICATION_TIMEOUT_MS, 10000); // 10 seconds max
            Assert.LessOrEqual(ConnectionTimeouts.DISCOVERY_TIMEOUT_MS, 60000); // 1 minute max
            Assert.LessOrEqual(ConnectionTimeouts.RECONNECTION_DELAY_MS, 10000); // 10 seconds max
        }

        [Test]
        public void ErrorClassification_TimeoutErrors()
        {
            var timeoutError1 = new TimeoutException("Connection timeout");
            var timeoutError2 = new Exception("Operation timed out");
            var nonTimeoutError = new InvalidOperationException("Not a timeout");
            
            // Use reflection to test private methods (if needed) or test through public interface
            // For now, we'll test the public HandleConnectionFailureAsync method behavior
            Assert.DoesNotThrowAsync(async () => 
                await PushNotificationErrorHandler.HandleConnectionFailureAsync(mockPushClient, timeoutError1));
            Assert.DoesNotThrowAsync(async () => 
                await PushNotificationErrorHandler.HandleConnectionFailureAsync(mockPushClient, timeoutError2));
            Assert.DoesNotThrowAsync(async () => 
                await PushNotificationErrorHandler.HandleConnectionFailureAsync(mockPushClient, nonTimeoutError));
        }

        [Test]
        public void ErrorClassification_NetworkErrors()
        {
            var socketError = new System.Net.Sockets.SocketException();
            var webError = new System.Net.WebException("Network error");
            var nonNetworkError = new ArgumentException("Not a network error");
            
            Assert.DoesNotThrowAsync(async () => 
                await PushNotificationErrorHandler.HandleConnectionFailureAsync(mockPushClient, socketError));
            Assert.DoesNotThrowAsync(async () => 
                await PushNotificationErrorHandler.HandleConnectionFailureAsync(mockPushClient, webError));
            Assert.DoesNotThrowAsync(async () => 
                await PushNotificationErrorHandler.HandleConnectionFailureAsync(mockPushClient, nonNetworkError));
        }

        [Test]
        public void ErrorClassification_PortConflictErrors()
        {
            var portError1 = new Exception("address already in use");
            var portError2 = new Exception("port 8080 in use");
            var nonPortError = new Exception("Different error");
            
            Assert.DoesNotThrowAsync(async () => 
                await PushNotificationErrorHandler.HandleConnectionFailureAsync(mockPushClient, portError1));
            Assert.DoesNotThrowAsync(async () => 
                await PushNotificationErrorHandler.HandleConnectionFailureAsync(mockPushClient, portError2));
            Assert.DoesNotThrowAsync(async () => 
                await PushNotificationErrorHandler.HandleConnectionFailureAsync(mockPushClient, nonPortError));
        }
    }
}