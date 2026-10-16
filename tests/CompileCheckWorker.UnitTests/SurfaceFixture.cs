using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using NUnit.Framework;

namespace UnityCliLoop.CompileCheckWorker.UnitTests
{
    /// <summary>
    /// Builds the reference assemblies of two versions of a library the way Unity does, compiles a
    /// dependent against each, and asks the worker whether the two describe the same surface.
    /// </summary>
    internal static class SurfaceFixture
    {
        private const string LibraryAssemblyName = "Lib";

        // The name a library's InternalsVisibleTo has to give for the dependent to see its internals.
        internal const string DependentAssemblyName = "Dep";

        // The identifiers of a dependent's source, cut the way the scheduler cuts them: a letter or '_'
        // and then letters, digits and '_', with '@' left off, and "new" followed by '(' as "new(".
        private static readonly Regex Identifier = new Regex(@"[\p{L}_][\p{L}\p{Nd}_]*", RegexOptions.CultureInvariant);
        private static readonly Regex TargetTypedNew = new Regex(@"(?<![\p{L}\p{Nd}_])new\s*\(", RegexOptions.CultureInvariant);
        private const string TargetTypedNewToken = "new(";

        /// <summary>
        /// Asserts that the two versions build different bytes that a dependent reads the same way,
        /// and that the worker calls them the same surface.
        /// </summary>
        internal static void AssertSame(string oldSource, string newSource, string dependentSource)
        {
            byte[] oldAssembly = EmitReferenceAssembly(oldSource);
            byte[] newAssembly = EmitReferenceAssembly(newSource);
            Assert.That(newAssembly, Is.Not.EqualTo(oldAssembly),
                "the two versions built identical bytes, so the comparison was never exercised");
            Assert.That(DependentDiagnostics(newAssembly, dependentSource),
                Is.EqualTo(DependentDiagnostics(oldAssembly, dependentSource)),
                "the dependent compiles differently, so the case is not a same-surface case");
            CompareSurfaceResponse response = Compare(oldAssembly, newAssembly);
            Assert.That(response.Outcome, Is.EqualTo(CompareSurfaceResponse.SameOutcome), response.Detail);
        }

        /// <summary>
        /// Asserts that a dependent reads the two versions differently, and that the worker calls them
        /// different surfaces.
        /// </summary>
        internal static void AssertDifferent(string oldSource, string newSource, string dependentSource)
        {
            byte[] oldAssembly = EmitReferenceAssembly(oldSource);
            byte[] newAssembly = EmitReferenceAssembly(newSource);
            Assert.That(DependentDiagnostics(newAssembly, dependentSource),
                Is.Not.EqualTo(DependentDiagnostics(oldAssembly, dependentSource)),
                "the dependent compiles the same way, so the case does not show a real difference");
            CompareSurfaceResponse response = Compare(oldAssembly, newAssembly);
            Assert.That(response.Outcome, Is.EqualTo(CompareSurfaceResponse.DifferentOutcome));
            // A dependent that compiles differently must be compiled: either the change cannot be told
            // by names, or one of its names is written in the dependent.
            HashSet<string> identifiers = SourceIdentifiers(dependentSource);
            Assert.That(response.UnnamedReason != "" || response.ChangedNames.Any(identifiers.Contains), Is.True,
                "the dependent compiles differently but writes none of the changed names ["
                + string.Join(", ", response.ChangedNames) + "] and the change was not called unnamed");
        }

        /// <summary>
        /// Asserts that the two versions are different surfaces whose change is told by exactly the
        /// expected names.
        /// </summary>
        internal static void AssertChangedNames(string oldSource, string newSource, string[] expectedNames)
        {
            CompareSurfaceResponse response = Compare(EmitReferenceAssembly(oldSource), EmitReferenceAssembly(newSource));
            Assert.That(response.Outcome, Is.EqualTo(CompareSurfaceResponse.DifferentOutcome));
            Assert.That(response.UnnamedReason, Is.Empty);
            Assert.That(response.ChangedNames, Is.EquivalentTo(expectedNames));
        }

        /// <summary>
        /// Asserts that the two versions are different surfaces whose change cannot be told by names,
        /// for the expected reason, and that no names come with it.
        /// </summary>
        internal static void AssertUnnamed(string oldSource, string newSource, string reasonPrefix)
        {
            CompareSurfaceResponse response = Compare(EmitReferenceAssembly(oldSource), EmitReferenceAssembly(newSource));
            Assert.That(response.Outcome, Is.EqualTo(CompareSurfaceResponse.DifferentOutcome));
            Assert.That(response.UnnamedReason, Does.StartWith(reasonPrefix));
            Assert.That(response.ChangedNames, Is.Empty);
        }

        /// <summary>
        /// Lists the identifiers a dependent's source writes, comments and strings included.
        /// </summary>
        internal static HashSet<string> SourceIdentifiers(string source)
        {
            HashSet<string> identifiers = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Identifier.Matches(source))
            {
                identifiers.Add(match.Value);
            }
            if (TargetTypedNew.IsMatch(source))
            {
                identifiers.Add(TargetTypedNewToken);
            }

            return identifiers;
        }

        /// <summary>
        /// Builds the reference assembly of a library with nullable annotations enabled, the way Unity
        /// builds it with -refout and /deterministic.
        /// </summary>
        internal static byte[] EmitReferenceAssembly(string source)
        {
            CSharpCompilation compilation = CSharpCompilation.Create(
                LibraryAssemblyName,
                new[] { Parse(source) },
                new[] { StandardLibrary() },
                CompilationOptions());
            using MemoryStream assembly = new MemoryStream();
            using MemoryStream referenceAssembly = new MemoryStream();
            EmitResult result = compilation.Emit(
                assembly,
                metadataPEStream: referenceAssembly,
                options: new EmitOptions().WithIncludePrivateMembers(false));
            Assert.That(result.Success, Is.True, string.Join("\n", result.Diagnostics));

            return referenceAssembly.ToArray();
        }

        /// <summary>
        /// Compiles a dependent against a reference assembly and lists its warnings and errors as
        /// "ID@line:column", in a fixed order.
        /// </summary>
        internal static string DependentDiagnostics(byte[] referenceAssembly, string source)
        {
            CSharpCompilation compilation = CSharpCompilation.Create(
                DependentAssemblyName,
                new[] { Parse(source) },
                new[] { StandardLibrary(), MetadataReference.CreateFromImage(referenceAssembly) },
                CompilationOptions());
            using MemoryStream output = new MemoryStream();
            string[] diagnostics = compilation.Emit(output).Diagnostics
                .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
                .Select(Describe)
                .OrderBy(text => text, StringComparer.Ordinal)
                .ToArray();

            return diagnostics.Length == 0 ? "(none)" : string.Join(" ", diagnostics);
        }

        /// <summary>
        /// Writes the two assemblies to files and runs the worker's comparison on them.
        /// </summary>
        internal static CompareSurfaceResponse Compare(byte[] unityBuild, byte[] produced)
        {
            string directory = Path.Combine(Path.GetTempPath(), "compile-check-surface-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string unityBuildPath = Path.Combine(directory, "unity.ref.dll");
                string producedPath = Path.Combine(directory, "produced.ref.dll");
                File.WriteAllBytes(unityBuildPath, unityBuild);
                File.WriteAllBytes(producedPath, produced);

                return CompareSurfaceCommand.Run(new CompareSurfaceRequest
                {
                    ProducedPath = producedPath,
                    UnityBuildPath = unityBuildPath,
                });
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// Lists every field and method name inside the private nested types of a reference assembly,
        /// qualified by the type, so a test can show that those types are what differs.
        /// </summary>
        internal static List<string> PrivateNestedMemberNames(byte[] referenceAssembly)
        {
            using PEReader peReader = new PEReader(ImmutableArray.Create(referenceAssembly));
            MetadataReader reader = peReader.GetMetadataReader();
            List<string> names = new List<string>();
            foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
            {
                TypeDefinition type = reader.GetTypeDefinition(handle);
                if ((type.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.NestedPrivate)
                {
                    continue;
                }
                string typeName = reader.GetString(type.Name);
                names.Add(typeName);
                names.AddRange(type.GetFields().Select(field => typeName + "::" + reader.GetString(reader.GetFieldDefinition(field).Name)));
                names.AddRange(type.GetMethods().Select(method => typeName + "::" + reader.GetString(reader.GetMethodDefinition(method).Name)));
            }

            return names;
        }

        private static SyntaxTree Parse(string source)
        {
            return CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp9));
        }

        private static CSharpCompilationOptions CompilationOptions()
        {
            return new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                deterministic: true,
                allowUnsafe: true);
        }

        private static MetadataReference StandardLibrary()
        {
            return MetadataReference.CreateFromFile(Path.Combine(AppContext.BaseDirectory, "SurfaceReferences", "netstandard.dll"));
        }

        private static string Describe(Diagnostic diagnostic)
        {
            FileLinePositionSpan span = diagnostic.Location.GetLineSpan();

            return diagnostic.Id + "@" + (span.StartLinePosition.Line + 1) + ":" + (span.StartLinePosition.Character + 1);
        }
    }
}
