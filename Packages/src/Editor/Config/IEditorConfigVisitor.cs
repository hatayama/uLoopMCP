namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Visitor interface for handling editor-specific configuration generation.
    /// 
    /// This interface enables the Visitor pattern to eliminate switch statements
    /// when converting McpEditorType enum values to their corresponding settings.
    /// Each editor type has its own Visit method that returns type T.
    /// 
    /// Related classes:
    /// - McpEditorType: Enum that defines supported editor types
    /// - EditorConfig: Value object that stores configuration information
    /// - EditorConfigProvider: Concrete implementation of this visitor interface
    /// </summary>
    /// <typeparam name="T">The return type for visit operations</typeparam>
    public interface IEditorConfigVisitor<out T>
    {
        /// <summary>
        /// Visits Cursor editor configuration.
        /// </summary>
        /// <returns>Configuration result for Cursor</returns>
        T VisitCursor();

        /// <summary>
        /// Visits Claude Code editor configuration.
        /// </summary>
        /// <returns>Configuration result for Claude Code</returns>
        T VisitClaudeCode();

        /// <summary>
        /// Visits VSCode editor configuration.
        /// </summary>
        /// <returns>Configuration result for VSCode</returns>
        T VisitVSCode();

        /// <summary>
        /// Visits Gemini CLI editor configuration.
        /// </summary>
        /// <returns>Configuration result for Gemini CLI</returns>
        T VisitGeminiCLI();

        /// <summary>
        /// Visits Windsurf editor configuration.
        /// </summary>
        /// <returns>Configuration result for Windsurf</returns>
        T VisitWindsurf();

        /// <summary>
        /// Visits Codex editor configuration.
        /// </summary>
        /// <returns>Configuration result for Codex</returns>
        T VisitCodex();

        /// <summary>
        /// Visits MCP Inspector configuration.
        /// </summary>
        /// <returns>Configuration result for MCP Inspector</returns>
        T VisitMcpInspector();
    }
}