using NUnit.Framework;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    public class PlayUnityToolTests
    {
        private PlayUnityTool playTool;

        [SetUp]
        public void Setup()
        {
            playTool = new PlayUnityTool();
        }

        /// <summary>
        /// Test for tool name.
        /// - Asserts that the tool name is "play-unity".
        /// </summary>
        [Test]
        public void ToolName_ShouldReturnPlayUnity()
        {
            // Assert
            Assert.That(playTool.ToolName, Is.EqualTo("play-unity"));
        }
    }

    public class StopUnityToolTests
    {
        private StopUnityTool stopTool;

        [SetUp]
        public void Setup()
        {
            stopTool = new StopUnityTool();
        }

        /// <summary>
        /// Test for tool name.
        /// - Asserts that the tool name is "stop-unity".
        /// </summary>
        [Test]
        public void ToolName_ShouldReturnStopUnity()
        {
            // Assert
            Assert.That(stopTool.ToolName, Is.EqualTo("stop-unity"));
        }

        /// <summary>
        /// Response structure test for play tool.
        /// </summary>
        [Test]
        public void PlayResponse_ShouldHaveCorrectProperties()
        {
            // Arrange
            PlayStopUnityResponse response = new PlayStopUnityResponse
            {
                Message = "Unity play mode started",
                IsPlaying = true,
                ActionPerformed = "play",
                Success = true
            };

            // Assert - Response properties should match what we set
            Assert.That(response.Message, Is.EqualTo("Unity play mode started"));
            Assert.That(response.IsPlaying, Is.True);
            Assert.That(response.ActionPerformed, Is.EqualTo("play"));
            Assert.That(response.Success, Is.True);
        }
    }

    public class StopUnityResponseTests
    {
        /// <summary>
        /// Response structure test for stop tool.
        /// </summary>
        [Test]
        public void StopResponse_ShouldHaveCorrectProperties()
        {
            // Arrange
            PlayStopUnityResponse response = new PlayStopUnityResponse
            {
                Message = "Unity play mode stopped",
                IsPlaying = false,
                ActionPerformed = "stop",
                Success = true
            };

            // Assert - Response properties should match what we set
            Assert.That(response.Message, Is.EqualTo("Unity play mode stopped"));
            Assert.That(response.IsPlaying, Is.False);
            Assert.That(response.ActionPerformed, Is.EqualTo("stop"));
            Assert.That(response.Success, Is.True);
        }
    }
}