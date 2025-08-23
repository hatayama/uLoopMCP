namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Security Violation
    /// Related Classes: SecurityValidator, SecuritySyntaxWalker
    /// </summary>
    public class SecurityViolation
    {
        /// <summary>Violation Type</summary>
        public SecurityViolationType Type { get; set; }

        /// <summary>Description of the Security Violation</summary>
        public string Description { get; set; }

        /// <summary>Line Number where the Violation Occurred</summary>
        public int LineNumber { get; set; }

        /// <summary>Code Snippet Containing the Violation</summary>
        public string CodeSnippet { get; set; }
        
        /// <summary>Violation Message</summary>
        public string Message { get; set; }
        
        /// <summary>API Name Involved in the Violation</summary>
        public string ApiName { get; set; }
        
#if ULOOPMCP_HAS_ROSLYN
        /// <summary>Location of the Violation</summary>
        public Microsoft.CodeAnalysis.Location Location { get; set; }
#else
        /// <summary>Location (object when Roslyn is disabled)</summary>
        public object Location { get; set; }
#endif
    }

    /// <summary>
    /// Security Violation Types
    /// </summary>
    public enum SecurityViolationType
    {
        /// <summary>Using Declaration of a Forbidden Namespace</summary>
        ForbiddenNamespace,

        /// <summary>Dangerous API Call (Method, Property, etc.)</summary>
        DangerousApiCall,

        /// <summary>Inheritance from a Dangerous Class</summary>
        DangerousInheritance,

        /// <summary>Instantiation of a Dangerous Type</summary>
        DangerousTypeCreation,

        /// <summary>Unauthorized Reflection Usage</summary>
        UnauthorizedReflection
    }
}