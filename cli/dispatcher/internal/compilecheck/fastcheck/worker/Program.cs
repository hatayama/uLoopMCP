using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;

namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// Entry point of the changed-files check worker that compile-check builds and runs with the
    /// Editor's dotnet and Roslyn.
    /// </summary>
    internal static class Program
    {
        private const int CompletedExitCode = 0;
        private const int UsageExitCode = 2;
        private const int UnexpectedFailureExitCode = 3;
        internal const string FingerprintCommandName = "fingerprint";
        internal const string CheckCommandName = "check";
        internal const string CompareSurfaceCommandName = "compare-surface";
        private const string RoslynDirectorySidecarFileName = "roslyn-directory.txt";

        private static int Main(string[] args)
        {
            if (args.Length != 3 || !IsCommand(args[0]))
            {
                Console.Error.WriteLine("usage: worker (fingerprint|check|compare-surface) <in.json> <out.json>");
                return UsageExitCode;
            }

            try
            {
                RegisterRoslynResolver();
                RunCommand(args[0], args[1], args[2]);
                return CompletedExitCode;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.ToString());
                return UnexpectedFailureExitCode;
            }
        }

        private static bool IsCommand(string command)
        {
            return command == FingerprintCommandName || command == CheckCommandName || command == CompareSurfaceCommandName;
        }

        // Runs one command on the request in inputPath and writes its response to outputPath.
        // Kept out of Main so no Roslyn type is loaded before the resolver is registered.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void RunCommand(string command, string inputPath, string outputPath)
        {
            switch (command)
            {
                case FingerprintCommandName:
                    WriteJson(outputPath, FingerprintCommand.Run(ReadJson<FingerprintRequest>(inputPath)));
                    return;
                case CheckCommandName:
                    WriteJson(outputPath, ChangedFileChecker.Check(ReadJson<CheckRequest>(inputPath)));
                    return;
                case CompareSurfaceCommandName:
                    WriteJson(outputPath, CompareSurfaceCommand.Run(ReadJson<CompareSurfaceRequest>(inputPath)));
                    return;
                default:
                    throw new ArgumentException("unknown worker command: " + command, nameof(command));
            }
        }

        private static T ReadJson<T>(string path)
        {
            T value = JsonSerializer.Deserialize<T>(File.ReadAllBytes(path));
            if (value == null)
            {
                throw new InvalidOperationException("request file is empty: " + path);
            }

            return value;
        }

        private static void WriteJson<T>(string path, T value)
        {
            // UTF-8 without a BOM, which the Go side reads on every platform.
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(value));
        }

        private static void RegisterRoslynResolver()
        {
            // Roslyn ships with the Editor next to csc, not in the shared framework the worker runs on.
            string roslynDirectoryPath = ReadRoslynDirectorySidecar();
            AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
            {
                string candidatePath = Path.Combine(roslynDirectoryPath, assemblyName.Name + ".dll");
                if (File.Exists(candidatePath))
                {
                    return context.LoadFromAssemblyPath(candidatePath);
                }

                return null;
            };
        }

        private static string ReadRoslynDirectorySidecar()
        {
            string sidecarPath = Path.Combine(AppContext.BaseDirectory, RoslynDirectorySidecarFileName);
            if (!File.Exists(sidecarPath))
            {
                throw new InvalidOperationException("Roslyn directory sidecar not found next to worker.dll: " + sidecarPath);
            }

            string roslynDirectoryPath = File.ReadAllText(sidecarPath, new UTF8Encoding(false)).Trim();
            if (string.IsNullOrEmpty(roslynDirectoryPath) || !Directory.Exists(roslynDirectoryPath))
            {
                throw new InvalidOperationException("Roslyn directory from sidecar is missing: " + roslynDirectoryPath);
            }

            return roslynDirectoryPath;
        }
    }
}
