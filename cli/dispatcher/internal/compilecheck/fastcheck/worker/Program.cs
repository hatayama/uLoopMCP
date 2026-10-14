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
        private const string FingerprintCommandName = "fingerprint";
        private const string CheckCommandName = "check";
        private const string RoslynDirectorySidecarFileName = "roslyn-directory.txt";

        private static int Main(string[] args)
        {
            if (args.Length != 3 || (args[0] != FingerprintCommandName && args[0] != CheckCommandName))
            {
                Console.Error.WriteLine("usage: worker (fingerprint|check) <in.json> <out.json>");
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

        // Kept out of Main so no Roslyn type is loaded before the resolver is registered.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RunCommand(string command, string inputPath, string outputPath)
        {
            if (command == FingerprintCommandName)
            {
                FingerprintRequest fingerprintRequest = ReadJson<FingerprintRequest>(inputPath);
                WriteJson(outputPath, FingerprintCommand.Run(fingerprintRequest));
                return;
            }

            CheckRequest checkRequest = ReadJson<CheckRequest>(inputPath);
            WriteJson(outputPath, ChangedFileChecker.Check(checkRequest));
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
