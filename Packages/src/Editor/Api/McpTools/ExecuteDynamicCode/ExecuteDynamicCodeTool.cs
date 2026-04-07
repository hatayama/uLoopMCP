using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// MCP Dynamic C# Code Execution Tool
    /// Regenerates Executor only when security level changes, otherwise caches and reuses
    /// Related Classes: IDynamicCodeExecutor, DynamicCodeExecutorFactory
    /// </summary>
    [McpTool(Description = @"Execute C# code dynamically in Unity Editor for editor automation.

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
    public class ExecuteDynamicCodeTool : AbstractUnityTool<ExecuteDynamicCodeSchema, ExecuteDynamicCodeResponse>, IDisposable
    {
        private IDynamicCodeExecutor _executor;
        private readonly UserFriendlyErrorConverter _errorHandler;
        private DynamicCodeSecurityLevel _currentSecurityLevel;
        
        public override string ToolName => "execute-dynamic-code";
        
        public ExecuteDynamicCodeTool()
        {
            _executor = null;
            _errorHandler = new UserFriendlyErrorConverter();
            _currentSecurityLevel = (DynamicCodeSecurityLevel)(-1);
        }
        
        protected override async Task<ExecuteDynamicCodeResponse> ExecuteAsync(
            ExecuteDynamicCodeSchema parameters, 
            CancellationToken cancellationToken)
        {
            string correlationId = McpConstants.GenerateCorrelationId();
            
            try
            {
                DynamicCodeSecurityLevel editorLevel = ULoopSettings.GetDynamicCodeSecurityLevel();

                if (_executor == null || editorLevel != _currentSecurityLevel)
                {
                    _currentSecurityLevel = editorLevel;
                    this.ReplaceExecutor(Factory.DynamicCodeExecutorFactory.Create(_currentSecurityLevel));
                }
                
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
                
                // Retrieve code
                string originalCode = parameters.Code ?? "";

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

        public void Dispose()
        {
            this.ReplaceExecutor(null);
        }
        
        private ExecuteDynamicCodeResponse ConvertExecutionResultToResponse(
            ExecutionResult result, string originalCode, string correlationId)
        {
            ExecuteDynamicCodeResponse response = new ExecuteDynamicCodeResponse
            {
                Success = result.Success,
                Result = result.Result?.ToString() ?? "",
                Logs = result.Logs ?? new List<string>(),
                CompilationErrors = new List<CompilationErrorDto>(), // Cannot be retrieved from ExecutionResult
                ErrorMessage = result.ErrorMessage ?? "",
                Timings = result.Timings != null ? new List<string>(result.Timings) : new List<string>()
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
                    CompilationErrorDto first = response.Diagnostics.First();
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

            // AI agents silently get auto-resolved usings without knowing they were missing;
            // this hint teaches them to include usings upfront for faster compilation
            if (result.AutoInjectedNamespaces != null && result.AutoInjectedNamespaces.Count > 0)
            {
                if (response.Logs == null) response.Logs = new List<string>();
                string usingList = string.Join(" ", result.AutoInjectedNamespaces.Select(ns => $"using {ns};"));
                response.Logs.Add(
                    $"Performance hint: Auto-resolved {result.AutoInjectedNamespaces.Count} missing using directive(s): "
                    + $"{usingList} — Include them in your code to skip auto-resolution and improve compilation speed.");
            }

            return response;
        }

        private void ReplaceExecutor(IDynamicCodeExecutor nextExecutor)
        {
            if (_executor != null)
            {
                _executor.Dispose();
            }

            _executor = nextExecutor;
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

        private static (string, List<string>) GetHintAndSuggestions(
            CompilationError e,
            Dictionary<string, List<string>> ambiguousCandidates = null)
        {
            string hint = string.Empty;
            List<string> suggestions = new();
            switch (e.ErrorCode)
            {
                case "CS0246": // type or namespace name could not be found
                    string typeName = CompilationDiagnosticMessageParser.ExtractTypeNameFromMessage(e.Message);
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
                    string identifierName = CompilationDiagnosticMessageParser.ExtractTypeNameFromMessage(e.Message);
                    if (identifierName != null
                        && ambiguousCandidates != null
                        && ambiguousCandidates.TryGetValue(identifierName, out List<string> cs0103Candidates))
                    {
                        string candidateList = string.Join(", ", cs0103Candidates);
                        hint = $"Auto-using resolution found multiple candidates for '{identifierName}': {candidateList}. Use a fully-qualified name or add the correct using directive.";
                        foreach (string ns in cs0103Candidates)
                        {
                            suggestions.Add($"Use {ns}.{identifierName}");
                        }
                    }
                    else
                    {
                        hint = "Identifier does not exist in the current context. Check spelling, declaration scope, and whether this should be a type name.";
                        suggestions.Add("Declare the identifier before use");
                        suggestions.Add("If this is a type name, use a fully-qualified name or add the correct using directive");
                    }
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

        private static string AppendReturnIfMissing(string originalCode)
        {
            string code = originalCode ?? string.Empty;
            string trimmed = code.TrimEnd();
            bool endsWithSemicolon = trimmed.EndsWith(";");
            string builder = endsWithSemicolon ? code : (code + ";");
            return builder + "\nreturn null;";
        }
        
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
