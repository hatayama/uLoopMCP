using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace UnityCliLoop.CompileCheckWorker
{
    /// <summary>
    /// Fingerprints every source of an assembly right after its full compile, to record the
    /// baseline a later changed-files check compares against.
    /// </summary>
    internal static class FingerprintCommand
    {
        internal static FingerprintResponse Run(FingerprintRequest request)
        {
            CSharpCommandLineArguments arguments = ResponseFileCompilationLoader.ParseArguments(request.ProjectRoot, request.ResponseFilePath);
            string argumentError = ResponseFileCompilationLoader.FirstArgumentError(arguments);
            if (argumentError != null)
            {
                throw new InvalidOperationException("response file: " + argumentError);
            }

            string[] sources = request.Sources.ToArray();
            string[] fingerprints = new string[sources.Length];
            string[] contentHashes = new string[sources.Length];
            Parallel.For(0, sources.Length, index =>
            {
                // One read: the hash must describe the exact bytes the fingerprint was computed from.
                byte[] bytes = File.ReadAllBytes(sources[index]);
                contentHashes[index] = DeclarationFingerprint.Sha256Hex(bytes);
                SyntaxTree tree = ResponseFileCompilationLoader.ParseSource(sources[index], bytes, arguments);
                fingerprints[index] = DeclarationFingerprint.Compute(tree);
            });

            FingerprintResponse response = new FingerprintResponse();
            for (int index = 0; index < sources.Length; index++)
            {
                response.Fingerprints[sources[index]] = fingerprints[index];
                response.ContentHashes[sources[index]] = contentHashes[index];
            }

            return response;
        }
    }
}
