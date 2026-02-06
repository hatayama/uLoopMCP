using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// MCP Dynamic C# Code Execution Tool
    /// Regenerates Executor only when security level changes, otherwise caches and reuses
    /// Related Classes: IDynamicCodeExecutor, DynamicCodeExecutorFactory
    /// </summary>
    [McpTool(Description = @"Editor automation only — no file I/O, no script authoring.

Direct statements only (no classes/namespaces/methods); return is optional (auto 'return null;' if omitted).

You may include using directives at the top; they are hoisted above the wrapper.
Example:
  using UnityEngine;
  var x = Mathf.PI;
  return x;

Do:
- Prefab/material wiring (PrefabUtility)
- AddComponent + reference wiring (SerializedObject)
- Scene/hierarchy edits

Don't:
- System.IO.* (File/Directory/Path)
- AssetDatabase.CreateFolder / file writes
- Create/edit .cs/.asmdef (use Terminal/IDE instead)

Need files/dirs? Run terminal commands.

See examples at {project_root}/.claude/skills/uloop-execute-dynamic-code/examples/")]
    public class ExecuteDynamicCodeTool : AbstractUnityTool<ExecuteDynamicCodeSchema, ExecuteDynamicCodeResponse>
    {
        private IDynamicCodeExecutor _executor;
        private readonly UserFriendlyErrorConverter _errorHandler;
        private DynamicCodeSecurityLevel _currentSecurityLevel;
        
        public override string ToolName => "execute-dynamic-code";
        
        public ExecuteDynamicCodeTool()
        {
#if ULOOPMCP_HAS_ROSLYN
            _executor = null;
            _errorHandler = new UserFriendlyErrorConverter();
            // Set initial value to an invalid value (will always be recreated on the first request)
            _currentSecurityLevel = (DynamicCodeSecurityLevel)(-1);
#else
            // Null when Roslyn is disabled
            _executor = null;
            _errorHandler = null;
            _currentSecurityLevel = DynamicCodeSecurityLevel.Disabled;
#endif
        }
        
        protected override async Task<ExecuteDynamicCodeResponse> ExecuteAsync(
            ExecuteDynamicCodeSchema parameters, 
            CancellationToken cancellationToken)
        {
            string correlationId = McpConstants.GenerateCorrelationId();
            
            try
            {
#if ULOOPMCP_HAS_ROSLYN
                DynamicCodeSecurityLevel editorLevel = McpEditorSettings.GetDynamicCodeSecurityLevel();
                
                // Recreate Executor only when editor settings change (cache for performance)
                if (_executor == null || editorLevel != _currentSecurityLevel)
                {
                    _currentSecurityLevel = editorLevel;
                    _executor = Factory.DynamicCodeExecutorFactory.Create(_currentSecurityLevel);
                }
#endif
                
                // Log execution start with VibeLogger
                VibeLogger.LogInfo(
                    "execute_dynamic_code_start",
                    "Dynamic code execution started (return optional)",
                    new { 
                        correlationId,
                        codeLength = parameters.Code?.Length ?? 0,
                        compileOnly = parameters.CompileOnly,
                        parametersCount = parameters.Parameters?.Count ?? 0,
                        securityLevel = _currentSecurityLevel.ToString()
                    },
                    correlationId,
                    "Dynamic code execution request received (return is optional)",
                    "Monitor execution flow and performance"
                );
                
                // Level 0: Preempt with unified error (compilation and execution not allowed)
                if (_currentSecurityLevel == DynamicCodeSecurityLevel.Disabled)
                {
                    VibeLogger.LogWarning(
                        "execute_dynamic_code_blocked_level0",
                        "Dynamic code request blocked at security level 0",
                        new { level = _currentSecurityLevel.ToString(), compileOnly = parameters.CompileOnly },
                        correlationId,
                        "Compilation is disabled at isolation level 0",
                        "Raise to level 1+ to compile or execute"
                    );

                    return new ExecuteDynamicCodeResponse
                    {
                        Success = false,
                        Result = "",
                        Logs = new List<string>(),
                        CompilationErrors = new List<CompilationErrorDto>(),
                        ErrorMessage = McpConstants.ERROR_MESSAGE_COMPILATION_DISABLED_LEVEL0,
                        SecurityLevel = _currentSecurityLevel.ToString()
                    };
                }
                
                // Level 1: In Restricted mode, delegate to Roslyn-based validation
                // Remove regex-based checks, SecurityValidator of RoslynCompiler handles this
                // This allows proper handling of user-defined classes (Assembly-CSharp)
                
                // Retrieve code
                string originalCode = parameters.Code ?? "";

                // Pre-execution guard: block file/dir I/O attempts only in Restricted mode (use terminal instead)
                if (_currentSecurityLevel == DynamicCodeSecurityLevel.Restricted &&
                    Regex.IsMatch(originalCode, @"\b(System\.IO\.|File\.|Directory\.|AssetDatabase\.CreateFolder\b)"))
                {
                    VibeLogger.LogWarning(
                        "execute_dynamic_code_blocked_io",
                        "Blocked due to file/dir I/O usage in dynamic code",
                        new { pattern = "System.IO.*, File.*, Directory.*, Path.*, AssetDatabase.CreateFolder" },
                        correlationId,
                        "File/dir I/O is disallowed in ExecuteDynamicCode",
                        "Use terminal commands for files/dirs"
                    );

                    return new ExecuteDynamicCodeResponse
                    {
                        Success = false,
                        Result = string.Empty,
                        Logs = new List<string> { "Explanation: File/dir I/O is disallowed in ExecuteDynamicCode. Use terminal commands instead." },
                        CompilationErrors = new List<CompilationErrorDto>(),
                        ErrorMessage = "File/dir I/O is disallowed in ExecuteDynamicCode. Use terminal commands instead.",
                        SecurityLevel = _currentSecurityLevel.ToString()
                    };
                }

                // Convert to parameter array
                object[] parametersArray = null;
                if (parameters.Parameters != null && parameters.Parameters.Count > 0)
                {
                    parametersArray = parameters.Parameters.Values.ToArray();
                }
                
                // Code execution (RoslynCompiler performs diagnostic-driven modifications)
                ExecutionResult executionResult = await _executor.ExecuteCodeAsync(
                    originalCode, // Use original code (RoslynCompiler will perform modifications)
                    "DynamicCommand",
                    parametersArray,
                    cancellationToken,
                    parameters.CompileOnly
                );

                // Optional: auto-insert return retry if missing return likely caused failure (unconditional)
                if (!executionResult.Success)
                {
                    bool looksLikeMissingReturn = false;
                    if (executionResult.CompilationErrors?.Any() == true)
                    {
                        looksLikeMissingReturn = executionResult.CompilationErrors.Any(e => e.ErrorCode == "CS0161" || e.ErrorCode == "CS0127");
                    }
                    else if (executionResult.Logs?.Any() == true)
                    {
                        looksLikeMissingReturn = executionResult.Logs.Any(l => l.Contains("CS0161") || l.Contains("CS0127") || l.Contains("must return a value"));
                    }

                    if (looksLikeMissingReturn && string.IsNullOrEmpty(executionResult.UpdatedCode))
                    {
                        string codeWithReturn = AppendReturnIfMissing(originalCode);
                        ExecutionResult retryReturnResult = await _executor.ExecuteCodeAsync(
                            codeWithReturn,
                            "DynamicCommand",
                            parametersArray,
                            cancellationToken,
                            parameters.CompileOnly
                        );
                        if (retryReturnResult.Success)
                        {
                            executionResult = retryReturnResult;
                        }
                        else if (retryReturnResult.Logs?.Any() == true)
                        {
                            if (executionResult.Logs == null) executionResult.Logs = new List<string>();
                            executionResult.Logs.AddRange(retryReturnResult.Logs);
                        }
                    }
                }

                // Convert to response (use improved message on error)
                ExecuteDynamicCodeResponse toolResponse = ConvertExecutionResultToResponse(
                    executionResult, originalCode, correlationId);
                
                // Add security level
                toolResponse.SecurityLevel = _currentSecurityLevel.ToString();
                
                return toolResponse;
            }
            catch (Exception ex)
            {
                // Log error with VibeLogger
                VibeLogger.LogError(
                    "execute_dynamic_code_error",
                    "Dynamic code execution failed with exception",
                    new { 
                        correlationId,
                        error = ex.Message,
                        stackTrace = ex.StackTrace
                    },
                    correlationId,
                    "Unexpected error during dynamic code execution",
                    "Investigate error cause and improve error handling"
                );

                // Delegate exception-to-DTO conversion to UserFriendlyErrorConverter
                UserFriendlyErrorDto exceptionResponse = _errorHandler?.ProcessException(ex);
                if (exceptionResponse != null)
                {
                    return new ExecuteDynamicCodeResponse
                    {
                        Success = false,
                        Result = "",
                        Logs = new List<string> 
                        { 
                            $"Original Error: {ex.Message}",
                            string.IsNullOrEmpty(exceptionResponse.Explanation) ? null : $"Explanation: {exceptionResponse.Explanation}"
                        }.Where(s => !string.IsNullOrEmpty(s)).ToList(),
                        CompilationErrors = new List<CompilationErrorDto>(),
                        ErrorMessage = exceptionResponse.FriendlyMessage,
                        SecurityLevel = _currentSecurityLevel.ToString()
                    };
                }

                return CreateErrorResponse(ex.Message);
            }
        }
        
        /// <summary>
        /// Convert ExecutionResponse to ExecuteDynamicCodeResponse
        /// </summary>
        private ExecuteDynamicCodeResponse ConvertExecutionResultToResponse(
            ExecutionResult result, string originalCode, string correlationId)
        {
            ExecuteDynamicCodeResponse response = new ExecuteDynamicCodeResponse
            {
                Success = result.Success,
                Result = result.Result?.ToString() ?? "",
                Logs = result.Logs ?? new List<string>(),
                CompilationErrors = new List<CompilationErrorDto>(), // Cannot be retrieved from ExecutionResult
                ErrorMessage = result.ErrorMessage ?? ""
            };

            // Use improved message on error
            if (!result.Success)
            {
                // In case of compilation error, retrieve error information from Logs
                string actualErrorMessage = result.ErrorMessage ?? "";
                if (result.Logs?.Any() == true)
                {
                    actualErrorMessage = string.Join(" ", result.Logs);
                }
                
                UserFriendlyErrorDto enhancedError = 
                    _errorHandler.ProcessError(result, originalCode);
                
                // Replace with a more understandable error message
                response.ErrorMessage = enhancedError.FriendlyMessage;
                
                // Prepare logs container; keep concise on failure (no raw per-line spam)
                response.Logs = new List<string>();
                
                if (!string.IsNullOrEmpty(enhancedError.Explanation))
                {
                    response.Logs.Add($"Explanation: {enhancedError.Explanation}");
                }
                
                if (!string.IsNullOrEmpty(enhancedError.Example))
                {
                    response.Logs.Add($"Example: {enhancedError.Example}");
                }
                
                if (enhancedError.SuggestedSolutions?.Any() == true)
                {
                    response.Logs.Add("Solutions:");
                    foreach (string solution in enhancedError.SuggestedSolutions)
                    {
                        response.Logs.Add($"- {solution}");
                    }
                }
                
                if (enhancedError.LearningTips?.Any() == true)
                {
                    response.Logs.Add("Tips:");
                    foreach (string tip in enhancedError.LearningTips)
                    {
                        response.Logs.Add($"- {tip}");
                    }
                }

                // Populate structured diagnostics and summary for clients
                if (result.CompilationErrors?.Any() == true)
                {
                    // Build diagnostics with context, hints, and suggestions
                    response.Diagnostics = BuildDiagnostics(result.CompilationErrors, result.UpdatedCode, result.AmbiguousTypeCandidates);
                    response.CompilationErrors = response.Diagnostics; // backward compat

                    int total = response.Diagnostics.Count;
                    int unique = response.Diagnostics
                        .GroupBy(e => new { e.Line, e.Column, e.ErrorCode, e.Message })
                        .Count();
                    var first = response.Diagnostics.First();
                    response.DiagnosticsSummary = $"Errors: {unique} unique ({total} total). First at L{first.Line}: {first.ErrorCode} {first.Message}";

                    // Prefer concise summary in Logs instead of raw error spam
                    response.Logs.Add(response.DiagnosticsSummary);
                }

                // Attach updated code when available (for line mapping)
                response.UpdatedCode = result.UpdatedCode ?? response.UpdatedCode;
            }

            // Add error information when an exception occurs
            if (result.Exception != null)
            {
                if (response.Logs == null) response.Logs = new List<string>();
                response.Logs.Add($"Exception: {result.Exception.Message}");
                if (!string.IsNullOrEmpty(result.Exception.StackTrace))
                {
                    response.Logs.Add($"Stack Trace: {result.Exception.StackTrace}");
                }
            }

            return response;
        }

        private static List<CompilationErrorDto> BuildDiagnostics(
            List<CompilationError> errors,
            string updatedCode,
            Dictionary<string, List<string>> ambiguousCandidates = null)
        {
            List<CompilationErrorDto> list = new();
            string[] lines = string.IsNullOrEmpty(updatedCode) ? Array.Empty<string>() : updatedCode.Split(new char[] { '\n' }, StringSplitOptions.None);
            foreach (CompilationError e in errors)
            {
                (string hint, List<string> suggestions) = GetHintAndSuggestions(e, ambiguousCandidates);
                string context = ExtractContext(lines, e.Line, e.Column);
                list.Add(new CompilationErrorDto
                {
                    Line = e.Line,
                    Column = e.Column,
                    Message = e.Message,
                    ErrorCode = e.ErrorCode,
                    Hint = hint,
                    Suggestions = suggestions,
                    Context = context,
                    PointerColumn = e.Column
                });
            }

            // Deduplicate by (line, column, code, message)
            list = list
                .GroupBy(d => new { d.Line, d.Column, d.ErrorCode, d.Message })
                .Select(g => g.First())
                .ToList();

            return list;
        }

        private static readonly Regex TypeNameInErrorPattern = new Regex(@"['""]([^'""]+)['""]", RegexOptions.Compiled);

        private static (string, List<string>) GetHintAndSuggestions(
            CompilationError e,
            Dictionary<string, List<string>> ambiguousCandidates = null)
        {
            string hint = string.Empty;
            List<string> suggestions = new();
            switch (e.ErrorCode)
            {
                case "CS0246": // type or namespace name could not be found
                    string typeName = ExtractTypeNameFromErrorMessage(e.Message);
                    if (typeName != null
                        && ambiguousCandidates != null
                        && ambiguousCandidates.TryGetValue(typeName, out List<string> candidates))
                    {
                        string candidateList = string.Join(", ", candidates);
                        hint = $"Auto-using resolution found multiple candidates for '{typeName}': {candidateList}. Use a fully-qualified name or add the correct using directive.";
                        foreach (string ns in candidates)
                        {
                            suggestions.Add($"Use {ns}.{typeName}");
                        }
                    }
                    else
                    {
                        hint = "Auto-using resolution was attempted but could not resolve this identifier. Use a fully-qualified name (e.g., UnityEngine.Mathf) or add the correct using directive.";
                        suggestions.Add("Use fully-qualified name (e.g., UnityEngine.Mathf, System.Linq.Enumerable)");
                        suggestions.Add("Add the appropriate using directive at the top of the snippet");
                    }
                    break;
                case "CS0103": // name does not exist in the current context
                    hint = "Identifier does not exist in the current context. Check spelling, declaration scope, and whether this should be a type name.";
                    suggestions.Add("Declare the identifier before use");
                    suggestions.Add("If this is a type name, use a fully-qualified name or add the correct using directive");
                    break;
                case "CS0104": // ambiguous reference
                    hint = "Identifier is ambiguous; qualify explicitly (e.g., UnityEngine.Object).";
                    suggestions.Add("Qualify with full namespace (e.g., UnityEngine.Object)");
                    break;
                default:
                    break;
            }
            return (hint, suggestions);
        }

        private static string ExtractTypeNameFromErrorMessage(string message)
        {
            Match match = TypeNameInErrorPattern.Match(message);
            if (!match.Success) return null;

            string rawName = match.Groups[1].Value;
            int genericIndex = rawName.IndexOf('<');
            if (genericIndex > 0)
            {
                rawName = rawName.Substring(0, genericIndex);
            }
            return rawName;
        }

        private static string ExtractContext(string[] lines, int lineNumber1Based, int column1Based)
        {
            if (lines == null || lines.Length == 0 || lineNumber1Based <= 0 || lineNumber1Based > lines.Length)
            {
                return string.Empty;
            }

            int start = Math.Max(1, lineNumber1Based - 3);
            int end = Math.Min(lines.Length, lineNumber1Based + 3);
            StringBuilder sb = new();
            for (int i = start; i <= end; i++)
            {
                string ln = lines[i - 1].TrimEnd('\r');
                sb.AppendLine($"L{i}:{ln}");
                if (i == lineNumber1Based)
                {
                    int caretPos = Math.Max(1, column1Based);
                    sb.AppendLine("   " + new string(' ', Math.Max(0, caretPos - 1)) + "^");
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Append a safe default return when snippet likely misses a return statement.
        /// - Adds a trailing semicolon if last non-whitespace char is not ';'
        /// - Appends '\nreturn null;' to make result type object-compatible
        /// </summary>
        private static string AppendReturnIfMissing(string originalCode)
        {
            string code = originalCode ?? string.Empty;
            string trimmed = code.TrimEnd();
            bool endsWithSemicolon = trimmed.EndsWith(";");
            string builder = endsWithSemicolon ? code : (code + ";");
            return builder + "\nreturn null;";
        }
        
        /// <summary>
        /// Create error response
        /// </summary>
        private ExecuteDynamicCodeResponse CreateErrorResponse(string errorMessage)
        {
            return new ExecuteDynamicCodeResponse
            {
                Success = false,
                Result = "",
                Logs = new List<string>(),
                CompilationErrors = new List<CompilationErrorDto>(),
                ErrorMessage = errorMessage ?? "Unknown error occurred"
            };
        }
    }
}
