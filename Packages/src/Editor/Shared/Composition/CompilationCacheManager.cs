using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Manages the caching of compilation results.
    /// Avoids recompiling the same code by hashing requests with SHA256.
    /// </summary>
    public class CompilationCacheManager
    {
        private const int MaxCacheEntries = 32;
        private readonly Dictionary<string, Assembly> _compilationCache = new();
        private readonly Queue<string> _cacheOrder = new();

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

        public void CacheResultIfSuccessful(CompilationResult result, CompilationRequest request)
        {
            if (result.Success && result.CompiledAssembly != null)
            {
                string cacheKey = GenerateCacheKey(request);
                if (_compilationCache.ContainsKey(cacheKey))
                {
                    _compilationCache[cacheKey] = result.CompiledAssembly;
                    return;
                }

                // Dynamic assemblies cannot be unloaded from the default AppDomain,
                // so the cache keeps only a small hot set instead of retaining every snippet forever.
                if (_compilationCache.Count >= MaxCacheEntries)
                {
                    string oldestKey = _cacheOrder.Dequeue();
                    _compilationCache.Remove(oldestKey);
                }

                _compilationCache[cacheKey] = result.CompiledAssembly;
                _cacheOrder.Enqueue(cacheKey);
            }
        }

        public void ClearCache()
        {
            _compilationCache.Clear();
            _cacheOrder.Clear();
        }

        public string GenerateCacheKey(CompilationRequest request)
        {
            StringBuilder keyBuilder = new StringBuilder();
            keyBuilder.Append(request.Code);
            keyBuilder.Append("|");
            keyBuilder.Append(request.ClassName ?? "");
            keyBuilder.Append("|");
            keyBuilder.Append(request.Namespace ?? "");

            if (request.AdditionalReferences != null && request.AdditionalReferences.Any())
            {
                keyBuilder.Append("|");
                keyBuilder.Append(string.Join(",", request.AdditionalReferences.OrderBy(r => r)));
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyBuilder.ToString()));
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}
