using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
#if ULOOPMCP_HAS_ROSLYN
using Microsoft.CodeAnalysis;
#endif

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// コンパイル結果のキャッシュ管理を担当するクラス
    /// RoslynCompilerから分離されたキャッシュ管理責務
    /// 
    /// 関連クラス:
    /// - RoslynCompiler: このキャッシュマネージャーを使用するコンパイラ
    /// - CompilationRequest: キャッシュキー生成に使用
    /// - CompilationResult: キャッシュされる結果
    /// </summary>
    public class CompilationCacheManager
    {
        private readonly Dictionary<string, Assembly> _compilationCache = new();
#if ULOOPMCP_HAS_ROSLYN
        private readonly Dictionary<DynamicCodeSecurityLevel, List<MetadataReference>> _referenceCache = new();
#endif

        /// <summary>
        /// キャッシュから結果を取得
        /// </summary>
        public CompilationResult CheckCache(CompilationRequest request)
        {
            string cacheKey = GenerateCacheKey(request);
            if (_compilationCache.TryGetValue(cacheKey, out Assembly cachedAssembly))
            {
                return new CompilationResult
                {
                    Success = true,
                    CompiledAssembly = cachedAssembly,
                    UpdatedCode = request.Code
                };
            }

            return null;
        }

        /// <summary>
        /// 成功した結果をキャッシュに保存
        /// </summary>
        public void CacheResultIfSuccessful(CompilationResult result, string cacheKey)
        {
            if (result.Success && result.CompiledAssembly != null)
            {
                _compilationCache[cacheKey] = result.CompiledAssembly;
            }
        }

        /// <summary>
        /// 成功した結果をキャッシュに保存（CompilationRequestベース）
        /// </summary>
        public void CacheResultIfSuccessful(CompilationResult result, CompilationRequest request)
        {
            if (result.Success && result.CompiledAssembly != null)
            {
                string cacheKey = GenerateCacheKey(request);
                _compilationCache[cacheKey] = result.CompiledAssembly;
            }
        }

        /// <summary>
        /// 公開用のキャッシュクリアメソッド
        /// </summary>
        public void ClearCache()
        {
            _compilationCache.Clear();
            VibeLogger.LogInfo(
                "roslyn_cache_cleared",
                "Compilation cache cleared",
                new { },
                correlationId: McpConstants.GenerateCorrelationId(),
                humanNote: "Compilation cache was cleared",
                aiTodo: "Monitor cache usage patterns"
            );
        }

        /// <summary>
        /// セキュリティレベル変更時のキャッシュクリア
        /// </summary>
        public void ClearCompilationCache()
        {
            _compilationCache.Clear();
            
            VibeLogger.LogInfo(
                "compilation_cache_cleared",
                "Compilation cache cleared due to security level change",
                new { 
                    cacheSize = _compilationCache.Count 
                },
                correlationId: McpConstants.GenerateCorrelationId(),
                humanNote: "Cache cleared after security level change",
                aiTodo: "Monitor cache invalidation frequency"
            );
        }

#if ULOOPMCP_HAS_ROSLYN
        /// <summary>
        /// セキュリティレベル用の参照キャッシュを取得または作成
        /// </summary>
        public List<MetadataReference> GetOrCreateReferences(DynamicCodeSecurityLevel level, Func<List<MetadataReference>> factory)
        {
            if (!_referenceCache.TryGetValue(level, out List<MetadataReference> references))
            {
                references = factory();
                _referenceCache[level] = references;
            }
            return references;
        }
#endif

        /// <summary>
        /// 参照キャッシュをクリア
        /// </summary>
        public void ClearReferenceCache()
        {
#if ULOOPMCP_HAS_ROSLYN
            _referenceCache.Clear();
#endif
        }

        /// <summary>
        /// CompilationRequestからキャッシュキーを生成
        /// </summary>
        public string GenerateCacheKey(CompilationRequest request)
        {
            // コード、クラス名、名前空間、追加参照を組み合わせてキーを生成
            StringBuilder keyBuilder = new StringBuilder();
            keyBuilder.Append(request.Code);
            keyBuilder.Append("|");
            keyBuilder.Append(request.ClassName ?? "");
            keyBuilder.Append("|");
            keyBuilder.Append(request.Namespace ?? "");
            
            // 追加参照がある場合は含める
            if (request.AdditionalReferences != null && request.AdditionalReferences.Any())
            {
                keyBuilder.Append("|");
                keyBuilder.Append(string.Join(",", request.AdditionalReferences.OrderBy(r => r)));
            }
            
            // SHA256でハッシュ化
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyBuilder.ToString()));
                return Convert.ToBase64String(hashBytes);
            }
        }

        /// <summary>
        /// キャッシュ統計情報を取得
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            return new CacheStatistics
            {
                CompilationCacheCount = _compilationCache.Count,
#if ULOOPMCP_HAS_ROSLYN
                ReferenceCacheCount = _referenceCache.Count,
                TotalCacheSize = _compilationCache.Count + _referenceCache.Count
#else
                ReferenceCacheCount = 0,
                TotalCacheSize = _compilationCache.Count
#endif
            };
        }
    }

    /// <summary>
    /// キャッシュ統計情報
    /// </summary>
    public class CacheStatistics
    {
        public int CompilationCacheCount { get; set; }
        public int ReferenceCacheCount { get; set; }
        public int TotalCacheSize { get; set; }
    }
}