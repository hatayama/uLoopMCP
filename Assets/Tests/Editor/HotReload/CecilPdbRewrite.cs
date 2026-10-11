using System.IO;

using Mono.Cecil;
using Mono.Cecil.Cil;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// Writes a copy of a compiled assembly of this project the way an IL post-processor does, for
    /// tests that need a PDB which lost the documents no sequence point refers to.
    /// </summary>
    internal static class CecilPdbRewrite
    {
        // Reads the assembly with its symbols and writes both again, as an IL post-processor does.
        // Cecil keeps only the documents a method refers to. workDirectory receives the copy that is
        // read, so the compiled files are never opened for writing.
        internal static void Rewrite(string projectRoot, string assemblyName, string workDirectory, string outputDllPath)
        {
            CompiledAssemblyLayout real = CompiledAssemblyLayout.Resolve(projectRoot);
            string sourceDll = Path.Combine(workDirectory, "Source", assemblyName + ".dll");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceDll));
            File.Copy(real.DllPath(assemblyName), sourceDll);
            File.Copy(real.PdbPath(assemblyName), Path.ChangeExtension(sourceDll, ".pdb"));
            using (DefaultAssemblyResolver resolver = new DefaultAssemblyResolver())
            {
                resolver.AddSearchDirectory(real.CompiledAssembliesDirectory);
                resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location));
                using (ModuleDefinition module = ModuleDefinition.ReadModule(
                    sourceDll,
                    new ReaderParameters { ReadSymbols = true, AssemblyResolver = resolver }))
                {
                    module.Write(outputDllPath, new WriterParameters
                    {
                        WriteSymbols = true,
                        SymbolWriterProvider = new PortablePdbWriterProvider(),
                    });
                }
            }
        }
    }
}
