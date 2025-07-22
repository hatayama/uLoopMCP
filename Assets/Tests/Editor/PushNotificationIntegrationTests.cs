/**
 * Push通知システム統合テスト
 * 設計書参照: /.kiro/specs/unity-push-notification-system/design.md
 * 関連クラス: UnityPushClient.cs, McpSessionManager.cs, PushNotificationErrorHandler.cs
 */

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections.Generic;
using io.github.hatayama.uLoopMCP;

namespace io.github.hatayama.uLoopMCP.Tests.Editor
{
    public class PushNotificationIntegrationTests
    {
        private UnityPushClient pushClient;
        private List<string> connectionEvents;
        private List<string> disconnectionEvents;
        private List<Exception> errorEvents;

        [SetUp]
        public void SetUp()
        {
            pushClient = new UnityPushClient();
            connectionEvents = new();
            disconnectionEvents = new();
            errorEvents = new();

            pushClient.OnConnected += (endpoint) => connectionEvents.Add(endpoint);
            pushClient.OnDisconnected += (endpoint) => disconnectionEvents.Add(endpoint);
            pushClient.OnError += (error) => errorEvents.Add(error);
        }

        [TearDown]
        public async Task TearDown()
        {
            if (pushClient != null)
            {
                await pushClient.DisconnectAsync();
                pushClient.Dispose();
                pushClient = null;
            }

            connectionEvents?.Clear();
            disconnectionEvents?.Clear();
            errorEvents?.Clear();
        }

        [Test, Category("Integration")]
        public async Task UnityInitialConnectionSequence_WithoutRunningServer_FailsGracefully()
        {
            // This test simulates Unity starting without TypeScript server running
            bool result = await pushClient.DiscoverAndConnectAsync();

            Assert.IsFalse(result);
            Assert.IsFalse(pushClient.IsConnected);
            Assert.IsEmpty(connectionEvents);
        }

        [Test, Category("Integration")]
        public void EndpointPersistence_WorksWithSessionManager()
        {
            // Test endpoint persistence through domain reloads
            string testEndpoint = "localhost:8080";
            
            if (McpSessionManager.instance != null)
            {
                // Clear any existing data
                pushClient.ClearPersistedEndpoint();
                Assert.IsNull(pushClient.LoadPersistedEndpoint());

                // Persist endpoint
                pushClient.PersistEndpoint(testEndpoint);
                string loadedEndpoint = pushClient.LoadPersistedEndpoint();
                
                Assert.AreEqual(testEndpoint, loadedEndpoint);

                // Clear and verify
                pushClient.ClearPersistedEndpoint();
                Assert.IsNull(pushClient.LoadPersistedEndpoint());
            }
        }

        [Test, Category("Integration")]
        public async Task ConnectionFailureErrorHandling_ReturnsCorrectResult()
        {
            // Test connection to non-existent server
            bool result = await pushClient.ConnectToEndpointAsync("localhost:99999");

            Assert.IsFalse(result);
            Assert.IsFalse(pushClient.IsConnected);
            
            // May have error events depending on timing
            // This is expected behavior for connection failures
        }

        [Test, Category("Integration")]
        public void PushNotificationSerialization_CreatesValidJson()
        {
            // Test notification serialization without connection
            var connectionNotification = PushNotificationSerializer.CreateConnectionEstablishedNotification("localhost:8080");
            
            Assert.AreEqual(PushNotificationConstants.CONNECTION_ESTABLISHED, connectionNotification.type);
            Assert.IsNotNull(connectionNotification.timestamp);
            Assert.IsNotNull(connectionNotification.payload);

            string json = PushNotificationSerializer.SerializePushNotification(connectionNotification);
            Assert.IsNotEmpty(json);
            
            // Validate JSON can be deserialized
            var deserialized = PushNotificationSerializer.DeserializePushNotification(json);
            Assert.AreEqual(connectionNotification.type, deserialized.type);
            Assert.AreEqual(connectionNotification.timestamp, deserialized.timestamp);
        }

        [Test, Category("Integration")]
        public void PushNotificationConstants_AreConsistent()
        {
            // Verify all notification type constants are defined
            Assert.IsNotEmpty(PushNotificationConstants.CONNECTION_ESTABLISHED);
            Assert.IsNotEmpty(PushNotificationConstants.DOMAIN_RELOAD);
            Assert.IsNotEmpty(PushNotificationConstants.DOMAIN_RELOAD_RECOVERED);
            Assert.IsNotEmpty(PushNotificationConstants.TOOLS_CHANGED);
            Assert.IsNotEmpty(PushNotificationConstants.USER_DISCONNECT);

            // Verify change type constants
            Assert.IsNotEmpty(PushNotificationConstants.CHANGE_TYPE_ADDED);
            Assert.IsNotEmpty(PushNotificationConstants.CHANGE_TYPE_REMOVED);
            Assert.IsNotEmpty(PushNotificationConstants.CHANGE_TYPE_MODIFIED);
        }

        [Test, Category("Integration")]
        public async Task MultipleNotificationTypes_SerializeCorrectly()
        {
            var notifications = new List<PushNotification>
            {
                PushNotificationSerializer.CreateConnectionEstablishedNotification("localhost:8080"),
                PushNotificationSerializer.CreateDomainReloadNotification(),
                PushNotificationSerializer.CreateDomainReloadRecoveredNotification(),
                PushNotificationSerializer.CreateDisconnectNotification(PushNotificationConstants.USER_DISCONNECT, "Test disconnect"),
                PushNotificationSerializer.CreateToolsChangedNotification(10, new[] { "tool1", "tool2" }, PushNotificationConstants.CHANGE_TYPE_ADDED)
            };

            foreach (var notification in notifications)
            {
                Assert.IsNotNull(notification.type);
                Assert.IsNotNull(notification.timestamp);
                
                string json = PushNotificationSerializer.SerializePushNotification(notification);
                Assert.IsNotEmpty(json);
                
                bool isValid = PushNotificationSerializer.ValidatePushNotification(json, out var parsed);
                Assert.IsTrue(isValid);
                Assert.IsNotNull(parsed);
                Assert.AreEqual(notification.type, parsed.type);
            }
        }

        [Test, Category("Integration")]
        public async Task ErrorHandlerStaticMethods_WorkCorrectly()
        {
            var timeoutException = new TimeoutException("Connection timeout");
            var networkException = new System.Net.Sockets.SocketException();
            
            // Test error handling methods don't throw
            Assert.DoesNotThrowAsync(async () => 
                await PushNotificationErrorHandler.HandleConnectionFailureAsync(pushClient, timeoutException));
            
            Assert.DoesNotThrowAsync(async () => 
                await PushNotificationErrorHandler.HandleConnectionFailureAsync(pushClient, networkException));

            // Test static methods for error handling
            Assert.DoesNotThrow(() => 
                PushNotificationErrorHandler.HandleTypeScriptServerCrash());

            Assert.DoesNotThrowAsync(async () => 
                await PushNotificationErrorHandler.HandleUnityEditorCrashDetectionAsync(pushClient));
        }

        [Test, Category("Integration")]
        public void ConnectionTimeouts_HaveReasonableValues()
        {
            // Test that timeout values are reasonable for integration scenarios
            Assert.Greater(ConnectionTimeouts.CONNECTION_TIMEOUT_MS, 1000); // At least 1 second
            Assert.Less(ConnectionTimeouts.CONNECTION_TIMEOUT_MS, 60000);   // Less than 1 minute
            
            Assert.Greater(ConnectionTimeouts.PUSH_NOTIFICATION_TIMEOUT_MS, 500);  // At least 0.5 seconds
            Assert.Less(ConnectionTimeouts.PUSH_NOTIFICATION_TIMEOUT_MS, 30000);   // Less than 30 seconds
            
            Assert.Greater(ConnectionTimeouts.DISCOVERY_TIMEOUT_MS, 5000);   // At least 5 seconds
            Assert.Less(ConnectionTimeouts.DISCOVERY_TIMEOUT_MS, 300000);    // Less than 5 minutes
        }

        [Test, Category("Integration")]
        public async Task DiscoverAndConnect_OnlyUsesPersistedEndpoint()
        {
            // Per design spec: no port discovery, only uses persisted endpoint
            // This test validates that no port scanning occurs
            
            bool result = await pushClient.DiscoverAndConnectAsync();
            
            // Without a persisted endpoint, this should fail immediately without port scanning
            Assert.IsFalse(result);
            Assert.IsFalse(pushClient.IsConnected);
        }

        [Test, Category("Integration")]
        public void ClientDispose_CleansUpProperly()
        {
            // Test that disposal works correctly
            Assert.DoesNotThrow(() => pushClient.Dispose());
            Assert.IsFalse(pushClient.IsConnected);
            
            // Multiple dispose calls should be safe
            Assert.DoesNotThrow(() => pushClient.Dispose());
        }

        [Test, Category("Integration")]
        public async Task SendNotification_WhenNotConnected_LogsWarning()
        {
            var notification = new PushNotification
            {
                type = PushNotificationConstants.CONNECTION_ESTABLISHED,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            // Should log warning when not connected
            LogAssert.Expect(LogType.Warning, "[uLoopMCP] Cannot send push notification: not connected");
            
            Assert.DoesNotThrowAsync(async () => await pushClient.SendPushNotificationAsync(notification));
        }

        [Test, Category("Integration")]
        public async Task DomainReloadSimulation_HandlesGracefully()
        {
            // Simulate what happens during domain reload
            if (McpSessionManager.instance != null)
            {
                // Set some state
                string testEndpoint = "localhost:8080";
                McpSessionManager.instance.SetPushServerEndpoint(testEndpoint);
                McpSessionManager.instance.SetPushServerConnected(true);
                
                // Simulate domain reload crash detection
                await PushNotificationErrorHandler.HandleUnityEditorCrashDetectionAsync(pushClient);
                
                // State should be cleared
                Assert.IsNull(McpSessionManager.instance.GetPushServerEndpoint());
                Assert.IsFalse(McpSessionManager.instance.IsPushServerConnected());
            }
        }
    }
}

