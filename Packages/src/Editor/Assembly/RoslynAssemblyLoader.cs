#if ULOOPMCP_HAS_ROSLYN
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Roslynアセンブリの動的検出・ロード機能
    /// ULOOPMCP_HAS_ROSLYNが定義されている時のみ有効
    /// </summary>
    public static class RoslynAssemblyLoader
    {
        private static bool _isInitialized;
        private static bool _isRoslynAvailable;
        private static Assembly _codeAnalysisAssembly;
        private static Assembly _codeAnalysisCSharpAssembly;
        
        /// <summary>
        /// Roslynアセンブリが利用可能かチェック
        /// </summary>
        public static bool IsRoslynAvailable
        {
            get
            {
                if (!_isInitialized)
                {
                    Initialize();
                }
                return _isRoslynAvailable;
            }
        }
        
        /// <summary>
        /// Microsoft.CodeAnalysisアセンブリ
        /// </summary>
        public static Assembly CodeAnalysisAssembly => _codeAnalysisAssembly;
        
        /// <summary>
        /// Microsoft.CodeAnalysis.CSharpアセンブリ
        /// </summary>
        public static Assembly CodeAnalysisCSharpAssembly => _codeAnalysisCSharpAssembly;
        
        /// <summary>
        /// Roslynアセンブリの初期化と検出
        /// </summary>
        private static void Initialize()
        {
            _isInitialized = true;
            
            try
            {
                // 既にロードされているアセンブリから検索
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                
                _codeAnalysisAssembly = assemblies.FirstOrDefault(a => 
                    a.GetName().Name == "Microsoft.CodeAnalysis");
                    
                _codeAnalysisCSharpAssembly = assemblies.FirstOrDefault(a => 
                    a.GetName().Name == "Microsoft.CodeAnalysis.CSharp");
                
                // 見つからない場合は動的ロードを試みる
                if (_codeAnalysisAssembly == null)
                {
                    try
                    {
                        _codeAnalysisAssembly = Assembly.Load("Microsoft.CodeAnalysis");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[uLoopMCP] Failed to load Microsoft.CodeAnalysis: {ex.Message}");
                    }
                }
                
                if (_codeAnalysisCSharpAssembly == null)
                {
                    try
                    {
                        _codeAnalysisCSharpAssembly = Assembly.Load("Microsoft.CodeAnalysis.CSharp");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[uLoopMCP] Failed to load Microsoft.CodeAnalysis.CSharp: {ex.Message}");
                    }
                }
                
                _isRoslynAvailable = _codeAnalysisAssembly != null && _codeAnalysisCSharpAssembly != null;
                
                if (_isRoslynAvailable)
                {
                    VibeLogger.LogInfo(
                        "roslyn_assemblies_loaded",
                        "Roslyn assemblies successfully loaded",
                        new
                        {
                            codeAnalysisVersion = _codeAnalysisAssembly.GetName().Version?.ToString(),
                            codeAnalysisCSharpVersion = _codeAnalysisCSharpAssembly.GetName().Version?.ToString()
                        },
                        correlationId: Guid.NewGuid().ToString("N")[..8],
                        humanNote: "Roslyn assemblies detected and loaded",
                        aiTodo: "Monitor Roslyn assembly usage"
                    );
                }
                else
                {
                    VibeLogger.LogWarning(
                        "roslyn_assemblies_not_found",
                        "Roslyn assemblies not found. Please install from NuGet.",
                        new
                        {
                            searchedAssemblies = assemblies.Length,
                            codeAnalysisFound = _codeAnalysisAssembly != null,
                            codeAnalysisCSharpFound = _codeAnalysisCSharpAssembly != null
                        },
                        correlationId: Guid.NewGuid().ToString("N")[..8],
                        humanNote: "Roslyn features disabled due to missing assemblies",
                        aiTodo: "Guide user to install Roslyn from NuGet"
                    );
                }
            }
            catch (Exception ex)
            {
                _isRoslynAvailable = false;
                Debug.LogError($"[uLoopMCP] Error initializing Roslyn assemblies: {ex}");
                
                VibeLogger.LogError(
                    "roslyn_initialization_failed",
                    "Failed to initialize Roslyn assemblies",
                    ex,
                    correlationId: Guid.NewGuid().ToString("N")[..8],
                    humanNote: "Critical error during Roslyn initialization",
                    aiTodo: "Investigate initialization failure"
                );
            }
        }
        
        /// <summary>
        /// Roslynの状態レポートを生成
        /// </summary>
        public static string GetStatusReport()
        {
            if (!_isInitialized)
            {
                Initialize();
            }
            
            if (_isRoslynAvailable)
            {
                return $"Roslyn Status: Available\n" +
                       $"Microsoft.CodeAnalysis: {_codeAnalysisAssembly?.GetName().Version}\n" +
                       $"Microsoft.CodeAnalysis.CSharp: {_codeAnalysisCSharpAssembly?.GetName().Version}";
            }
            else
            {
                return "Roslyn Status: Not Available\n" +
                       "Please install Microsoft.CodeAnalysis from NuGet.\n" +
                       "See README for installation instructions.";
            }
        }
    }
}
#endif