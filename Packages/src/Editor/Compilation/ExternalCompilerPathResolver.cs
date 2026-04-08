using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    internal static class ExternalCompilerPathResolver
    {
        public static ExternalCompilerPaths Resolve()
        {
            string editorPath = EditorApplication.applicationPath;
            if (string.IsNullOrEmpty(editorPath))
            {
                return null;
            }

            string contentsPath = ResolveEditorContentsPath(editorPath);
            if (string.IsNullOrEmpty(contentsPath))
            {
                return null;
            }

            string dotnetHostFileName = UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WindowsEditor
                ? "dotnet.exe"
                : "dotnet";
            string dotnetHostPath = Path.Combine(contentsPath, "NetCoreRuntime", dotnetHostFileName);
            string compilerDllPath = Path.Combine(contentsPath, "DotNetSdkRoslyn", "csc.dll");
            string compilerRuntimeConfigPath = Path.Combine(contentsPath, "DotNetSdkRoslyn", "csc.runtimeconfig.json");
            string compilerDepsFilePath = Path.Combine(contentsPath, "DotNetSdkRoslyn", "csc.deps.json");
            string codeAnalysisDllPath = Path.Combine(contentsPath, "DotNetSdkRoslyn", "Microsoft.CodeAnalysis.dll");
            string codeAnalysisCSharpDllPath = Path.Combine(contentsPath, "DotNetSdkRoslyn", "Microsoft.CodeAnalysis.CSharp.dll");
            string netCoreRuntimeSharedRootPath = Path.Combine(contentsPath, "NetCoreRuntime", "shared", "Microsoft.NETCore.App");
            string netCoreRuntimeSharedDirectoryPath = ResolveNetCoreRuntimeSharedDirectoryPath(netCoreRuntimeSharedRootPath);

            List<string> missingComponents = new List<string>();
            if (!File.Exists(dotnetHostPath))
            {
                missingComponents.Add(dotnetHostPath);
            }

            if (!File.Exists(compilerDllPath))
            {
                missingComponents.Add(compilerDllPath);
            }

            if (!File.Exists(compilerRuntimeConfigPath))
            {
                missingComponents.Add(compilerRuntimeConfigPath);
            }

            if (!File.Exists(compilerDepsFilePath))
            {
                missingComponents.Add(compilerDepsFilePath);
            }

            if (!File.Exists(codeAnalysisDllPath))
            {
                missingComponents.Add(codeAnalysisDllPath);
            }

            if (!File.Exists(codeAnalysisCSharpDllPath))
            {
                missingComponents.Add(codeAnalysisCSharpDllPath);
            }

            if (string.IsNullOrEmpty(netCoreRuntimeSharedDirectoryPath))
            {
                missingComponents.Add(netCoreRuntimeSharedRootPath);
            }

            if (missingComponents.Count > 0)
            {
                DynamicCompilationHealthMonitor.ReportFastPathUnavailable(
                    editorPath,
                    contentsPath,
                    missingComponents);
                return null;
            }

            return new ExternalCompilerPaths(
                contentsPath,
                dotnetHostPath,
                compilerDllPath,
                compilerRuntimeConfigPath,
                compilerDepsFilePath,
                codeAnalysisDllPath,
                codeAnalysisCSharpDllPath,
                netCoreRuntimeSharedDirectoryPath);
        }

        internal static string ResolveNetCoreRuntimeSharedDirectoryPath(string netCoreRuntimeSharedRootPath)
        {
            if (!Directory.Exists(netCoreRuntimeSharedRootPath))
            {
                return null;
            }

            string[] runtimeDirectories = Directory.GetDirectories(netCoreRuntimeSharedRootPath);
            if (runtimeDirectories.Length == 0)
            {
                return null;
            }

            string highestVersionDirectoryPath = runtimeDirectories
                .Select(runtimeDirectoryPath => new
                {
                    Path = runtimeDirectoryPath,
                    VersionText = Path.GetFileName(runtimeDirectoryPath)
                })
                .Where(candidate => Version.TryParse(candidate.VersionText, out _))
                .OrderByDescending(candidate => new Version(candidate.VersionText))
                .ThenByDescending(candidate => candidate.VersionText, StringComparer.Ordinal)
                .Select(candidate => candidate.Path)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(highestVersionDirectoryPath))
            {
                return highestVersionDirectoryPath;
            }

            return runtimeDirectories
                .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
                .First();
        }

        private static string ResolveEditorContentsPath(string editorPath)
        {
            if (editorPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(editorPath, "Contents");
            }

            string editorDirectoryPath = Path.GetDirectoryName(editorPath);
            if (string.IsNullOrEmpty(editorDirectoryPath))
            {
                return null;
            }

            string dataDirectoryPath = Path.Combine(editorDirectoryPath, "Data");
            if (Directory.Exists(dataDirectoryPath))
            {
                return dataDirectoryPath;
            }

            string installRootPath = Path.GetDirectoryName(editorDirectoryPath);
            return string.IsNullOrEmpty(installRootPath)
                ? null
                : installRootPath;
        }
    }
}
