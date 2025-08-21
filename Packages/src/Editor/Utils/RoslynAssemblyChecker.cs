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
            return $"Microsoft.CodeAnalysis.CSharp is required for Roslyn features.\n\n" +
                   "Please install it using OpenUPM:\n" +
                   "1. Open Project Settings → Package Manager\n" +
                   "2. Add OpenUPM as Scoped Registry:\n" +
                   "   - Name: OpenUPM\n" +
                   "   - URL: https://package.openupm.com\n" +
                   "   - Scope(s): org.nuget\n" +
                   "3. Open Window → Package Manager\n" +
                   "4. Select 'My Registries' from the dropdown\n" +
                   "5. Search and install 'Microsoft.CodeAnalysis.CSharp' (version 4.14.0 or higher)\n" +
                   "6. See README for detailed instructions.";
        }
    }
}