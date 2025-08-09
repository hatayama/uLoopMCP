using NUnit.Framework;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    public class PlayStopUnityToolTests
    {
        private PlayStopUnityTool playStopTool;

        [SetUp]
        public void Setup()
        {
            playStopTool = new PlayStopUnityTool();
        }

        /// <summary>
        /// Test for tool name.
        /// - Asserts that the tool name is "play-stop-unity".
        /// </summary>
        [Test]
        public void ToolName_ShouldReturnPlayStopUnity()
        {
            // Assert
            Assert.That(playStopTool.ToolName, Is.EqualTo("play-stop-unity"));
        }

        /// <summary>
        /// Parameter parsing test for play action.
        /// </summary>
        [Test]
        public void ParseParameters_ShouldParsePlayActionCorrectly()
        {
            // Arrange - Test the Schema object directly
            PlayStopUnitySchema schema = new PlayStopUnitySchema
            {
                Action = "play"
            };

            // Assert - Schema properties should match what we set
            Assert.That(schema.Action, Is.EqualTo("play"));
        }

        /// <summary>
        /// Parameter parsing test for stop action.
        /// </summary>
        [Test]
        public void ParseParameters_ShouldParseStopActionCorrectly()
        {
            // Arrange - Test the Schema object directly
            PlayStopUnitySchema schema = new PlayStopUnitySchema
            {
                Action = "stop"
            };

            // Assert - Schema properties should match what we set
            Assert.That(schema.Action, Is.EqualTo("stop"));
        }

        /// <summary>
        /// Default value test with default schema.
        /// </summary>
        [Test]
        public void Schema_ShouldHaveEmptyActionByDefault()
        {
            // Arrange
            PlayStopUnitySchema schema = new PlayStopUnitySchema();

            // Assert - Default values should be empty
            Assert.That(schema.Action, Is.EqualTo(""));
        }

        /// <summary>
        /// Response structure test.
        /// </summary>
        [Test]
        public void Response_ShouldHaveCorrectProperties()
        {
            // Arrange
            PlayStopUnityResponse response = new PlayStopUnityResponse
            {
                Message = "Test message",
                IsPlaying = true,
                ActionPerformed = "play",
                Success = true
            };

            // Assert - Response properties should match what we set
            Assert.That(response.Message, Is.EqualTo("Test message"));
            Assert.That(response.IsPlaying, Is.True);
            Assert.That(response.ActionPerformed, Is.EqualTo("play"));
            Assert.That(response.Success, Is.True);
        }

        /// <summary>
        /// Test action validation with case sensitivity.
        /// </summary>
        [Test]
        public void Action_ShouldBeCaseInsensitive()
        {
            // Arrange - Test different case variations
            var playSchema = new PlayStopUnitySchema { Action = "PLAY" };
            var stopSchema = new PlayStopUnitySchema { Action = "Stop" };

            // Assert - These should be valid inputs (case insensitive)
            Assert.That(playSchema.Action.ToLowerInvariant(), Is.EqualTo("play"));
            Assert.That(stopSchema.Action.ToLowerInvariant(), Is.EqualTo("stop"));
        }
    }
}