using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    /// <summary>
    /// Test for DynamicCodeSecurityLevel enum
    /// Verify values and default values for each level
    /// </summary>
    [TestFixture]
    public class DynamicCodeSecurityLevelTests
    {
        [Test]
        public void Verify_Correct_Level_Values()
        {
            // Assert
            Assert.AreEqual(0, (int)DynamicCodeSecurityLevel.Disabled);
            Assert.AreEqual(1, (int)DynamicCodeSecurityLevel.Restricted);
            Assert.AreEqual(2, (int)DynamicCodeSecurityLevel.FullAccess);
        }

        [Test]
        public void Verify_Default_Value_Is_Disabled()
        {
            // Arrange & Act
            DynamicCodeSecurityLevel defaultLevel = default(DynamicCodeSecurityLevel);
            
            // Assert
            Assert.AreEqual(DynamicCodeSecurityLevel.Disabled, defaultLevel); // enum default is 0
        }

        [Test]
        public void Verify_All_Levels_Are_Defined()
        {
            // Arrange
            string[] expectedNames = { "Disabled", "Restricted", "FullAccess" };
            
            // Act
            string[] actualNames = System.Enum.GetNames(typeof(DynamicCodeSecurityLevel));
            
            // Assert
            Assert.AreEqual(expectedNames.Length, actualNames.Length);
            foreach (string name in expectedNames)
            {
                Assert.Contains(name, actualNames);
            }
        }
    }
}