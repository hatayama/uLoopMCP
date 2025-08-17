using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// アセンブリがユーザー定義かどうかを判定するクラス
    /// 設計ドキュメント参照: working-notes/2025-08-16_Restrictedモードユーザークラス実行機能_design.md
    /// 関連クラス: AssemblyReferencePolicy, SecurityValidator, RoslynCompiler
    /// </summary>
    public static class AssemblyClassifier
    {
        /// <summary>
        /// アセンブリがユーザー定義かどうかを判定
        /// </summary>
        public static bool IsUserDefinedAssembly(Assembly assembly)
        {
            if (assembly == null) return false;
            
            string assemblyName = assembly.GetName().Name;
            string location = assembly.Location;
            
            // 判定基準1: Assembly-CSharp系
            if (IsAssemblyCSharpVariant(assemblyName))
            {
                return true;
            }
            
            // 判定基準2: Assets/フォルダ内のasmdef
            if (IsInAssetsFolder(location))
            {
                return true;
            }
            
            // 判定基準3: Library/ScriptAssemblies内
            if (IsInScriptAssemblies(location))
            {
                return true;
            }
            
            // 判定基準4: ローカルパッケージ
            if (IsLocalPackageAssembly(assembly))
            {
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// アセンブリ名でユーザー定義かどうかを判定（簡易版）
        /// </summary>
        public static bool IsUserDefinedAssemblyByName(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName)) return false;
            
            // Assembly-CSharp系の判定
            if (IsAssemblyCSharpVariant(assemblyName))
            {
                return true;
            }
            
            // 完全な判定が必要な場合はAssemblyオブジェクトを取得して判定
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);
            
            if (assembly != null)
            {
                return IsUserDefinedAssembly(assembly);
            }
            
            return false;
        }
        
        private static bool IsAssemblyCSharpVariant(string assemblyName)
        {
            string[] variants = {
                "Assembly-CSharp",
                "Assembly-CSharp-Editor",
                "Assembly-CSharp-firstpass",
                "Assembly-CSharp-Editor-firstpass"
            };
            
            return variants.Any(v => assemblyName.Equals(v, StringComparison.OrdinalIgnoreCase));
        }
        
        private static bool IsInAssetsFolder(string location)
        {
            if (string.IsNullOrEmpty(location)) return false;
            
            // Unity プロジェクトパスを正規化
            string projectPath = Application.dataPath.Replace("/Assets", "");
            string normalizedLocation = Path.GetFullPath(location);
            string assetsPath = Path.Combine(projectPath, "Assets");
            
            return normalizedLocation.StartsWith(assetsPath, StringComparison.OrdinalIgnoreCase);
        }
        
        private static bool IsInScriptAssemblies(string location)
        {
            if (string.IsNullOrEmpty(location)) return false;
            
            return location.Contains("Library/ScriptAssemblies", StringComparison.OrdinalIgnoreCase);
        }
        
        private static bool IsLocalPackageAssembly(Assembly assembly)
        {
            // カスタム属性でローカルパッケージを識別
            object[] attributes = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false);
            foreach (object attr in attributes)
            {
                if (attr is AssemblyMetadataAttribute metaAttr)
                {
                    if (metaAttr.Key == "PackageSource" && metaAttr.Value == "Local")
                    {
                        return true;
                    }
                }
            }
            
            // パスベースの判定
            string location = assembly.Location;
            if (!string.IsNullOrEmpty(location))
            {
                // Packages/フォルダ内かつfile://プロトコル
                if (location.Contains("Packages/") && 
                    (location.Contains("file://") || location.Contains("Library/PackageCache")))
                {
                    return false; // リモートパッケージ
                }
                
                if (location.Contains("Packages/") && !location.Contains("Library/PackageCache"))
                {
                    return true; // ローカルパッケージ
                }
            }
            
            return false;
        }
    }
}