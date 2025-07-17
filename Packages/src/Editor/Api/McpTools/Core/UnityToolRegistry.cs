using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace io.github.hatayama.uLoopMCP
{
    // Related classes:
    // - UnityToolExecutor: Uses this registry to execute _tools.
    // - IUnityTool: The interface for all _tools stored in this registry.
    // - AbstractUnityTool: The base class for most tool implementations.
    // - McpToolAttribute: Attribute used to discover and register _tools automatically.
    /// <summary>
    /// Unity MCP tool registry class
    /// Supports dynamic tool registration, allowing users to add their own _tools
    /// </summary>
    public class UnityToolRegistry
    {
        private readonly Dictionary<string, IUnityTool> _tools = new();

        /// <summary>
        /// Singleton instance for global access
        /// </summary>
        public static UnityToolRegistry Instance { get; private set; }

        /// <summary>
        /// Default constructor
        /// Auto-registers standard _tools
        /// </summary>
        public UnityToolRegistry()
        {
            Instance = this;
            RegisterDefaultTools();
        }

        /// <summary>
        /// Register standard _tools
        /// </summary>
        private void RegisterDefaultTools()
        {
            // Register _tools with attribute-based discovery
            RegisterToolsWithAttributes();

            // Manual registration for _tools without attributes (for backward compatibility)
            RegisterManualTools();
        }

        /// <summary>
        /// Register _tools using attribute-based discovery
        /// </summary>
        private void RegisterToolsWithAttributes()
        {
            try
            {
                // Get all assemblies in the current domain
                Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();

                List<Type> toolTypes = new List<Type>();

                foreach (Assembly assembly in assemblies)
                {
                    // Find all types with McpTool attribute that implement IUnityTool
                    Type[] types = assembly.GetTypes()
                        .Where(type => type.GetCustomAttribute<McpToolAttribute>() != null)
                        .Where(type => typeof(IUnityTool).IsAssignableFrom(type))
                        .Where(type => !type.IsAbstract && !type.IsInterface)
                        .ToArray();

                    toolTypes.AddRange(types);
                }

                // Register all _tools - filtering will be handled by TypeScript side
                foreach (Type type in toolTypes)
                {
                    // Security: Validate type before creating instance
                    if (!IsValidToolType(type))
                    {
                        UnityEngine.Debug.LogWarning($"{McpConstants.SECURITY_LOG_PREFIX} Skipping invalid tool type: {type.FullName}");
                        continue;
                    }
                    
                    IUnityTool tool = (IUnityTool)Activator.CreateInstance(type);
                    RegisterTool(tool);
                }
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"Failed to register _tools with attributes: {ex.Message}");
                throw;
            }
        }


        /// <summary>
        /// Register _tools manually (for backward compatibility)
        /// </summary>
        private void RegisterManualTools()
        {
            // Only register _tools that don't have the McpTool attribute
            // This prevents double registration

            if (!IsToolTypeRegistered<PingTool>())
            {
                RegisterTool(new PingTool());
            }

            if (!IsToolTypeRegistered<CompileTool>())
            {
                RegisterTool(new CompileTool());
            }

            if (!IsToolTypeRegistered<GetLogsTool>())
            {
                RegisterTool(new GetLogsTool());
            }

            if (!IsToolTypeRegistered<RunTestsTool>())
            {
                RegisterTool(new RunTestsTool());
            }
        }

        /// <summary>
        /// Check if a tool type is already registered
        /// </summary>
        private bool IsToolTypeRegistered<T>() where T : IUnityTool
        {
            return _tools.Values.Any(tool => tool.GetType() == typeof(T));
        }
        
        /// <summary>
        /// Security: Validate if the type is safe to instantiate
        /// </summary>
        private bool IsValidToolType(Type type)
        {
            try
            {
                // Must implement IUnityTool
                if (!typeof(IUnityTool).IsAssignableFrom(type))
                {
                    return false;
                }
                
                // Must not be abstract or interface
                if (type.IsAbstract || type.IsInterface)
                {
                    return false;
                }
                
                // Must have McpTool attribute
                if (type.GetCustomAttribute<McpToolAttribute>() == null)
                {
                    return false;
                }
                
                
                // Must have parameterless constructor
                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"{McpConstants.SECURITY_LOG_PREFIX} Error validating tool type {type?.FullName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Register tool
        /// </summary>
        /// <param name="tool">Tool to register</param>
        public void RegisterTool(IUnityTool tool)
        {
            if (tool == null)
            {
                throw new ArgumentNullException(nameof(tool));
            }

            if (string.IsNullOrWhiteSpace(tool.ToolName))
            {
                throw new ArgumentException("Tool name cannot be null or empty", nameof(tool));
            }

            _tools[tool.ToolName] = tool;
        }

        /// <summary>
        /// Unregister tool
        /// </summary>
        /// <param name="toolName">Name of tool to unregister</param>
        public void UnregisterTool(string toolName)
        {
            _tools.Remove(toolName);
        }

        /// <summary>
        /// Execute tool
        /// </summary>
        /// <param name="toolName">Tool name</param>
        /// <param name="paramsToken">Parameters</param>
        /// <returns>Execution result</returns>
        /// <exception cref="ArgumentException">When tool is unknown</exception>
        /// <exception cref="McpSecurityException">When tool is blocked by security settings</exception>
        public async Task<BaseToolResponse> ExecuteToolAsync(string toolName, JToken paramsToken)
        {
            if (!_tools.TryGetValue(toolName, out IUnityTool tool))
            {
                throw new ArgumentException($"Unknown tool: {toolName}");
            }

            // Security check - validate tool before execution
            if (!McpSecurityChecker.IsToolAllowed(toolName))
            {
                throw new McpSecurityException(toolName, "Tool is blocked by security settings");
            }

            await MainThreadSwitcher.SwitchToMainThread();
            return await tool.ExecuteAsync(paramsToken);
        }


        /// <summary>
        /// Get detailed information of registered _tools
        /// </summary>
        /// <returns>Array of tool information</returns>
        public ToolInfo[] GetRegisteredTools()
        {
            return _tools.Values.Select(tool => 
            {
                // Check if tool has McpTool attribute with DisplayDevelopmentOnly
                bool displayDevelopmentOnly = false;
                McpToolAttribute attribute = tool.GetType().GetCustomAttribute<McpToolAttribute>();
                if (attribute != null)
                {
                    displayDevelopmentOnly = attribute.DisplayDevelopmentOnly;
                }
                
                // Check security settings
                bool isAllowed = McpSecurityChecker.IsToolAllowed(tool.ToolName);
                
                // Get description from attribute
                string description = attribute?.Description ?? "";
                
                // Modify description for blocked _tools
                if (!isAllowed)
                {
                    description = $"[BLOCKED] {description} - Blocked by security settings";
                }
                
                return new ToolInfo(tool.ToolName, description, tool.ParameterSchema, displayDevelopmentOnly);
            }).ToArray();
        }

        /// <summary>
        /// Get tool type by name for security checking
        /// </summary>
        /// <param name="toolName">Tool name</param>
        /// <returns>Tool type or null if not found</returns>
        public Type GetToolType(string toolName)
        {
            if (_tools.TryGetValue(toolName, out IUnityTool tool))
            {
                return tool.GetType();
            }
            return null;
        }

        /// <summary>
        /// Check if specified tool is registered
        /// </summary>
        /// <param name="toolName">Tool name</param>
        /// <returns>True if registered</returns>
        public bool IsToolRegistered(string toolName)
        {
            return _tools.ContainsKey(toolName);
        }

        /// <summary>
        /// Manually trigger _tools changed notification
        /// Used for manual notifications and post-compilation notifications
        /// </summary>
        public static void TriggerToolsChangedNotification()
        {
            // Call the public method in McpServerController
            McpServerController.TriggerToolChangeNotification();
        }

    }

    /// <summary>
    /// Class representing tool information
    /// </summary>
    public class ToolInfo
    {
        [JsonProperty("name")] public string Name { get; }

        [JsonProperty("description")] public string Description { get; }

        [JsonProperty("parameterSchema")] public ToolParameterSchema ParameterSchema { get; }

        [JsonProperty("displayDevelopmentOnly")] public bool DisplayDevelopmentOnly { get; }

        public ToolInfo(string name, string description, ToolParameterSchema parameterSchema, bool displayDevelopmentOnly = false)
        {
            Name = name;
            Description = description;
            ParameterSchema = parameterSchema;
            DisplayDevelopmentOnly = displayDevelopmentOnly;
        }
    }
}