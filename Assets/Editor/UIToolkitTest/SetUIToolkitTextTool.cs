using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using io.github.hatayama.uLoopMCP;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// MCP tool for setting text in the UI Toolkit test window
    /// Allows Claude or other MCP clients to display text in the Unity UI Toolkit test window
    /// </summary>
    [McpTool(Description = "Set text in the UI Toolkit test window")]
    public class SetUIToolkitTextTool : AbstractUnityTool<SetUIToolkitTextSchema, SetUIToolkitTextResponse>
    {
        public override string ToolName => "set-ui-toolkit-text";
        
        protected override Task<SetUIToolkitTextResponse> ExecuteAsync(SetUIToolkitTextSchema parameters, CancellationToken cancellationToken)
        {
            bool success = false;
            bool windowOpened = false;
            string message = "";
            
            try
            {
                // Execute on main thread
                success = UIToolkitTestWindow.SetTextFromMCP(parameters.Text, parameters.AutoOpenWindow, out windowOpened);
                
                if (success)
                {
                    message = windowOpened 
                        ? $"Text set successfully and window opened" 
                        : $"Text set successfully";
                    
                    if (parameters.LogToConsole)
                    {
                        UnityEngine.Debug.Log($"[SetUIToolkitText] {message}: '{parameters.Text}'");
                    }
                }
                else
                {
                    message = "Failed to set text";
                }
            }
            catch (Exception ex)
            {
                success = false;
                message = $"Error: {ex.Message}";
                UnityEngine.Debug.LogError($"[SetUIToolkitText] {message}");
            }

            // Create type-safe response
            SetUIToolkitTextResponse response = new(
                success: success,
                setText: parameters.Text,
                message: message,
                windowOpened: windowOpened,
                timestamp: DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            );

            return Task.FromResult(response);
        }
    }
}