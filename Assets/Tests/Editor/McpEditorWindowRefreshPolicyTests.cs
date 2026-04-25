using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP
{
    public class McpEditorWindowRefreshPolicyTests
    {
        [Test]
        public void ShouldRefreshOnEditorUpdate_WhenRepaintIsRequested_ReturnsTrue()
        {
            RuntimeState runtimeState = new RuntimeState(needsRepaint: true);

            bool shouldRefresh = McpEditorWindowRefreshPolicy.ShouldRefreshOnEditorUpdate(runtimeState);

            Assert.That(shouldRefresh, Is.True);
        }

        [Test]
        public void ShouldRefreshOnEditorUpdate_WhenPostCompileModeHasNoRepaintRequest_ReturnsFalse()
        {
            RuntimeState runtimeState = new RuntimeState(
                needsRepaint: false,
                isPostCompileMode: true);

            bool shouldRefresh = McpEditorWindowRefreshPolicy.ShouldRefreshOnEditorUpdate(runtimeState);

            Assert.That(shouldRefresh, Is.False);
        }

        [Test]
        public void ShouldRunExpensiveChecks_WhenInitialPaint_ReturnsFalse()
        {
            bool shouldRun = McpEditorWindowRefreshPolicy.ShouldRunExpensiveChecks(
                McpEditorWindowRefreshMode.InitialPaint);

            Assert.That(shouldRun, Is.False);
        }

        [Test]
        public void ShouldRefreshSkillInstallState_WhenInitialPaintEvenIfRequested_ReturnsFalse()
        {
            bool shouldRefresh = McpEditorWindowRefreshPolicy.ShouldRefreshSkillInstallState(
                McpEditorWindowRefreshMode.InitialPaint,
                refreshRequested: true);

            Assert.That(shouldRefresh, Is.False);
        }

        [Test]
        public void ShouldRefreshSkillInstallState_WhenFullRefreshRequested_ReturnsTrue()
        {
            bool shouldRefresh = McpEditorWindowRefreshPolicy.ShouldRefreshSkillInstallState(
                McpEditorWindowRefreshMode.Full,
                refreshRequested: true);

            Assert.That(shouldRefresh, Is.True);
        }
    }
}
