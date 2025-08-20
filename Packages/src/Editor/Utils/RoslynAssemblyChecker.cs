using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Roslynアセンブリの存在確認ユーティリティ
    /// </summary>
    /// <summary>
    /// Roslynアセンブリの存在確認ユーティリティ
    /// </summary>
    public static class RoslynAssemblyChecker
    {
        private const string ROSLYN_ASSEMBLY_NAME = "Microsoft.CodeAnalysis.CSharp";
        private const string REQUIRED_VERSION = "3.11.0";
        
        /// <summary>
        /// Roslynアセンブリが利用可能かチェック
        /// </summary>
        public static bool IsRoslynAvailable()
        {
            try
            {
                // アセンブリが読み込まれているか確認
                Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                Assembly roslynAssembly = loadedAssemblies.FirstOrDefault(a => 
                    a.GetName().Name == ROSLYN_ASSEMBLY_NAME);
                
                if (roslynAssembly != null)
                {
                    return true;
                }
                
                // 型の存在確認（アセンブリがプロジェクトにあるか）
                Type csharpSyntaxType = Type.GetType(
                    "Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree, Microsoft.CodeAnalysis.CSharp",
                    false);
                
                return csharpSyntaxType != null;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        /// <summary>
        /// Roslynアセンブリのバージョン情報を取得
        /// </summary>
        public static string GetRoslynVersion()
        {
            try
            {
                Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                Assembly roslynAssembly = loadedAssemblies.FirstOrDefault(a => 
                    a.GetName().Name == ROSLYN_ASSEMBLY_NAME);
                
                if (roslynAssembly != null)
                {
                    return roslynAssembly.GetName().Version.ToString();
                }
                
                return "Not Installed";
            }
            catch (Exception)
            {
                return "Unknown";
            }
        }
        
        /// <summary>
        /// インストール手順のメッセージを生成
        /// </summary>
        public static string GetInstallationMessage()
        {
            return $"Microsoft.CodeAnalysis.CSharp (v{REQUIRED_VERSION}) is required for Roslyn features.\n\n" +
                   "Please install it using NuGet:\n" +
                   "1. Open NuGet Package Manager\n" +
                   "2. Search for 'Microsoft.CodeAnalysis.CSharp'\n" +
                   $"3. Install version {REQUIRED_VERSION}\n\n" +
                   "See README for detailed instructions.";
        }
    }
}