using System;
using System.Collections.Generic;
using System.IO;
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

            string contentsPath = editorPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(editorPath, "Contents")
                : Path.GetDirectoryName(Path.GetDirectoryName(editorPath));
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
            string[] runtimeDirectories = Directory.Exists(netCoreRuntimeSharedRootPath)
                ? Directory.GetDirectories(netCoreRuntimeSharedRootPath)
                : Array.Empty<string>();
            string netCoreRuntimeSharedDirectoryPath = runtimeDirectories.Length > 0
                ? runtimeDirectories[0]
                : null;

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
    }
}
