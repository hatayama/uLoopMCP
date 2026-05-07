using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace io.github.hatayama.UnityCliLoop
{
    // Guards the facade/service split introduced by the onion-architecture refactor.
    public sealed class StaticFacadeStateGuardTests
    {
        private static readonly string[] MigratedFacadePaths = new string[]
        {
            "Packages/src/Editor/CLI/CliInstallationDetector.cs",
            "Packages/src/Editor/Config/ToolSettings.cs",
            "Packages/src/Editor/Config/ULoopSettings.cs",
            "Packages/src/Editor/Config/UnityCliLoopEditorSettings.cs",
            "Packages/src/Editor/Api/Tools/Core/UnityCliLoopToolRegistrar.cs",
            "Packages/src/Editor/Api/Tools/Core/UnityCliLoopToolHostServicesProvider.cs",
            "Packages/src/Editor/Composition/DynamicCodeServices.cs",
            "Packages/src/Editor/Execution/DynamicCodeForegroundWarmupState.cs",
            "Packages/src/Editor/Execution/DynamicCodeStartupTelemetry.cs",
            "Packages/src/Editor/Utils/EditorDelayManager.cs",
            "Packages/src/Editor/Api/Tools/RecordInput/InputRecorder.cs",
            "Packages/src/Editor/Api/Tools/RecordInput/RecordingsApplicationFacade.cs",
            "Packages/src/Editor/Api/Tools/ReplayInput/InputReplayer.cs",
            "Packages/src/Editor/Api/Tools/SimulateKeyboard/KeyboardKeyState.cs",
            "Packages/src/Editor/Api/Tools/SimulateMouseInput/MouseInputState.cs",
            "Packages/src/Editor/Api/Tools/SimulateMouseUi/MouseDragState.cs",
            "Packages/src/Editor/Api/Tools/Core/OverlayCanvasFactory.cs",
            "Packages/src/Editor/Core/CoreTools/Util/MainThreadSwitcher.cs",
            "Packages/src/Editor/Logging/VibeLogger.cs",
            "Packages/src/Editor/Server/UnityCliLoopServerController.cs",
            "Packages/src/Runtime/RecordInput/RecordInputOverlayState.cs",
            "Packages/src/Runtime/ReplayInput/ReplayInputOverlayState.cs",
            "Packages/src/Runtime/SimulateKeyboard/SimulateKeyboardOverlayState.cs",
            "Packages/src/Runtime/SimulateMouseInput/SimulateMouseInputOverlayState.cs",
            "Packages/src/Runtime/SimulateMouseUi/SimulateMouseUiOverlayState.cs"
        };

        private static readonly string[] PublicContractPaths = new string[]
        {
            "Packages/src/Editor/ToolContracts/UnityCliLoopToolResponse.cs"
        };

        private static readonly Regex DirectMutableStaticFieldPattern = new Regex(
            @"\b(private|internal|public|protected)\s+static\s+(?!readonly\b)(?!event\b)(?!extern\b)[^(\r\n;=]*[;=]",
            RegexOptions.Compiled);

        private static readonly Regex ReadonlyMutableStaticFieldPattern = new Regex(
            @"\b(private|internal|public|protected)\s+static\s+readonly\s+([^;=]+)",
            RegexOptions.Compiled);

        private static readonly Regex DirectStaticEventPattern = new Regex(
            @"\b(private|internal|public|protected)\s+static\s+event\b",
            RegexOptions.Compiled);

        [Test]
        public void MigratedFacadeFiles_WhenScanned_DoNotOwnMutableStaticState()
        {
            // Tests that migrated facades keep state inside instance services instead of direct static fields.
            List<string> violations = FindMutableStaticFieldViolations();

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void ProductionSources_WhenScanned_DoNotDeclareStaticEvents()
        {
            // Tests that static entrypoints do not hide event subscription lifetimes.
            List<string> violations = FindDirectStaticEventViolations();

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void PublicContracts_WhenScanned_DoNotOwnMutableStaticState()
        {
            // Tests that extension-facing contracts do not share mutable state across tool responses.
            List<string> violations = FindMutableStaticFieldViolations(PublicContractPaths);

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        private static List<string> FindMutableStaticFieldViolations()
        {
            return FindMutableStaticFieldViolations(MigratedFacadePaths);
        }

        private static List<string> FindMutableStaticFieldViolations(string[] relativePaths)
        {
            List<string> violations = new List<string>();
            string projectRoot = UnityCliLoopPathResolver.GetProjectRoot();

            for (int pathIndex = 0; pathIndex < relativePaths.Length; pathIndex++)
            {
                string relativePath = relativePaths[pathIndex];
                string absolutePath = Path.Combine(projectRoot, relativePath);
                string[] lines = File.ReadAllLines(absolutePath);

                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string line = lines[lineIndex];
                    if (IsAllowedStaticLine(line))
                    {
                        continue;
                    }

                    if (DirectMutableStaticFieldPattern.IsMatch(line)
                        || ReadonlyMutableStaticFieldPattern.IsMatch(line))
                    {
                        violations.Add($"{relativePath}:{lineIndex + 1}: {line.Trim()}");
                    }
                }
            }

            return violations;
        }

        private static List<string> FindDirectStaticEventViolations()
        {
            List<string> violations = new List<string>();
            string projectRoot = UnityCliLoopPathResolver.GetProjectRoot();
            string packagesSrcRoot = Path.Combine(projectRoot, "Packages/src");
            string[] sourcePaths = Directory.GetFiles(packagesSrcRoot, "*.cs", SearchOption.AllDirectories);

            for (int pathIndex = 0; pathIndex < sourcePaths.Length; pathIndex++)
            {
                string absolutePath = sourcePaths[pathIndex];
                string relativePath = Path.GetRelativePath(projectRoot, absolutePath);
                string[] lines = File.ReadAllLines(absolutePath);

                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string line = lines[lineIndex];
                    if (DirectStaticEventPattern.IsMatch(line))
                    {
                        violations.Add($"{relativePath}:{lineIndex + 1}: {line.Trim()}");
                    }
                }
            }

            return violations;
        }

        private static bool IsAllowedStaticLine(string line)
        {
            if (line.Contains("=>"))
            {
                return true;
            }

            if (line.Contains("("))
            {
                return true;
            }

            return line.Contains("ServiceValue")
                   || line.Contains("RepositoryValue")
                   || line.Contains("RegistryValue");
        }
    }
}
