namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Response for ExecuteMenuItem command
    /// Contains execution result and method information
    /// </summary>
    public class ExecuteMenuItemResponse : BaseToolResponse
    {
        /// <summary>
        /// The menu item path that was executed
        /// </summary>
        public string MenuItemPath { get; set; }

        /// <summary>
        /// Whether the execution was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The execution method used (EditorApplication or Reflection)
        /// </summary>
        public string ExecutionMethod { get; set; }

        /// <summary>
        /// Error message if execution failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Additional information about the execution
        /// </summary>
        public string Details { get; set; }

        /// <summary>
        /// Whether the menu item was found in the system
        /// </summary>
        public bool MenuItemFound { get; set; }

        /// <summary>
        /// Warning message if there are issues with this MenuItem (e.g., duplicate attributes)
        /// </summary>
        public string WarningMessage { get; set; }

        public ExecuteMenuItemResponse()
        {
            MenuItemPath = string.Empty;
            ExecutionMethod = string.Empty;
            ErrorMessage = string.Empty;
            Details = string.Empty;
            WarningMessage = string.Empty;
        }
    }
}