using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Responsible for temporal cohesion of GameObject search processing
    /// Processing sequence: 1. Search criteria validation, 2. GameObject search execution, 3. Result conversion and formatting
    /// Related classes: FindGameObjectsTool, GameObjectFinderService, ComponentSerializer
    /// Design reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// </summary>
    public class FindGameObjectsUseCase : AbstractUseCase<FindGameObjectsSchema, FindGameObjectsResponse>
    {
        private readonly GameObjectFinderService _finderService;
        private readonly ComponentSerializer _componentSerializer;

        public FindGameObjectsUseCase(GameObjectFinderService finderService, ComponentSerializer componentSerializer)
        {
            _finderService = finderService ?? throw new System.ArgumentNullException(nameof(finderService));
            _componentSerializer = componentSerializer ?? throw new System.ArgumentNullException(nameof(componentSerializer));
        }
        /// <summary>
        /// Execute GameObject search processing
        /// </summary>
        /// <param name="parameters">Search parameters</param>
        /// <param name="cancellationToken">Cancellation control token</param>
        /// <returns>Search result</returns>
        public override Task<FindGameObjectsResponse> ExecuteAsync(FindGameObjectsSchema parameters, CancellationToken cancellationToken)
        {
            // 1. Search criteria validation
            if (string.IsNullOrEmpty(parameters.NamePattern) &&
                (parameters.RequiredComponents == null || parameters.RequiredComponents.Length == 0) &&
                string.IsNullOrEmpty(parameters.Tag) &&
                !parameters.Layer.HasValue)
            {
                return Task.FromResult(new FindGameObjectsResponse
                {
                    results = new FindGameObjectResult[0],
                    totalFound = 0,
                    errorMessage = "At least one search criterion must be provided"
                });
            }
            
            // 2. GameObject search execution
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                GameObjectSearchOptions options = new GameObjectSearchOptions
                {
                    NamePattern = parameters.NamePattern,
                    SearchMode = parameters.SearchMode,
                    RequiredComponents = parameters.RequiredComponents,
                    Tag = parameters.Tag,
                    Layer = parameters.Layer,
                    IncludeInactive = parameters.IncludeInactive,
                    MaxResults = parameters.MaxResults
                };
                
                GameObjectDetails[] foundObjects = _finderService.FindGameObjectsAdvanced(options);
            
                // 3. Result conversion and formatting
                cancellationToken.ThrowIfCancellationRequested();
                
                List<FindGameObjectResult> results = new List<FindGameObjectResult>();
                
                foreach (GameObjectDetails details in foundObjects)
                {
                    // Check cancellation less frequently for better performance
                    if (results.Count % 100 == 0)
                        cancellationToken.ThrowIfCancellationRequested();
                    
                    try
                    {
                        FindGameObjectResult result = new FindGameObjectResult
                        {
                            name = details.Name,
                            path = details.Path,
                            isActive = details.IsActive,
                            tag = details.GameObject.tag,
                            layer = details.GameObject.layer,
                            components = _componentSerializer.SerializeComponents(details.GameObject)
                        };
                        
                        results.Add(result);
                    }
                    catch (System.Exception ex)
                    {
                        // Log error but continue processing other GameObjects
                        UnityEngine.Debug.LogWarning($"Failed to process GameObject '{details.Name}': {ex.Message}");
                        VibeLogger.LogWarning(
                            "gameobject_processing_failed", 
                            $"Failed to process GameObject: {details.Name}", 
                            new { gameObjectName = details.Name, gameObjectPath = details.Path, error = ex.Message }
                        );
                        continue;
                    }
                }
                
                FindGameObjectsResponse response = new FindGameObjectsResponse
                {
                    results = results.ToArray(),
                    totalFound = results.Count
                };
                
                // Underlying services are synchronous; wrapping in Task.FromResult for API consistency.
                return Task.FromResult(response);
            }
            catch (System.Exception ex)
            {
                // Log full exception details for debugging
                UnityEngine.Debug.LogError($"GameObject search failed: {ex}");
                VibeLogger.LogError(
                    "gameobject_search_failed", 
                    "GameObject search execution failed", 
                    new { searchParameters = parameters, error = ex.Message }
                );
                
                return Task.FromResult(new FindGameObjectsResponse
                {
                    results = new FindGameObjectResult[0],
                    totalFound = 0,
                    errorMessage = "Search execution failed. Please check the logs for details."
                });
            }
        }
    }
}