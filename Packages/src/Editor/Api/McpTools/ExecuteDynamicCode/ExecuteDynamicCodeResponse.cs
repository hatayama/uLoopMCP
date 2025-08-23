using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Response for dynamic code execution tool

    /// Related classes: ExecuteDynamicCodeTool, ExecuteDynamicCodeSchema
    /// </summary>
    public class ExecuteDynamicCodeResponse : BaseToolResponse
    {
        /// <summary>Execution success flag</summary>
        public bool Success { get; set; }
        
        /// <summary>Execution result</summary>
        public string Result { get; set; }
        
        /// <summary>Log messages</summary>
        public List<string> Logs { get; set; } = new();
        
        /// <summary>Compilation errors</summary>
        public List<CompilationErrorDto> CompilationErrors { get; set; } = new();
        
        /// <summary>Error message (on failure)</summary>
        public string ErrorMessage { get; set; }
        
        /// <summary>Current security level</summary>
        public string SecurityLevel { get; set; }
        
        /// <summary>Error message (alias for ErrorMessage)</summary>
        public string Error 
        { 
            get => ErrorMessage; 
            set => ErrorMessage = value; 
        }
        
        /// <summary>
        /// Code formatted for compilation
        /// (After extracting/moving using statements and applying class/method wrapping)
        /// Allows checking the actual compiled code during debugging
        /// </summary>
        public string UpdatedCode { get; set; }
        
    }
    
    /// <summary>
    /// DTO for compilation error information
    /// </summary>
    public class CompilationErrorDto
    {
        /// <summary>Error message</summary>
        public string Message { get; set; }
        
        /// <summary>Line number</summary>
        public int Line { get; set; }
        
        /// <summary>Column number</summary>
        public int Column { get; set; }
        
        /// <summary>Compiler error code (e.g., CS0103)</summary>
        public string ErrorCode { get; set; }
    }
}