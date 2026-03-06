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
    /// Manages the caching of compilation results
    /// Separated cache management responsibility from RoslynCompiler
    /// 
    /// Cache Responsibilities:
    /// - Avoid recompiling the same code (performance improvement)
    /// - Generate cache keys using SHA256 hash
    /// - Note: Independent of reference assembly list caching
    /// 
    /// Related Classes:
    /// - RoslynCompiler: Compiler that uses this cache manager
    /// - CompilationRequest: Used for cache key generation
    /// - CompilationResult: Results to be cached
    /// </summary>
    public class CompilationCacheManager
    {
        // Compiled assembly cache (key: SHA256 hash of the code)
        private readonly Dictionary<string, Assembly> _compilationCache = new();
#if ULOOPMCP_HAS_ROSLYN
        // Reference cache for different dynamic code security levels
        private readonly Dictionary<DynamicCodeSecurityLevel, List<MetadataReference>> _referenceCache = new();
#endif

        /// <summary>
        /// Retrieve results from cache
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
        /// Save successful results to cache (based on CompilationRequest)
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
        /// Public method to clear the cache
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
        /// Clear the reference cache
        /// </summary>
        public void ClearReferenceCache()
        {
#if ULOOPMCP_HAS_ROSLYN
            _referenceCache.Clear();
#endif
        }

        /// <summary>
        /// Generate cache key from CompilationRequest
        /// </summary>
        public string GenerateCacheKey(CompilationRequest request)
        {
            // Generate key by combining code, class name, namespace, and additional references
            StringBuilder keyBuilder = new StringBuilder();
            keyBuilder.Append(request.Code);
            keyBuilder.Append("|");
            keyBuilder.Append(request.ClassName ?? "");
            keyBuilder.Append("|");
            keyBuilder.Append(request.Namespace ?? "");
            
            // Include additional references if present
            if (request.AdditionalReferences != null && request.AdditionalReferences.Any())
            {
                keyBuilder.Append("|");
                keyBuilder.Append(string.Join(",", request.AdditionalReferences.OrderBy(r => r)));
            }
            
            // Hash with SHA256
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyBuilder.ToString()));
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}