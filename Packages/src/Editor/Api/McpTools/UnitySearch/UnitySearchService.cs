using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Service layer for Unity Search API integration
    /// Handles search execution, result conversion, and filtering
    /// Related classes:
    /// - UnitySearchCommand: Main command handler that uses this service
    /// - SearchResultItem: Data structure for converted search results
    /// - SearchResultExporter: File export functionality
    /// </summary>
    public static class UnitySearchService
    {
        /// <summary>
        /// Execute Unity search with the specified parameters
        /// </summary>
        /// <param name="schema">Search parameters</param>
        /// <returns>Search results or file path if exported</returns>
        public static async Task<UnitySearchResponse> ExecuteSearchAsync(UnitySearchSchema schema)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Validate search query
                if (string.IsNullOrWhiteSpace(schema.SearchQuery))
                {
                    return new UnitySearchResponse("Search query cannot be empty", schema.SearchQuery);
                }

                // Create search context
                SearchContext context = CreateSearchContext(schema);
                if (context == null)
                {
                    return new UnitySearchResponse("Failed to create search context", schema.SearchQuery);
                }

                // Execute search
                List<SearchItem> searchItems = await ExecuteUnitySearchAsync(context, schema);
                
                // Convert Unity SearchItems to our SearchResultItems
                SearchResultItem[] results = ConvertSearchItems(searchItems, schema);
                
                // Apply additional filtering
                results = ApplyFiltering(results, schema);
                
                // Apply result limit
                if (results.Length > schema.MaxResults)
                {
                    results = results.Take(schema.MaxResults).ToArray();
                }

                stopwatch.Stop();
                
                // Get providers used
                string[] providersUsed = GetProvidersUsed(context);
                
                // Determine if we should save to file
                bool shouldSaveToFile = ShouldSaveToFile(results, schema);
                
                if (shouldSaveToFile)
                {
                    return CreateFileBasedResponse(results, schema, providersUsed, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    return new UnitySearchResponse(results, searchItems.Count, schema.SearchQuery, 
                                                 providersUsed, stopwatch.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new UnitySearchResponse($"Search failed: {ex.Message}", schema.SearchQuery);
            }
        }

        /// <summary>
        /// Create Unity Search context based on schema parameters
        /// </summary>
        private static SearchContext CreateSearchContext(UnitySearchSchema schema)
        {
            SearchContext context;
            
            if (schema.Providers != null && schema.Providers.Length > 0)
            {
                // Use specific providers
                context = SearchService.CreateContext(schema.Providers, schema.SearchQuery, 
                                                     ConvertSearchFlags(schema.SearchFlags));
            }
            else
            {
                // Use all active providers
                context = SearchService.CreateContext(schema.SearchQuery, ConvertSearchFlags(schema.SearchFlags));
            }

            return context;
        }

        /// <summary>
        /// Execute Unity search asynchronously
        /// </summary>
        private static async Task<List<SearchItem>> ExecuteUnitySearchAsync(SearchContext context, UnitySearchSchema schema)
        {
            TaskCompletionSource<List<SearchItem>> tcs = new TaskCompletionSource<List<SearchItem>>();
            List<SearchItem> allItems = new List<SearchItem>();

            // Use SearchService.Request with callback
            SearchService.Request(context, 
                (ctx, items) => {
                    // This is called when search completes
                    allItems.AddRange(items);
                    if (!tcs.Task.IsCompleted)
                    {
                        tcs.SetResult(allItems);
                    }
                }, 
                ConvertSearchFlags(schema.SearchFlags));

            // Add timeout
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(schema.TimeoutSeconds));
            Task completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                throw new TimeoutException($"Search timed out after {schema.TimeoutSeconds} seconds");
            }

            return await tcs.Task;
        }

        /// <summary>
        /// Convert Unity SearchItems to our SearchResultItems
        /// </summary>
        private static SearchResultItem[] ConvertSearchItems(List<SearchItem> searchItems, UnitySearchSchema schema)
        {
            List<SearchResultItem> results = new List<SearchResultItem>();

            foreach (SearchItem item in searchItems)
            {
                SearchResultItem result = new SearchResultItem
                {
                    Id = item.id ?? "",
                    Label = item.label ?? "",
                    Description = schema.IncludeDescription ? (item.description ?? "") : "",
                    Provider = item.provider?.id ?? "",
                    Type = GetItemType(item),
                    Path = GetItemPath(item),
                    Score = item.score,
                    Thumbnail = "",
                    Tags = GetItemTags(item),
                    IsSelectable = true
                };

                // Add metadata if requested
                if (schema.IncludeMetadata)
                {
                    AddMetadata(result, item);
                }

                results.Add(result);
            }

            return results.ToArray();
        }

        /// <summary>
        /// Apply additional filtering based on schema parameters
        /// </summary>
        private static SearchResultItem[] ApplyFiltering(SearchResultItem[] results, UnitySearchSchema schema)
        {
            IEnumerable<SearchResultItem> filtered = results;

            // Filter by file extensions
            if (schema.FileExtensions != null && schema.FileExtensions.Length > 0)
            {
                filtered = filtered.Where(r => 
                {
                    string ext = Path.GetExtension(r.Path)?.TrimStart('.');
                    return !string.IsNullOrEmpty(ext) && schema.FileExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
                });
            }

            // Filter by asset types
            if (schema.AssetTypes != null && schema.AssetTypes.Length > 0)
            {
                filtered = filtered.Where(r => 
                    schema.AssetTypes.Contains(r.Type, StringComparer.OrdinalIgnoreCase));
            }

            // Filter by path pattern
            if (!string.IsNullOrWhiteSpace(schema.PathFilter))
            {
                string pattern = schema.PathFilter.Replace("*", ".*").Replace("?", ".");
                filtered = filtered.Where(r => 
                    System.Text.RegularExpressions.Regex.IsMatch(r.Path, pattern, 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase));
            }

            return filtered.ToArray();
        }

        /// <summary>
        /// Determine if results should be saved to file
        /// </summary>
        private static bool ShouldSaveToFile(SearchResultItem[] results, UnitySearchSchema schema)
        {
            // User explicitly requested file save
            if (schema.SaveToFile)
                return true;

            // Auto-save threshold exceeded
            if (schema.AutoSaveThreshold > 0 && results.Length > schema.AutoSaveThreshold)
                return true;

            return false;
        }

        /// <summary>
        /// Create file-based response when results are saved to file
        /// </summary>
        private static UnitySearchResponse CreateFileBasedResponse(SearchResultItem[] results, 
                                                                  UnitySearchSchema schema, 
                                                                  string[] providersUsed, 
                                                                  long searchDurationMs)
        {
            try
            {
                string filePath = SearchResultExporter.ExportSearchResults(results, schema.OutputFormat, 
                                                                          schema.SearchQuery, providersUsed);
                
                string saveReason = schema.SaveToFile ? "user_request" : "auto_threshold";
                
                return new UnitySearchResponse(filePath, schema.OutputFormat.ToString(), saveReason,
                                             results.Length, schema.SearchQuery, providersUsed, searchDurationMs);
            }
            catch (Exception)
            {
                // Fallback to inline response
                return new UnitySearchResponse(results, results.Length, schema.SearchQuery, 
                                             providersUsed, searchDurationMs);
            }
        }

        /// <summary>
        /// Convert our search flags to Unity search flags
        /// </summary>
        private static SearchFlags ConvertSearchFlags(UnitySearchFlags flags)
        {
            SearchFlags unityFlags = SearchFlags.Default;

            if (flags.HasFlag(UnitySearchFlags.Synchronous))
                unityFlags |= SearchFlags.Synchronous;
            if (flags.HasFlag(UnitySearchFlags.WantsMore))
                unityFlags |= SearchFlags.WantsMore;
            if (flags.HasFlag(UnitySearchFlags.Packages))
                unityFlags |= SearchFlags.Packages;
            if (flags.HasFlag(UnitySearchFlags.Sorted))
                unityFlags |= SearchFlags.Sorted;

            return unityFlags;
        }

        /// <summary>
        /// Get item type from Unity SearchItem
        /// </summary>
        private static string GetItemType(SearchItem item)
        {
            // Try to get type from item data
            if (item.data is UnityEngine.Object obj)
            {
                return obj.GetType().Name;
            }

            // Fallback to provider-based type detection
            return item.provider?.id switch
            {
                "asset" => "Asset",
                "scene" => "GameObject",
                "menu" => "MenuItem",
                "settings" => "Setting",
                "packages" => "Package",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Get item path from Unity SearchItem
        /// </summary>
        private static string GetItemPath(SearchItem item)
        {
            // Try various ways to get the path
            if (!string.IsNullOrEmpty(item.description) && item.description.Contains("/"))
                return item.description;

            if (item.data is UnityEngine.Object obj)
            {
                string assetPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(assetPath))
                    return assetPath;
            }

            return item.id ?? "";
        }

        /// <summary>
        /// Get item tags from Unity SearchItem
        /// </summary>
        private static string[] GetItemTags(SearchItem item)
        {
            List<string> tags = new List<string>();

            // Add provider as tag
            if (!string.IsNullOrEmpty(item.provider?.id))
                tags.Add(item.provider.id);

            // Add other contextual tags based on item properties
            if (item.data is UnityEngine.Object obj)
            {
                tags.Add(obj.GetType().Name);
            }

            return tags.ToArray();
        }

        /// <summary>
        /// Add metadata to search result item
        /// </summary>
        private static void AddMetadata(SearchResultItem result, SearchItem item)
        {
            if (item.data is UnityEngine.Object obj)
            {
                string assetPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(assetPath) && File.Exists(assetPath))
                {
                    FileInfo fileInfo = new FileInfo(assetPath);
                    result.FileSize = fileInfo.Length;
                    result.LastModified = fileInfo.LastWriteTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                }
            }
        }

        /// <summary>
        /// Get list of providers that were used in the search
        /// </summary>
        private static string[] GetProvidersUsed(SearchContext context)
        {
            if (context?.providers == null)
                return Array.Empty<string>();

            return context.providers.Select(p => p.id).ToArray();
        }

        /// <summary>
        /// Get list of available search providers
        /// </summary>
        public static string[] GetAvailableProviders()
        {
            return SearchService.Providers.Select(p => p.id).ToArray();
        }

        /// <summary>
        /// Get detailed information about all available search providers
        /// </summary>
        public static ProviderInfo[] GetProviderDetails()
        {
            return SearchService.Providers.Select(p => new ProviderInfo
            {
                Id = p.id,
                DisplayName = p.name ?? p.id,
                Description = GetProviderDescription(p),
                IsActive = p.active,
                Priority = p.priority,
                FilterId = p.filterId ?? "",
                ShowDetails = p.showDetails,
                ShowDetailsOptions = p.showDetailsOptions.ToString(),
                SupportedTypes = new[] { p.type },
                ActionCount = p.actions?.Count ?? 0
            }).ToArray();
        }

        /// <summary>
        /// Get detailed information about a specific search provider
        /// </summary>
        public static ProviderInfo GetProviderDetails(string providerId)
        {
            SearchProvider provider = SearchService.Providers.FirstOrDefault(p => p.id == providerId);
            if (provider == null)
            {
                return null;
            }

            return new ProviderInfo
            {
                Id = provider.id,
                DisplayName = provider.name ?? provider.id,
                Description = GetProviderDescription(provider),
                IsActive = provider.active,
                Priority = provider.priority,
                FilterId = provider.filterId ?? "",
                ShowDetails = provider.showDetails,
                ShowDetailsOptions = provider.showDetailsOptions.ToString(),
                SupportedTypes = new[] { provider.type },
                ActionCount = provider.actions?.Count ?? 0
            };
        }

        /// <summary>
        /// Generate description for a search provider based on its properties
        /// </summary>
        private static string GetProviderDescription(SearchProvider provider)
        {
            return provider.id switch
            {
                "asset" => "Search project assets and files",
                "scene" => "Search objects in the current scene hierarchy",
                "menu" => "Search Unity menu items and commands",
                "settings" => "Search Unity settings and preferences",
                "packages" => "Search available Unity packages",
                "log" => "Search Unity console logs",
                "find" => "Search files in the project directory",
                "adb" => "Search using Unity Asset Database",
                "store" => "Search Unity Asset Store",
                "calculator" => "Perform mathematical calculations",
                "performance" => "Search performance tracking data",
                "profilermarkers" => "Search Unity Profiler markers",
                "static_methods" => "Search static API methods",
                _ => $"Search using {provider.name ?? provider.id} provider"
            };
        }

        /// <summary>
        /// Clean up old export files
        /// </summary>
        public static void CleanupOldExports()
        {
            SearchResultExporter.CleanupOldExports();
        }
    }
} 