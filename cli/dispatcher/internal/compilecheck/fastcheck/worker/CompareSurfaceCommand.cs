using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection.PortableExecutable;

namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// Answers whether two reference assemblies of one assembly describe the same surface to every
    /// assembly that compiles against them. Each is written out as text - its structure with what no
    /// dependent can see left out, then the effective nullable annotation of every row - and only
    /// identical texts are the same surface. Reads metadata alone; no compiler is loaded.
    /// </summary>
    internal static class CompareSurfaceCommand
    {
        // Raise whenever what the description writes changes, so a recorded answer is not reused for
        // a comparison that would now come out differently.
        private const string DescriptionVersion = "sv1";
        private const string NullabilitySection = "== nullability";
        private const string EndOfDescription = "(end)";

        internal static CompareSurfaceResponse Run(CompareSurfaceRequest request)
        {
            List<string> produced;
            List<string> unityBuild;
            // Why every failure answers "different": compiling a dependent that did not need it costs
            // time, while calling a surface the same without having read it costs a missed error.
            try
            {
                produced = Describe(request.ProducedPath);
                unityBuild = Describe(request.UnityBuildPath);
            }
            catch (Exception exception)
            {
                return CompareSurfaceResponse.Different("could not read: " + exception.GetType().Name);
            }

            int length = Math.Max(produced.Count, unityBuild.Count);
            for (int index = 0; index < length; index++)
            {
                string producedLine = index < produced.Count ? produced[index] : EndOfDescription;
                string unityBuildLine = index < unityBuild.Count ? unityBuild[index] : EndOfDescription;
                if (producedLine != unityBuildLine)
                {
                    return CompareSurfaceResponse.Different("produced: " + producedLine + " / Unity build: " + unityBuildLine);
                }
            }

            return CompareSurfaceResponse.Same();
        }

        /// <summary>
        /// Writes the surface description of one reference assembly.
        /// </summary>
        internal static List<string> Describe(string path)
        {
            using PEReader peReader = new PEReader(ImmutableArray.Create(File.ReadAllBytes(path)));
            SurfaceMetadata metadata = new SurfaceMetadata(peReader);
            List<string> lines = new List<string> { DescriptionVersion };
            SurfaceStructureWriter.Write(metadata, lines);
            lines.Add(NullabilitySection);
            SurfaceNullabilityWriter.Write(metadata, lines);

            return lines;
        }
    }
}
