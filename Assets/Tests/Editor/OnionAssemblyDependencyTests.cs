using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace io.github.hatayama.UnityCliLoop
{
    [TestFixture]
    public sealed class OnionAssemblyDependencyTests
    {
        private const string ApplicationAssemblyName = "UnityCLILoop.Application";
        private const string CompositionRootAssemblyName = "UnityCLILoop.CompositionRoot.Editor";
        private const string DomainAssemblyName = "UnityCLILoop.Domain";
        private const string FirstPartyToolsAssemblyName = "UnityCLILoop.FirstPartyTools.Editor";
        private const string InfrastructureAssemblyName = "UnityCLILoop.Infrastructure";
        private const string MetadataValidationAssemblyName = "uLoopMCP.Editor.MetadataValidation";
        private const string PresentationAssemblyName = "UnityCLILoop.Presentation";
        private const string ToolContractsAssemblyName = "UnityCLILoop.ToolContracts";
        private const string RemovedSharedAssemblyGuidReference = "GUID:290394860909340b7835eb7cc215ee75";

        [Test]
        public void DomainAsmdef_WhenLoaded_HasNoProjectAssemblyReferences()
        {
            // Tests that the domain layer stays independent from outer project assemblies.
            string[] references = ReadResolvedReferences("Packages/src/Editor/Domain/UnityCLILoop.Domain.asmdef");

            Assert.That(references, Is.Empty);
        }

        [Test]
        public void PlatformResultTypes_WhenLoaded_CompileUnderDomainAssembly()
        {
            // Tests that pure platform result values live in the domain layer.
            string validationResultAssemblyName = typeof(ValidationResult).Assembly.GetName().Name;
            string serviceResultAssemblyName = typeof(ServiceResult<int>).Assembly.GetName().Name;

            Assert.That(validationResultAssemblyName, Is.EqualTo(DomainAssemblyName));
            Assert.That(serviceResultAssemblyName, Is.EqualTo(DomainAssemblyName));
        }

        [Test]
        public void ProjectRootIdentityValidator_WhenLoaded_CompilesUnderDomainAssembly()
        {
            // Tests that project identity safety policy lives in the domain layer.
            string validatorAssemblyName = typeof(ProjectRootIdentityValidator).Assembly.GetName().Name;

            Assert.That(validatorAssemblyName, Is.EqualTo(DomainAssemblyName));
        }

        [Test]
        public void CliVersionComparer_WhenLoaded_CompilesUnderDomainAssembly()
        {
            // Tests that CLI compatibility version ordering lives in the domain layer.
            string comparerAssemblyName = typeof(CliVersionComparer).Assembly.GetName().Name;

            Assert.That(comparerAssemblyName, Is.EqualTo(DomainAssemblyName));
        }

        [Test]
        public void CompilationDiagnosticMessageParser_WhenLoaded_CompilesUnderDomainAssembly()
        {
            // Tests that diagnostic-message parsing lives in the domain layer.
            string parserAssemblyName = typeof(CompilationDiagnosticMessageParser).Assembly.GetName().Name;

            Assert.That(parserAssemblyName, Is.EqualTo(DomainAssemblyName));
        }

        [Test]
        public void DynamicCodeConstants_WhenLoaded_CompileUnderDomainAssembly()
        {
            // Tests that dynamic-code platform defaults live in the domain layer.
            string constantsAssemblyName = typeof(DynamicCodeConstants).Assembly.GetName().Name;

            Assert.That(constantsAssemblyName, Is.EqualTo(DomainAssemblyName));
        }

        [Test]
        public void ScriptChangesDuringPlayOptions_WhenLoaded_CompilesUnderDomainAssembly()
        {
            // Tests that play-mode compilation policy values live in the domain layer.
            string optionsAssemblyName = typeof(ScriptChangesDuringPlayOptions).Assembly.GetName().Name;

            Assert.That(optionsAssemblyName, Is.EqualTo(DomainAssemblyName));
        }

        [Test]
        public void DynamicCodeSecurityValues_WhenLoaded_CompileUnderDomainAssembly()
        {
            // Tests that dynamic-code security result values live in the domain layer.
            string resultAssemblyName = typeof(SecurityValidationResult).Assembly.GetName().Name;
            string violationAssemblyName = typeof(SecurityViolation).Assembly.GetName().Name;

            Assert.That(resultAssemblyName, Is.EqualTo(DomainAssemblyName));
            Assert.That(violationAssemblyName, Is.EqualTo(DomainAssemblyName));
        }

        [Test]
        public void DangerousApiCatalog_WhenLoaded_CompilesUnderDomainAssembly()
        {
            // Tests that dynamic-code dangerous API policy lives in the domain layer.
            string catalogAssemblyName = typeof(DangerousApiCatalog).Assembly.GetName().Name;

            Assert.That(catalogAssemblyName, Is.EqualTo(DomainAssemblyName));
        }

        [Test]
        public void SourceSecurityScanner_WhenLoaded_CompilesUnderDomainAssembly()
        {
            // Tests that source-level dynamic-code security scanning lives in the domain layer.
            string scannerAssemblyName = typeof(SourceSecurityScanner).Assembly.GetName().Name;

            Assert.That(scannerAssemblyName, Is.EqualTo(DomainAssemblyName));
        }

        [Test]
        public void ToolContractsAsmdef_WhenLoaded_HasNoProjectAssemblyReferences()
        {
            // Tests that the public tool contract assembly stays independent from implementation assemblies.
            string[] references = ReadResolvedReferences("Packages/src/Editor/ToolContracts/UnityCLILoop.ToolContracts.asmdef");

            Assert.That(references, Is.Empty);
        }

        [Test]
        public void ApplicationAsmdef_WhenLoaded_ReferencesInnerContractsButNotOuterLayers()
        {
            // Tests that application code can depend inward without depending on presentation or infrastructure.
            string[] references = ReadResolvedReferences("Packages/src/Editor/UnityCLILoop.Application.asmdef");

            Assert.That(references, Does.Contain(DomainAssemblyName));
            Assert.That(references, Does.Contain(ToolContractsAssemblyName));
            Assert.That(references, Does.Not.Contain(PresentationAssemblyName));
            Assert.That(references, Does.Not.Contain(InfrastructureAssemblyName));
            Assert.That(references, Does.Not.Contain(CompositionRootAssemblyName));
            Assert.That(references, Does.Not.Contain(FirstPartyToolsAssemblyName));
        }

        [Test]
        public void DynamicCompilationPorts_WhenLoaded_CompileUnderApplicationAssembly()
        {
            // Tests that dynamic-code compilation ports are owned by the application layer.
            string serviceAssemblyName = typeof(IDynamicCompilationService).Assembly.GetName().Name;
            string factoryAssemblyName = typeof(IDynamicCompilationServiceFactory).Assembly.GetName().Name;
            string registryAssemblyName = typeof(DynamicCompilationServiceRegistry).Assembly.GetName().Name;

            Assert.That(serviceAssemblyName, Is.EqualTo(ApplicationAssemblyName));
            Assert.That(factoryAssemblyName, Is.EqualTo(ApplicationAssemblyName));
            Assert.That(registryAssemblyName, Is.EqualTo(ApplicationAssemblyName));
        }

        [Test]
        public void DynamicCompilationDtos_WhenLoaded_CompileUnderApplicationAssembly()
        {
            // Tests that dynamic-code compilation DTOs are owned by the application layer.
            string requestAssemblyName = typeof(CompilationRequest).Assembly.GetName().Name;
            string resultAssemblyName = typeof(CompilationResult).Assembly.GetName().Name;
            string errorAssemblyName = typeof(CompilationError).Assembly.GetName().Name;
            string backendKindAssemblyName = typeof(DynamicCompilationBackendKind).Assembly.GetName().Name;
            string cacheManagerAssemblyName = typeof(CompilationCacheManager).Assembly.GetName().Name;

            Assert.That(requestAssemblyName, Is.EqualTo(ApplicationAssemblyName));
            Assert.That(resultAssemblyName, Is.EqualTo(ApplicationAssemblyName));
            Assert.That(errorAssemblyName, Is.EqualTo(ApplicationAssemblyName));
            Assert.That(backendKindAssemblyName, Is.EqualTo(ApplicationAssemblyName));
            Assert.That(cacheManagerAssemblyName, Is.EqualTo(ApplicationAssemblyName));
        }

        [Test]
        public void HierarchyHostServiceContract_WhenLoaded_CompilesUnderToolContractsAssembly()
        {
            // Tests that bundled hierarchy tools consume platform behavior through the public host-service boundary.
            string serviceAssemblyName = typeof(IUnityCliLoopHierarchyService).Assembly.GetName().Name;
            string requestAssemblyName = typeof(UnityCliLoopHierarchyRequest).Assembly.GetName().Name;
            string resultAssemblyName = typeof(UnityCliLoopHierarchyResult).Assembly.GetName().Name;

            Assert.That(serviceAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(requestAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(resultAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
        }

        [Test]
        public void GetHierarchyUseCase_WhenLoaded_CompilesUnderApplicationAssembly()
        {
            // Tests that the application layer owns the hierarchy host-service implementation.
            string useCaseAssemblyName = typeof(GetHierarchyUseCase).Assembly.GetName().Name;

            Assert.That(useCaseAssemblyName, Is.EqualTo(ApplicationAssemblyName));
        }

        [Test]
        public void TestExecutionHostServiceContract_WhenLoaded_CompilesUnderToolContractsAssembly()
        {
            // Tests that bundled test-runner tools consume platform behavior through the public host-service boundary.
            string serviceAssemblyName = typeof(IUnityCliLoopTestExecutionService).Assembly.GetName().Name;
            string requestAssemblyName = typeof(UnityCliLoopTestExecutionRequest).Assembly.GetName().Name;
            string resultAssemblyName = typeof(UnityCliLoopTestExecutionResult).Assembly.GetName().Name;

            Assert.That(serviceAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(requestAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(resultAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
        }

        [Test]
        public void RunTestsUseCase_WhenLoaded_CompilesUnderApplicationAssembly()
        {
            // Tests that the application layer owns the test execution host-service implementation.
            string useCaseAssemblyName = typeof(RunTestsUseCase).Assembly.GetName().Name;

            Assert.That(useCaseAssemblyName, Is.EqualTo(ApplicationAssemblyName));
        }

        [Test]
        public void GameObjectSearchHostServiceContract_WhenLoaded_CompilesUnderToolContractsAssembly()
        {
            // Tests that bundled GameObject search tools consume platform behavior through the public host-service boundary.
            string serviceAssemblyName = typeof(IUnityCliLoopGameObjectSearchService).Assembly.GetName().Name;
            string requestAssemblyName = typeof(UnityCliLoopGameObjectSearchRequest).Assembly.GetName().Name;
            string resultAssemblyName = typeof(UnityCliLoopGameObjectSearchResult).Assembly.GetName().Name;
            string componentAssemblyName = typeof(ComponentInfo).Assembly.GetName().Name;

            Assert.That(serviceAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(requestAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(resultAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(componentAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
        }

        [Test]
        public void FindGameObjectsUseCase_WhenLoaded_CompilesUnderApplicationAssembly()
        {
            // Tests that the application layer owns the GameObject search host-service implementation.
            string useCaseAssemblyName = typeof(FindGameObjectsUseCase).Assembly.GetName().Name;

            Assert.That(useCaseAssemblyName, Is.EqualTo(ApplicationAssemblyName));
        }

        [Test]
        public void ScreenshotHostServiceContract_WhenLoaded_CompilesUnderToolContractsAssembly()
        {
            // Tests that bundled screenshot tools consume platform behavior through the public host-service boundary.
            string serviceAssemblyName = typeof(IUnityCliLoopScreenshotService).Assembly.GetName().Name;
            string requestAssemblyName = typeof(UnityCliLoopScreenshotRequest).Assembly.GetName().Name;
            string resultAssemblyName = typeof(UnityCliLoopScreenshotResult).Assembly.GetName().Name;
            string elementAssemblyName = typeof(UIElementInfo).Assembly.GetName().Name;

            Assert.That(serviceAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(requestAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(resultAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(elementAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
        }

        [Test]
        public void ScreenshotUseCase_WhenLoaded_CompilesUnderApplicationAssembly()
        {
            // Tests that the application layer owns the screenshot host-service implementation.
            string useCaseAssemblyName = typeof(ScreenshotUseCase).Assembly.GetName().Name;

            Assert.That(useCaseAssemblyName, Is.EqualTo(ApplicationAssemblyName));
        }

        [Test]
        public void InputRecordingHostServiceContract_WhenLoaded_CompilesUnderToolContractsAssembly()
        {
            // Tests that bundled input recording tools consume platform behavior through the public host-service boundary.
            string recordServiceAssemblyName = typeof(IUnityCliLoopRecordInputService).Assembly.GetName().Name;
            string recordRequestAssemblyName = typeof(UnityCliLoopRecordInputRequest).Assembly.GetName().Name;
            string recordResultAssemblyName = typeof(UnityCliLoopRecordInputResult).Assembly.GetName().Name;
            string replayServiceAssemblyName = typeof(IUnityCliLoopReplayInputService).Assembly.GetName().Name;
            string replayRequestAssemblyName = typeof(UnityCliLoopReplayInputRequest).Assembly.GetName().Name;
            string replayResultAssemblyName = typeof(UnityCliLoopReplayInputResult).Assembly.GetName().Name;
            string recordActionAssemblyName = typeof(RecordInputAction).Assembly.GetName().Name;
            string replayActionAssemblyName = typeof(ReplayInputAction).Assembly.GetName().Name;

            Assert.That(recordServiceAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(recordRequestAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(recordResultAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(replayServiceAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(replayRequestAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(replayResultAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(recordActionAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(replayActionAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
        }

        [Test]
        public void InputRecordingUseCases_WhenLoaded_CompileUnderApplicationAssembly()
        {
            // Tests that the application layer owns the record/replay host-service implementations.
            string recordUseCaseAssemblyName = typeof(RecordInputUseCase).Assembly.GetName().Name;
            string replayUseCaseAssemblyName = typeof(ReplayInputUseCase).Assembly.GetName().Name;

            Assert.That(recordUseCaseAssemblyName, Is.EqualTo(ApplicationAssemblyName));
            Assert.That(replayUseCaseAssemblyName, Is.EqualTo(ApplicationAssemblyName));
        }

        [Test]
        public void InputSimulationHostServiceContract_WhenLoaded_CompilesUnderToolContractsAssembly()
        {
            // Tests that bundled input simulation tools consume platform behavior through the public host-service boundary.
            string keyboardServiceAssemblyName = typeof(IUnityCliLoopKeyboardSimulationService).Assembly.GetName().Name;
            string keyboardRequestAssemblyName = typeof(UnityCliLoopKeyboardSimulationRequest).Assembly.GetName().Name;
            string keyboardResultAssemblyName = typeof(UnityCliLoopKeyboardSimulationResult).Assembly.GetName().Name;
            string mouseServiceAssemblyName = typeof(IUnityCliLoopMouseInputSimulationService).Assembly.GetName().Name;
            string mouseRequestAssemblyName = typeof(UnityCliLoopMouseInputSimulationRequest).Assembly.GetName().Name;
            string mouseResultAssemblyName = typeof(UnityCliLoopMouseInputSimulationResult).Assembly.GetName().Name;

            Assert.That(keyboardServiceAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(keyboardRequestAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(keyboardResultAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(mouseServiceAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(mouseRequestAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
            Assert.That(mouseResultAssemblyName, Is.EqualTo(ToolContractsAssemblyName));
        }

        [Test]
        public void InputSimulationUseCases_WhenLoaded_CompileUnderApplicationAssembly()
        {
            // Tests that the application layer owns the keyboard and mouse input simulation host-service implementations.
            string keyboardUseCaseAssemblyName = typeof(SimulateKeyboardUseCase).Assembly.GetName().Name;
            string mouseUseCaseAssemblyName = typeof(SimulateMouseInputUseCase).Assembly.GetName().Name;

            Assert.That(keyboardUseCaseAssemblyName, Is.EqualTo(ApplicationAssemblyName));
            Assert.That(mouseUseCaseAssemblyName, Is.EqualTo(ApplicationAssemblyName));
        }

        [Test]
        public void PreloadMetadataValidationPorts_WhenLoaded_CompileUnderMetadataValidationAssembly()
        {
            // Tests that preload metadata validation contracts are owned by the metadata validation module.
            string validatorAssemblyName = typeof(IPreloadAssemblySecurityValidator).Assembly.GetName().Name;
            string overrideAssemblyName = typeof(IOverrideDefaultPreloadAssemblySecurityValidation).Assembly.GetName().Name;
            string registryAssemblyName = typeof(PreloadAssemblySecurityValidatorRegistry).Assembly.GetName().Name;

            Assert.That(validatorAssemblyName, Is.EqualTo(MetadataValidationAssemblyName));
            Assert.That(overrideAssemblyName, Is.EqualTo(MetadataValidationAssemblyName));
            Assert.That(registryAssemblyName, Is.EqualTo(MetadataValidationAssemblyName));
        }

        [Test]
        public void MetadataValidationAsmdef_WhenLoaded_DependsOnlyOnDomain()
        {
            // Tests that metadata validation exposes its own contracts without using Shared as a bucket.
            string[] references = ReadResolvedReferences("Packages/src/Editor/MetadataValidation/uLoopMCP.Editor.MetadataValidation.asmdef");

            Assert.That(references, Is.EquivalentTo(new[] { DomainAssemblyName }));
        }

        [Test]
        public void SharedSupportTypes_WhenLoaded_CompileUnderApplicationAssembly()
        {
            // Tests that support constants and logging are owned by the application layer instead of a shared bucket.
            string constantsAssemblyName = typeof(UnityCliLoopConstants).Assembly.GetName().Name;
            string loggerAssemblyName = typeof(VibeLogger).Assembly.GetName().Name;
            string registryAssemblyName = typeof(DomainReloadStateRegistry).Assembly.GetName().Name;
            string providerAssemblyName = typeof(IDomainReloadStateProvider).Assembly.GetName().Name;

            Assert.That(constantsAssemblyName, Is.EqualTo(ApplicationAssemblyName));
            Assert.That(loggerAssemblyName, Is.EqualTo(ApplicationAssemblyName));
            Assert.That(registryAssemblyName, Is.EqualTo(ApplicationAssemblyName));
            Assert.That(providerAssemblyName, Is.EqualTo(ApplicationAssemblyName));
        }

        [Test]
        public void PresentationAsmdef_WhenLoaded_DependsOnApplicationAndDoesNotReferenceInfrastructure()
        {
            // Tests that presentation and infrastructure remain sibling outer layers.
            string[] references = ReadResolvedReferences("Packages/src/Editor/Presentation/UnityCLILoop.Presentation.asmdef");

            Assert.That(references, Is.EquivalentTo(new[]
            {
                ApplicationAssemblyName,
                DomainAssemblyName,
                ToolContractsAssemblyName
            }));
        }

        [Test]
        public void InfrastructureAsmdef_WhenLoaded_DependsOnApplicationAndDoesNotReferencePresentation()
        {
            // Tests that infrastructure and presentation remain sibling outer layers.
            string[] references = ReadResolvedReferences("Packages/src/Editor/Infrastructure/UnityCLILoop.Infrastructure.asmdef");

            Assert.That(references, Is.EquivalentTo(new[]
            {
                ApplicationAssemblyName,
                DomainAssemblyName,
                ToolContractsAssemblyName
            }));
        }

        [Test]
        public void FirstPartyToolsAsmdef_WhenLoaded_ReferencesOnlyToolContracts()
        {
            // Tests that first-party tools use the same public contract boundary as extension tools.
            string[] references = ReadResolvedReferences("Packages/src/Editor/FirstPartyTools/UnityCLILoop.FirstPartyTools.Editor.asmdef");

            Assert.That(references, Is.EquivalentTo(new[] { ToolContractsAssemblyName }));
        }

        [Test]
        public void CompositionRootAsmdef_WhenLoaded_ReferencesAllOnionAssemblies()
        {
            // Tests that the composition root is the assembly allowed to wire every layer together.
            string[] references = ReadResolvedReferences("Packages/src/Editor/CompositionRoot/UnityCLILoop.CompositionRoot.Editor.asmdef");

            Assert.That(references, Is.EquivalentTo(new[]
            {
                ApplicationAssemblyName,
                DomainAssemblyName,
                FirstPartyToolsAssemblyName,
                InfrastructureAssemblyName,
                PresentationAssemblyName,
                ToolContractsAssemblyName
            }));
        }

        [Test]
        public void DynamicCodeCompilerRegistration_WhenLoaded_CompilesUnderCompositionRootAssembly()
        {
            // Tests that dynamic-code service registration is owned by the composition root.
            string registrationAssemblyName = typeof(DynamicCodeCompilationServiceRegistration).Assembly.GetName().Name;

            Assert.That(registrationAssemblyName, Is.EqualTo(CompositionRootAssemblyName));
        }

        [Test]
        public void DynamicCodeCompilationServiceFactory_WhenLoaded_CompilesUnderInfrastructureAssembly()
        {
            // Tests that concrete dynamic-code compiler construction is owned by infrastructure.
            string factoryAssemblyName = typeof(DynamicCodeCompilationServiceFactory).Assembly.GetName().Name;

            Assert.That(factoryAssemblyName, Is.EqualTo(InfrastructureAssemblyName));
        }

        [Test]
        public void ConsoleClearService_WhenLoaded_CompilesUnderInfrastructureAssembly()
        {
            // Tests that Unity Console mutation is owned by infrastructure.
            string serviceAssemblyName = typeof(ConsoleClearService).Assembly.GetName().Name;

            Assert.That(serviceAssemblyName, Is.EqualTo(InfrastructureAssemblyName));
        }

        [Test]
        public void ToolHostServices_WhenLoaded_CompileUnderCompositionRootAssembly()
        {
            // Tests that concrete tool host wiring is owned by the composition root.
            Type hostServicesType = Type.GetType(
                "io.github.hatayama.UnityCliLoop.UnityCliLoopToolHostServices, " + CompositionRootAssemblyName);

            Assert.That(hostServicesType, Is.Not.Null);
        }

        [Test]
        public void ToolRegistry_WhenLoaded_DoesNotCreateConcreteHostServices()
        {
            // Tests that the application registry depends on a registered host-services factory instead of wiring concrete services itself.
            string registrySource = File.ReadAllText(
                "Packages/src/Editor/Api/Tools/Core/UnityCliLoopToolRegistry.cs");

            Assert.That(registrySource, Does.Not.Contain("new UnityCliLoopToolHostServices"));
        }

        [Test]
        public void ProductionAsmdefs_WhenLoaded_DoNotReferenceCompositionRootExceptCompositionRootItself()
        {
            // Tests that composition root dependencies do not leak back into production assemblies.
            string[] offendingAssemblyNames = ReadProductionAsmdefPaths()
                .Where(path => ReadAsmdefName(path) != CompositionRootAssemblyName)
                .Where(path => ReadResolvedReferencesFromAbsolutePath(path).Contains(CompositionRootAssemblyName))
                .Select(ReadAsmdefName)
                .OrderBy(assemblyName => assemblyName)
                .ToArray();

            Assert.That(offendingAssemblyNames, Is.Empty);
        }

        [Test]
        public void ProjectAsmdefs_WhenLoaded_DoNotReferenceRemovedSharedAssemblyGuid()
        {
            // Tests that removed module asmdefs do not leave stale GUID references in dependent asmdefs.
            string[] offendingReferences = ReadProjectAsmdefPaths()
                .Where(path => ReadRawReferences(path).Contains(RemovedSharedAssemblyGuidReference))
                .Select(path => $"{ReadAsmdefName(path)} references removed shared assembly")
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void EditorUiFiles_WhenLoaded_DoNotReferenceFacadeInternalsDirectly()
        {
            // Tests that presentation-facing UI code uses Application facades for setup and tool settings workflows.
            string[] forbiddenReferences =
            {
                "CliInstallationDetector",
                "ProjectLocalCliAutoInstaller",
                "ProjectLocalCliInstaller",
                "NativeCliInstaller",
                "CliVersionComparer",
                "ToolSkillSynchronizer",
                "ULoopSettings.",
                "ToolSettings.",
                "UnityCliLoopToolRegistrar",
                "UnityCliLoopToolRegistry",
                "ToolSettingsCatalogItem",
                "InputRecorder",
                "InputReplayer",
                "InputRecordingFileHelper",
                "InputRecordingData",
                "RecordInputConstants",
                "RecordInputOverlayState",
                "OverlayCanvasFactory",
                "RecordReplayOverlayFactory"
            };
            string[] offendingReferences = ReadPresentationSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, forbiddenReferences))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void EditorUiFiles_WhenLoaded_ContainNoEditorWindowImplementations()
        {
            // Tests that EditorWindow implementations compile under the presentation assembly.
            string uiRoot = Path.Combine(UnityCliLoopPathResolver.GetProjectRoot(), "Packages", "src", "Editor", "UI");
            if (!Directory.Exists(uiRoot))
            {
                Assert.Pass();
            }

            string[] offendingReferences = Directory.GetFiles(uiRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains("EditorWindow"))
                .Select(path => Path.GetRelativePath(UnityCliLoopPathResolver.GetProjectRoot(), path))
                .OrderBy(path => path)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void EditorUiFiles_WhenLoaded_DoNotUseLegacyMcpSettingsWindowPath()
        {
            // Tests that the settings window does not return under the legacy MCP UI file path.
            string legacyUiFilePath = Path.Combine(
                UnityCliLoopPathResolver.GetProjectRoot(),
                "Packages",
                "src",
                "Editor",
                "UI",
                "Mcp" + "EditorWindow.cs");

            Assert.That(File.Exists(legacyUiFilePath), Is.False);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyMcpCommunicationLogTypes()
        {
            // Tests that the removed MCP communication log utility does not return under a legacy public name.
            string[] legacyNames =
            {
                "Mcp" + "CommunicationLog",
                "Mcp" + "CommunicationLogger"
            };
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, legacyNames))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ToolSourceFolders_WhenLoaded_DoNotUseLegacyMcpToolsFolder()
        {
            // Tests that public tool implementations are not grouped under the legacy MCP folder name.
            string editorApiRoot = Path.Combine(UnityCliLoopPathResolver.GetProjectRoot(), "Packages", "src", "Editor", "Api");
            string legacyToolFolder = Path.Combine(editorApiRoot, "Mcp" + "Tools");
            string currentToolFolder = Path.Combine(editorApiRoot, "Tools");

            Assert.That(Directory.Exists(legacyToolFolder), Is.False);
            Assert.That(Directory.Exists(currentToolFolder), Is.True);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyMcpUiConstantsName()
        {
            // Tests that shared UI constants use UnityCLILoop naming instead of legacy MCP naming.
            string legacyUiConstantsName = "Mcp" + "UIConstants";
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, new[] { legacyUiConstantsName }))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyMcpEditorSettingsName()
        {
            // Tests that editor settings use UnityCLILoop naming outside protocol-specific code.
            string legacyEditorSettingsName = "Mcp" + "EditorSettings";
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, new[] { legacyEditorSettingsName }))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyMcpEditorDomainReloadProviderName()
        {
            // Tests that editor domain reload state code uses UnityCLILoop naming outside protocol-specific code.
            string[] legacyNames =
            {
                "Mcp" + "EditorDomainReloadStateProvider",
                "Mcp" + "EditorDomainReloadStateRegistration"
            };
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, legacyNames))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyUnityMcpPathResolverName()
        {
            // Tests that path resolution code uses UnityCLILoop naming outside protocol-specific code.
            string legacyPathResolverName = "Unity" + "McpPathResolver";
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, new[] { legacyPathResolverName }))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyMcpVersionName()
        {
            // Tests that package version code uses UnityCLILoop naming outside protocol-specific code.
            string legacyVersionName = "Mcp" + "Version";
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, new[] { legacyVersionName }))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyMcpSecurityNames()
        {
            // Tests that tool execution security code uses UnityCLILoop naming outside protocol-specific code.
            string[] legacyNames =
            {
                "Mcp" + "SecurityChecker",
                "Mcp" + "SecurityException"
            };
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, legacyNames))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyMcpLogTypeName()
        {
            // Tests that GetLogs parameter values use UnityCLILoop naming outside protocol-specific code.
            string legacyLogTypeName = "Mcp" + "LogType";
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, new[] { legacyLogTypeName }))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyMcpConstantsName()
        {
            // Tests that shared constants use UnityCLILoop naming outside protocol-specific code.
            string legacyConstantsName = "Mcp" + "Constants";
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, new[] { legacyConstantsName }))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyMcpServerConfigName()
        {
            // Tests that bridge server settings use UnityCLILoop naming outside protocol-specific code.
            string legacyConfigName = "Mcp" + "ServerConfig";
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, new[] { legacyConfigName }))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotReferenceRemovedLegacyMcpTypes()
        {
            // Tests that comments and docs in production source do not point to removed legacy types.
            string[] legacyNames =
            {
                "Mcp" + "SessionManager",
                "Mcp" + "Logger",
                "Unity" + "McpServer"
            };
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, legacyNames))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyMcpServerLifecycleNames()
        {
            // Tests that project IPC server lifecycle types use UnityCLILoop naming outside protocol-specific code.
            string[] legacyNames =
            {
                "Mcp" + "BridgeServer",
                "Mcp" + "ServerController",
                "Mcp" + "ServerStartupService",
                "Mcp" + "ServerInitializationUseCase",
                "Mcp" + "ServerShutdownUseCase"
            };
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, legacyNames))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void EditorUiFiles_WhenLoaded_DoNotUseLegacyMcpSettingsWindowNames()
        {
            // Tests that settings-window UI code uses UnityCLILoop naming instead of legacy MCP naming.
            string legacySettingsWindowName = "Mcp" + "EditorWindow";
            string[] offendingReferences = ReadPresentationSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, new[] { legacySettingsWindowName }))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void PresentationAssets_WhenLoaded_DoNotUseLegacyMcpStylePrefix()
        {
            // Tests that presentation USS, UXML, and C# class names do not keep the legacy MCP style prefix.
            string legacyStylePrefix = "mcp" + "-";
            string[] offendingReferences = ReadPresentationAssetPaths()
                .SelectMany(path => FindForbiddenReferences(path, new[] { legacyStylePrefix }))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        private static string[] ReadResolvedReferences(string relativeAsmdefPath)
        {
            string asmdefPath = Path.Combine(UnityCliLoopPathResolver.GetProjectRoot(), relativeAsmdefPath);
            return ReadResolvedReferencesFromAbsolutePath(asmdefPath);
        }

        private static string[] ReadResolvedReferencesFromAbsolutePath(string asmdefPath)
        {
            Dictionary<string, string> guidToAssemblyNameMap = BuildGuidToAssemblyNameMap();
            string[] rawReferences = ReadRawReferences(asmdefPath);
            List<string> resolvedReferences = new List<string>();

            foreach (string rawReference in rawReferences)
            {
                resolvedReferences.Add(ResolveReference(rawReference, guidToAssemblyNameMap));
            }

            return resolvedReferences
                .OrderBy(reference => reference)
                .ToArray();
        }

        private static string[] ReadRawReferences(string asmdefPath)
        {
            JObject asmdef = JObject.Parse(File.ReadAllText(asmdefPath));
            return asmdef["references"]?.Values<string>().ToArray() ?? new string[0];
        }

        private static string[] ReadProductionAsmdefPaths()
        {
            string editorRoot = Path.Combine(UnityCliLoopPathResolver.GetProjectRoot(), "Packages", "src", "Editor");
            return Directory.GetFiles(editorRoot, "*.asmdef", SearchOption.AllDirectories);
        }

        private static string[] ReadPresentationSourcePaths()
        {
            string presentationRoot = Path.Combine(UnityCliLoopPathResolver.GetProjectRoot(), "Packages", "src", "Editor", "Presentation");
            return Directory.GetFiles(presentationRoot, "*.cs", SearchOption.AllDirectories);
        }

        private static string[] ReadPresentationAssetPaths()
        {
            string presentationRoot = Path.Combine(UnityCliLoopPathResolver.GetProjectRoot(), "Packages", "src", "Editor", "Presentation");
            return Directory.GetFiles(presentationRoot, "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".cs", StringComparison.Ordinal)
                    || path.EndsWith(".uxml", StringComparison.Ordinal)
                    || path.EndsWith(".uss", StringComparison.Ordinal))
                .ToArray();
        }

        private static string[] ReadProductionSourcePaths()
        {
            string editorRoot = Path.Combine(UnityCliLoopPathResolver.GetProjectRoot(), "Packages", "src", "Editor");
            return Directory.GetFiles(editorRoot, "*.cs", SearchOption.AllDirectories);
        }

        private static string[] FindForbiddenReferences(string path, string[] forbiddenReferences)
        {
            string source = File.ReadAllText(path);
            List<string> violations = new List<string>();

            foreach (string forbiddenReference in forbiddenReferences)
            {
                if (!source.Contains(forbiddenReference))
                {
                    continue;
                }

                violations.Add($"{Path.GetFileName(path)} references {forbiddenReference}");
            }

            return violations.ToArray();
        }

        private static Dictionary<string, string> BuildGuidToAssemblyNameMap()
        {
            string[] asmdefPaths = ReadProjectAsmdefPaths();
            Dictionary<string, string> guidToAssemblyNameMap = new Dictionary<string, string>();

            foreach (string asmdefPath in asmdefPaths)
            {
                string metaPath = asmdefPath + ".meta";
                if (!File.Exists(metaPath))
                {
                    continue;
                }

                string guid = ReadMetaGuid(metaPath);
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                guidToAssemblyNameMap[guid] = ReadAsmdefName(asmdefPath);
            }

            return guidToAssemblyNameMap;
        }

        private static string[] ReadProjectAsmdefPaths()
        {
            string projectRoot = UnityCliLoopPathResolver.GetProjectRoot();
            List<string> asmdefPaths = new List<string>();
            string assetsPath = Path.Combine(projectRoot, "Assets");
            string packagesSrcPath = Path.Combine(projectRoot, "Packages", "src");

            if (Directory.Exists(assetsPath))
            {
                asmdefPaths.AddRange(Directory.GetFiles(assetsPath, "*.asmdef", SearchOption.AllDirectories));
            }

            if (Directory.Exists(packagesSrcPath))
            {
                asmdefPaths.AddRange(Directory.GetFiles(packagesSrcPath, "*.asmdef", SearchOption.AllDirectories));
            }

            return asmdefPaths.ToArray();
        }

        private static string ResolveReference(string reference, Dictionary<string, string> guidToAssemblyNameMap)
        {
            const string guidReferencePrefix = "GUID:";
            if (!reference.StartsWith(guidReferencePrefix))
            {
                return reference;
            }

            string guid = reference.Substring(guidReferencePrefix.Length);
            if (!guidToAssemblyNameMap.ContainsKey(guid))
            {
                return reference;
            }

            return guidToAssemblyNameMap[guid];
        }

        private static string ReadMetaGuid(string metaPath)
        {
            string[] lines = File.ReadAllLines(metaPath);

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (!trimmedLine.StartsWith("guid:"))
                {
                    continue;
                }

                return trimmedLine.Substring("guid:".Length).Trim();
            }

            return string.Empty;
        }

        private static string ReadAsmdefName(string asmdefPath)
        {
            JObject asmdef = JObject.Parse(File.ReadAllText(asmdefPath));
            return asmdef["name"]?.Value<string>() ?? string.Empty;
        }
    }
}
