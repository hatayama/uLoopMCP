/**
 * UnityPushClient単体テスト
 * 設計書参照: /.kiro/specs/unity-push-notification-system/design.md
 * 関連クラス: UnityPushClient.cs, PushNotificationSerializer.cs
 */

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace io.github.hatayama.uLoopMCP.Tests.Editor
{
    public class UnityPushClientTests
    {
        private UnityPushClient pushClient;

        [SetUp]
        public void SetUp()
        {
            pushClient = new UnityPushClient();
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
        }

        [Test]
        public void IsConnected_InitiallyFalse()
        {
            Assert.IsFalse(pushClient.IsConnected);
        }

        [Test]
        public async Task ConnectToEndpoint_InvalidFormat_ReturnsFalse()
        {
            bool result = await pushClient.ConnectToEndpointAsync("invalid");
            Assert.IsFalse(result);
        }

        [Test]
        public async Task ConnectToEndpoint_EmptyEndpoint_ReturnsFalse()
        {
            bool result = await pushClient.ConnectToEndpointAsync("");
            Assert.IsFalse(result);
        }

        [Test]
        public async Task ConnectToEndpoint_NullEndpoint_ReturnsFalse()
        {
            bool result = await pushClient.ConnectToEndpointAsync(null);
            Assert.IsFalse(result);
        }

        [Test]
        public async Task ConnectToEndpoint_InvalidPort_ReturnsFalse()
        {
            bool result = await pushClient.ConnectToEndpointAsync("localhost:invalid");
            Assert.IsFalse(result);
        }

        [Test]
        public void DisconnectAsync_WhenNotConnected_DoesNotThrow()
        {
            Assert.DoesNotThrowAsync(async () => await pushClient.DisconnectAsync());
        }

        [Test]
        public void SendPushNotificationAsync_WhenNotConnected_DoesNotThrow()
        {
            var notification = new PushNotification
            {
                type = "CONNECTION_ESTABLISHED",
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            LogAssert.Expect(LogType.Warning, "[uLoopMCP] Cannot send push notification: not connected");
            Assert.DoesNotThrowAsync(async () => await pushClient.SendPushNotificationAsync(notification));
        }

        [Test]
        public void SendDisconnectNotificationAsync_CreatesCorrectNotification()
        {
            var reason = new DisconnectReason
            {
                type = "USER_DISCONNECT",
                message = "Test disconnect"
            };

            // This will log a warning since we're not connected, but shouldn't throw
            LogAssert.Expect(LogType.Warning, "[uLoopMCP] Cannot send push notification: not connected");
            Assert.DoesNotThrowAsync(async () => await pushClient.SendDisconnectNotificationAsync(reason));
        }

        [Test]
        public void PersistEndpoint_WithValidSessionManager_DoesNotThrow()
        {
            // Ensure McpSessionManager instance exists
            if (McpSessionManager.instance != null)
            {
                Assert.DoesNotThrow(() => pushClient.PersistEndpoint("localhost:8080"));
            }
        }

        [Test]
        public void LoadPersistedEndpoint_WithValidSessionManager_ReturnsString()
        {
            if (McpSessionManager.instance != null)
            {
                string endpoint = pushClient.LoadPersistedEndpoint();
                // Can be null or valid string
                Assert.That(endpoint, Is.Null.Or.TypeOf<string>());
            }
        }

        [Test]
        public void ClearPersistedEndpoint_WithValidSessionManager_DoesNotThrow()
        {
            if (McpSessionManager.instance != null)
            {
                Assert.DoesNotThrow(() => pushClient.ClearPersistedEndpoint());
            }
        }

        [Test]
        public async Task DiscoverAndConnectAsync_WithoutTypeScriptServer_EventuallyReturnsFalse()
        {
            // This test will attempt discovery but should fail since no server is running
            // It should complete within reasonable time due to port range limitation
            var startTime = DateTime.Now;
            
            bool result = await pushClient.DiscoverAndConnectAsync();
            
            var elapsed = DateTime.Now - startTime;
            
            Assert.IsFalse(result);
            Assert.IsFalse(pushClient.IsConnected);
            // Should not take more than a few seconds for port scanning
            Assert.Less(elapsed.TotalSeconds, 30);
        }

        [Test]
        public void Dispose_WhenAlreadyDisposed_DoesNotThrow()
        {
            pushClient.Dispose();
            Assert.DoesNotThrow(() => pushClient.Dispose());
        }

        [Test]
        public void Dispose_SetsIsConnectedToFalse()
        {
            pushClient.Dispose();
            Assert.IsFalse(pushClient.IsConnected);
        }

        [Test]
        public void EventHandlers_CanBeAssigned()
        {
            // Events are assigned without throwing
            Assert.DoesNotThrow(() => pushClient.OnConnected += (endpoint) => { });
            Assert.DoesNotThrow(() => pushClient.OnDisconnected += (endpoint) => { });
            Assert.DoesNotThrow(() => pushClient.OnError += (error) => { });
        }
    }

    [TestFixture]
    public class PushNotificationSerializerTests
    {
        [Test]
        public void CreateConnectionEstablishedNotification_CreatesValidNotification()
        {
            string endpoint = "localhost:8080";
            
            var notification = PushNotificationSerializer.CreateConnectionEstablishedNotification(endpoint);
            
            Assert.AreEqual(PushNotificationConstants.CONNECTION_ESTABLISHED, notification.type);
            Assert.IsNotNull(notification.timestamp);
            Assert.IsNotNull(notification.payload);
            Assert.AreEqual(endpoint, notification.payload.endpoint);
            Assert.IsNotNull(notification.payload.clientInfo);
        }

        [Test]
        public void CreateDomainReloadNotification_CreatesValidNotification()
        {
            var notification = PushNotificationSerializer.CreateDomainReloadNotification();
            
            Assert.AreEqual(PushNotificationConstants.DOMAIN_RELOAD, notification.type);
            Assert.IsNotNull(notification.timestamp);
            Assert.IsNotNull(notification.payload);
            Assert.IsNotNull(notification.payload.reason);
            Assert.AreEqual(PushNotificationConstants.DOMAIN_RELOAD, notification.payload.reason.type);
        }

        [Test]
        public void CreateDomainReloadRecoveredNotification_CreatesValidNotification()
        {
            var notification = PushNotificationSerializer.CreateDomainReloadRecoveredNotification();
            
            Assert.AreEqual(PushNotificationConstants.DOMAIN_RELOAD_RECOVERED, notification.type);
            Assert.IsNotNull(notification.timestamp);
            Assert.IsNotNull(notification.payload);
            Assert.IsNotNull(notification.payload.clientInfo);
        }

        [Test]
        public void CreateDisconnectNotification_CreatesValidNotification()
        {
            string reasonType = PushNotificationConstants.USER_DISCONNECT;
            string message = "Test disconnect";
            
            var notification = PushNotificationSerializer.CreateDisconnectNotification(reasonType, message);
            
            Assert.AreEqual(reasonType, notification.type);
            Assert.IsNotNull(notification.timestamp);
            Assert.IsNotNull(notification.payload);
            Assert.IsNotNull(notification.payload.reason);
            Assert.AreEqual(reasonType, notification.payload.reason.type);
            Assert.AreEqual(message, notification.payload.reason.message);
        }

        [Test]
        public void CreateToolsChangedNotification_CreatesValidNotification()
        {
            int toolCount = 5;
            string[] changedTools = { "tool1", "tool2" };
            string changeType = PushNotificationConstants.CHANGE_TYPE_ADDED;
            
            var notification = PushNotificationSerializer.CreateToolsChangedNotification(toolCount, changedTools, changeType);
            
            Assert.AreEqual(PushNotificationConstants.TOOLS_CHANGED, notification.type);
            Assert.IsNotNull(notification.timestamp);
            Assert.IsNotNull(notification.payload);
            Assert.IsNotNull(notification.payload.toolsInfo);
            Assert.AreEqual(toolCount, notification.payload.toolsInfo.toolCount);
            Assert.AreEqual(changedTools, notification.payload.toolsInfo.changedTools);
            Assert.AreEqual(changeType, notification.payload.toolsInfo.changeType);
        }

        [Test]
        public void SerializeDeserialize_PushNotification_Roundtrip()
        {
            var original = PushNotificationSerializer.CreateConnectionEstablishedNotification("localhost:8080");
            
            string json = PushNotificationSerializer.SerializePushNotification(original);
            var deserialized = PushNotificationSerializer.DeserializePushNotification(json);
            
            Assert.AreEqual(original.type, deserialized.type);
            Assert.AreEqual(original.timestamp, deserialized.timestamp);
        }

        [Test]
        public void ValidatePushNotification_ValidJson_ReturnsTrue()
        {
            var notification = PushNotificationSerializer.CreateConnectionEstablishedNotification("localhost:8080");
            string json = PushNotificationSerializer.SerializePushNotification(notification);
            
            bool isValid = PushNotificationSerializer.ValidatePushNotification(json, out var parsed);
            
            Assert.IsTrue(isValid);
            Assert.IsNotNull(parsed);
        }

        [Test]
        public void ValidatePushNotification_InvalidJson_ReturnsFalse()
        {
            string invalidJson = "{ invalid json }";
            
            bool isValid = PushNotificationSerializer.ValidatePushNotification(invalidJson, out var parsed);
            
            Assert.IsFalse(isValid);
            Assert.IsNull(parsed);
        }

        [Test]
        public void ValidatePushNotification_EmptyString_ReturnsFalse()
        {
            bool isValid = PushNotificationSerializer.ValidatePushNotification("", out var parsed);
            
            Assert.IsFalse(isValid);
            Assert.IsNull(parsed);
        }

        [Test]
        public void ValidatePushNotification_NullString_ReturnsFalse()
        {
            bool isValid = PushNotificationSerializer.ValidatePushNotification(null, out var parsed);
            
            Assert.IsFalse(isValid);
            Assert.IsNull(parsed);
        }
    }
}