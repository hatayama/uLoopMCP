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

            return new UnitySearchResponse(results, searchItems.Count, schema.SearchQuery,
                                           providersUsed, stopwatch.ElapsedMilliseconds);
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
            TaskCompletionSource<List<SearchItem>> completionSource = CreateCompletionSource();
            SearchResultCollector collector = new SearchResultCollector();

            SearchService.Request(
                context,
                (ctx, items) => HandleSearchCallback(items, collector, completionSource),
                ConvertSearchFlags(schema.SearchFlags));

            List<SearchItem> results = await completionSource.Task.ConfigureAwait(false);
            return results;
        }

        private static TaskCompletionSource<List<SearchItem>> CreateCompletionSource()
        {
            TaskCompletionSource<List<SearchItem>> completionSource =
                new TaskCompletionSource<List<SearchItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
            return completionSource;
        }

        private static void HandleSearchCallback(
            IList<SearchItem> items,
            SearchResultCollector collector,
            TaskCompletionSource<List<SearchItem>> completionSource)
        {
            try
            {
                List<SearchItem> snapshot = collector.TryCollect(items);
                if (snapshot != null)
                {
                    completionSource.TrySetResult(snapshot);
                }
            }
            catch (Exception ex)
            {
                completionSource.TrySetException(ex);
            }
        }

        private sealed class SearchResultCollector
        {
            private readonly List<SearchItem> items = new List<SearchItem>();
            private readonly object syncRoot = new object();
            private bool isHandled;

            public List<SearchItem> TryCollect(IList<SearchItem> newItems)
            {
                lock (syncRoot)
                {
                    if (isHandled)
                    {
                        return null;
                    }

                    if (newItems != null)
                    {
                        items.AddRange(newItems);
                    }

                    isHandled = true;
                    return new List<SearchItem>(items);
                }
            }
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

                // Optionally attach common fields and asset-specific details
                if (schema.IncludeMetadata)
                {
                    AddCommonProperties(result, item);
                    AddAssetSpecificProperties(result, item);
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
            string filePath = SearchResultExporter.ExportSearchResults(results, schema.OutputFormat,
                                                                        schema.SearchQuery, providersUsed);

            string saveReason = schema.SaveToFile ? "user_request" : "auto_threshold";

            return new UnitySearchResponse(filePath, schema.OutputFormat.ToString(), saveReason,
                                           results.Length, schema.SearchQuery, providersUsed, searchDurationMs);
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
        /// Improve path resolution so GlobalObjectId values are reliably handled
        /// </summary>
        private static string GetItemPath(SearchItem item)
        {
            // 1. Attempt to resolve the path via the object reference
            if (item.data is UnityEngine.Object obj)
            {
                string assetPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(assetPath))
                    return assetPath;
            }

            // 2. Resolve the GlobalObjectId into a project asset path
            if (!string.IsNullOrEmpty(item.id) && item.id.StartsWith("GlobalObjectId_"))
            {
                string assetPath = ConvertGlobalObjectIdToPath(item.id);
                if (!string.IsNullOrEmpty(assetPath))
                    return assetPath;
            }

            // 3. Treat the description as a path when it looks like one
            if (!string.IsNullOrEmpty(item.description) && item.description.Contains("/"))
                return item.description;

            // 4. Fallback: return item.id as-is
            return item.id ?? "";
        }

        /// <summary>
        /// Convert a GlobalObjectId into an asset path
        /// Format: GlobalObjectId_V1-{type}-{guid}-{localId}-{prefabId}
        /// </summary>
        private static string ConvertGlobalObjectIdToPath(string globalObjectId)
        {
            if (string.IsNullOrEmpty(globalObjectId))
                return string.Empty;

            // Format resembles GlobalObjectId_V1-1-ec10ac4d5b9a745b1a9c82614fc533d6-2800000-0
            string[] parts = globalObjectId.Split('-');
            if (parts.Length < 3)
                return string.Empty;

            string guid = parts[2];
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(assetPath))
                return assetPath;

            return string.Empty;
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
        /// Retrieve a UnityEngine.Object from a SearchItem
        /// Unity Search's asset provider sometimes omits direct object instances, so conversion is required
        /// </summary>
        private static UnityEngine.Object GetObjectFromSearchItem(SearchItem item)
        {
            // 1. item.data already holds a UnityEngine.Object
            if (item.data is UnityEngine.Object obj)
                return obj;

            // 2. When the provider is asset, prefer resolving via GlobalObjectId
            // (SearchUtils.ToObject does not exist)

            // 3. Resolve through the GUID extracted from the GlobalObjectId
            if (!string.IsNullOrEmpty(item.id) && item.id.StartsWith("GlobalObjectId_"))
            {
                string assetPath = ConvertGlobalObjectIdToPath(item.id);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                }
            }

            return null;
        }

        /// <summary>
        /// Add common properties shared across all assets
        /// </summary>
        private static void AddCommonProperties(SearchResultItem result, SearchItem item)
        {
            UnityEngine.Object obj = GetObjectFromSearchItem(item);
            if (obj == null)
                return;

            string assetPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(assetPath))
                return;

            // File details
            if (File.Exists(assetPath))
            {
                FileInfo fileInfo = new FileInfo(assetPath);
                result.FileSize = fileInfo.Length;
                result.LastModified = fileInfo.LastWriteTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                result.Properties["CreationTime"] = fileInfo.CreationTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            }

            // GUID
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (!string.IsNullOrEmpty(guid))
            {
                result.Properties["GUID"] = guid;
            }

            // Asset labels
            string[] labels = AssetDatabase.GetLabels(obj);
            if (labels.Length > 0)
            {
                result.Properties["Labels"] = string.Join(", ", labels);
            }

            // Addressables metadata when the package is available
            AddAddressableInfo(result, assetPath);

            // Number of dependent assets
            string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
            result.Properties["DependencyCount"] = dependencies.Length;

            // Import settings hash
            UnityEngine.Hash128 hash = AssetDatabase.GetAssetDependencyHash(assetPath);
            result.Properties["ImportHash"] = hash.ToString();
        }

        /// <summary>
        /// Append Addressables metadata
        /// </summary>
        private static void AddAddressableInfo(SearchResultItem result, string assetPath)
        {
            // Use reflection to acquire AddressableAssetSettings
            System.Type settingsType = System.Type.GetType("UnityEditor.AddressableAssets.AddressableAssetSettings, Unity.Addressables.Editor");
            if (settingsType == null)
                return;

            System.Reflection.PropertyInfo defaultSettingsProp = settingsType.GetProperty("DefaultObject",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            if (defaultSettingsProp == null)
                return;

            object settings = defaultSettingsProp.GetValue(null);
            if (settings == null)
                return;

            // Retrieve the FindAssetEntry method
            System.Reflection.MethodInfo findEntryMethod = settingsType.GetMethod("FindAssetEntry",
                new[] { typeof(string) });
            if (findEntryMethod == null)
                return;

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            object entry = findEntryMethod.Invoke(settings, new object[] { guid });
            if (entry == null)
                return;

            // Extract the Address and Group
            System.Type entryType = entry.GetType();
            System.Reflection.PropertyInfo addressProp = entryType.GetProperty("address");
            System.Reflection.PropertyInfo parentGroupProp = entryType.GetProperty("parentGroup");

            if (addressProp != null)
            {
                string address = addressProp.GetValue(entry) as string;
                if (!string.IsNullOrEmpty(address))
                {
                    result.Properties["AddressableAddress"] = address;
                }
            }

            if (parentGroupProp != null)
            {
                object parentGroup = parentGroupProp.GetValue(entry);
                if (parentGroup != null)
                {
                    System.Reflection.PropertyInfo nameProp = parentGroup.GetType().GetProperty("Name");
                    if (nameProp != null)
                    {
                        string groupName = nameProp.GetValue(parentGroup) as string;
                        if (!string.IsNullOrEmpty(groupName))
                        {
                            result.Properties["AddressableGroup"] = groupName;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Add asset-specific detailed properties
        /// </summary>
        private static void AddAssetSpecificProperties(SearchResultItem result, SearchItem item)
        {
            UnityEngine.Object obj = GetObjectFromSearchItem(item);
            if (obj == null)
                return;

            string assetPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(assetPath))
                return;

            // Texture information
            AddTextureProperties(result, obj, assetPath);

            // Mesh information
            AddMeshProperties(result, obj, assetPath);

            // Transform information
            AddTransformProperties(result, obj);
        }

        /// <summary>
        /// Add detailed texture properties
        /// </summary>
        private static void AddTextureProperties(SearchResultItem result, UnityEngine.Object obj, string assetPath)
        {
            if (!(obj is Texture texture))
                return;

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            // Width/Height
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            result.Properties["Width"] = width;
            result.Properties["Height"] = height;

            // MaxTextureSize
            result.Properties["MaxTextureSize"] = importer.maxTextureSize;

            // TextureCompression
            result.Properties["TextureCompression"] = importer.textureCompression.ToString();

            // IsReadable (Importer)
            result.Properties["IsReadable"] = importer.isReadable;

            // Format
            if (obj is Texture2D tex2D)
            {
                result.Properties["Format"] = tex2D.format.ToString();
            }

            // MipMapCount
            result.Properties["MipMapCount"] = texture.mipmapCount;

            // FilterMode
            result.Properties["FilterMode"] = texture.filterMode.ToString();

            // WrapMode
            result.Properties["WrapMode"] = texture.wrapMode.ToString();

            // AnisoLevel
            result.Properties["AnisoLevel"] = texture.anisoLevel;
        }

        /// <summary>
        /// Add detailed mesh properties
        /// </summary>
        private static void AddMeshProperties(SearchResultItem result, UnityEngine.Object obj, string assetPath)
        {
            Mesh mesh = null;

            // Acquire a MeshFilter from the GameObject
            if (obj is GameObject go)
            {
                if (go.TryGetComponent<MeshFilter>(out MeshFilter meshFilter))
                {
                    mesh = meshFilter.sharedMesh;
                }
                // Or use the SkinnedMeshRenderer
                else if (go.TryGetComponent<SkinnedMeshRenderer>(out SkinnedMeshRenderer skinnedMesh))
                {
                    mesh = skinnedMesh.sharedMesh;
                    // BoneCount
                    if (skinnedMesh.bones != null)
                    {
                        result.Properties["BoneCount"] = skinnedMesh.bones.Length;
                    }
                }
            }
            // Direct mesh object
            else if (obj is Mesh directMesh)
            {
                mesh = directMesh;
            }

            if (mesh == null)
                return;

            // IsReadable
            result.Properties["IsReadable"] = mesh.isReadable;

            // Properties that don't require readable access
            result.Properties["SubMeshCount"] = mesh.subMeshCount;
            result.Properties["BlendShapeCount"] = mesh.blendShapeCount;
            result.Properties["Bounds"] = mesh.bounds.ToString();

            // Properties that require readable access
            if (mesh.isReadable)
            {
                result.Properties["VertexCount"] = mesh.vertexCount;
                result.Properties["TriangleCount"] = mesh.triangles.Length / 3;
            }
        }

        /// <summary>
        /// Add detailed transform properties
        /// </summary>
        private static void AddTransformProperties(SearchResultItem result, UnityEngine.Object obj)
        {
            if (!(obj is GameObject gameObject) || gameObject.transform == null)
                return;

            Transform transform = gameObject.transform;

            // Position (world space)
            result.Properties["Position"] = transform.position.ToString();

            // LocalPosition
            result.Properties["LocalPosition"] = transform.localPosition.ToString();

            // Rotation (Euler angles)
            result.Properties["Rotation"] = transform.eulerAngles.ToString();

            // LocalRotation (Euler angles)
            result.Properties["LocalRotation"] = transform.localEulerAngles.ToString();

            // LocalScale
            result.Properties["LocalScale"] = transform.localScale.ToString();

            // LossyScale
            result.Properties["LossyScale"] = transform.lossyScale.ToString();
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
