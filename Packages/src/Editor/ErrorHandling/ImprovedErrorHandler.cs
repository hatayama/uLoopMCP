using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Related Classes: FriendlyMessageGenerator, ErrorTranslationDictionary
    /// </summary>
    public class UserFriendlyErrorConverter
    {
        private readonly ErrorTranslationDictionary _dictionary;
        private readonly FriendlyMessageGenerator _messageGenerator;

        public UserFriendlyErrorConverter()
        {
            _dictionary = new ErrorTranslationDictionary();
            _messageGenerator = new FriendlyMessageGenerator();
        }

        /// <summary>
        /// Convert an exception to an enhanced, user-friendly error response
        /// </summary>
        public UserFriendlyErrorDto ProcessException(Exception exception)
        {
            if (exception == null)
            {
                return new UserFriendlyErrorDto
                {
                    OriginalError = string.Empty,
                    FriendlyMessage = "Internal error",
                    Explanation = string.Empty,
                    Example = string.Empty,
                    SuggestedSolutions = new List<string>(),
                    LearningTips = new List<string>(),
                    Severity = ErrorSeverity.High
                };
            }

            string friendlyMessage;
            string explanation = string.Empty;
            List<string> solutions = new List<string>();

            if (exception is McpSecurityException securityException)
            {
                friendlyMessage = "Tool blocked by security settings";
                explanation = securityException.SecurityReason ?? string.Empty;
                solutions.Add("Open uLoopMCP Security Settings and enable the required permission");
            }
            else if (exception is TimeoutException)
            {
                friendlyMessage = "Request timeout";
                explanation = exception.Message;
                solutions.Add("Increase timeout or optimize the operation to complete faster");
            }
            else if (exception is ParameterValidationException)
            {
                // Parameter validation errors should surface detailed messages
                friendlyMessage = exception.Message;
                explanation = string.Empty;
                solutions.Add("Fix parameter types/values according to the tool schema");
            }
            else
            {
                friendlyMessage = "Internal error";
                explanation = exception.Message;
            }

            return new UserFriendlyErrorDto
            {
                OriginalError = exception.Message,
                FriendlyMessage = friendlyMessage,
                Explanation = explanation,
                Example = string.Empty,
                SuggestedSolutions = solutions,
                LearningTips = new List<string>(),
                Severity = DetermineErrorSeverityFromException(exception)
            };
        }

        /// <summary>
        /// Convert execution result errors into a more understandable format
        /// </summary>
        public UserFriendlyErrorDto ProcessError(
            ExecutionResult originalResult, 
            string originalCode)
        {
            // Retrieve error message (also check Logs for compilation errors)
            string errorMessage = originalResult.ErrorMessage ?? "";
            if (originalResult.Logs?.Any() == true)
            {
                string combinedErrors = string.Join(" ", originalResult.Logs);
                if (!string.IsNullOrEmpty(combinedErrors))
                {
                    errorMessage += " " + combinedErrors;
                }
            }
            
            // Error pattern matching
            ErrorPattern pattern = _dictionary.FindPattern(errorMessage);
            
            UserFriendlyErrorDto response = new()
            {
                OriginalError = errorMessage,
                FriendlyMessage = pattern?.FriendlyMessage ?? 
                                 _messageGenerator.TranslateError(errorMessage),
                Explanation = pattern?.Explanation ?? "",
                Example = pattern?.Example ?? "",
                SuggestedSolutions = pattern?.Solutions ?? new List<string>(),
                LearningTips = GetLearningTips(errorMessage),
                Severity = DetermineErrorSeverity(errorMessage)
            };

            return response;
        }

        private ErrorSeverity DetermineErrorSeverityFromException(Exception exception)
        {
            if (exception is McpSecurityException)
            {
                return ErrorSeverity.High;
            }
            if (exception is TimeoutException)
            {
                return ErrorSeverity.Medium;
            }
            if (exception is ParameterValidationException)
            {
                return ErrorSeverity.Low;
            }
            return ErrorSeverity.High;
        }

        private List<string> GetLearningTips(string errorMessage)
        {
            List<string> tips = new();
            
            if (errorMessage.Contains("Top-level statements"))
            {
                tips.Add("In Dynamic Tool, you can write code directly");
                tips.Add("Class and namespace declarations are not required");
            }
            
            if (errorMessage.Contains("not all code paths return"))
            {
                tips.Add("Add a return statement at the end of the code");
                tips.Add("Return the execution result as a string");
            }
            
            if (errorMessage.Contains("ambiguous reference"))
            {
                tips.Add("Clearly distinguish between UnityEngine.Object and System.Object");
                tips.Add("Using the full name (UnityEngine.Object) is safer");
            }
            
            return tips;
        }

        private ErrorSeverity DetermineErrorSeverity(string errorMessage)
        {
            if (errorMessage.Contains("Top-level statements") || 
                errorMessage.Contains("not all code paths"))
            {
                return ErrorSeverity.High; // Common pattern with high importance
            }
            
            if (errorMessage.Contains("ambiguous reference"))
            {
                return ErrorSeverity.Medium;
            }
            
            return ErrorSeverity.Low;
        }
    }

    /// <summary>
    /// Error Translation Dictionary - Translating Common Error Patterns into Understandable Messages
    /// </summary>
    public class ErrorTranslationDictionary
    {
        private static readonly Dictionary<string, ErrorPattern> Patterns = new()
        {
            ["Top-level statements must precede"] = new ErrorPattern
            {
                PatternRegex = @"Top-level statements must precede",
                FriendlyMessage = "There is an issue with the code structure",
                Explanation = "In the Dynamic Tool, class and namespace declarations are not required. Write Unity API processing directly.",
                Example = @"AVOID: namespace Test { class MyClass { void Method() { ... } } }

CORRECT: GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
cube.transform.position = Vector3.zero;
return ""Cube creation completed"";",
                Solutions = new List<string>
                {
                    "Remove class definition and write only the code inside the method",
                    "Remove namespace declaration", 
                    "Keep using statements and extract only the main processing"
                }
            },
            
            ["not all code paths return a value"] = new ErrorPattern
            {
                PatternRegex = @"not all code paths return",
                FriendlyMessage = "A return value is required at the end of the code",
                Explanation = "In the Dynamic Tool, you must return the execution result. Please add a return statement at the end of the processing.",
                Example = @"AVOID: GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
Debug.Log(""Completed"");

CORRECT: GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
Debug.Log(""Completed"");
return ""Cube creation completed"";",
                Solutions = new List<string>
                {
                    "Add 'return \"Execution completed\";' at the end of the code",
                    "Add return statements to all paths of conditional branching",
                    "Return the execution result as a string"
                }
            },

            ["Object' is an ambiguous reference"] = new ErrorPattern
            {
                PatternRegex = @"'Object' is an ambiguous reference",
                FriendlyMessage = "Object class reference is ambiguous",
                Explanation = "UnityEngine.Object and System.Object are mixed. Please specify explicitly.",
                Example = @"AVOID: Object.DestroyImmediate(obj);

CORRECT: UnityEngine.Object.DestroyImmediate(obj);",
                Solutions = new List<string>
                {
                    "Explicitly specify UnityEngine.Object",
                    "Use full name description"
                }
            }
        };

        public ErrorPattern FindPattern(string errorMessage)
        {
            foreach (var kvp in Patterns)
            {
                if (Regex.IsMatch(errorMessage, kvp.Value.PatternRegex))
                {
                    return kvp.Value;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// User-Friendly Message Generation Feature
    /// </summary>
    public class FriendlyMessageGenerator
    {
        public string TranslateError(string originalError)
        {
            // Basic error translation logic
            if (originalError.Contains("Top-level statements"))
            {
                return "There is an issue with the code structure. Class and namespace definitions are not required.";
            }
            
            if (originalError.Contains("not all code paths return"))
            {
                return "A return value is required at the end of the code. Please add 'return \"Completed\";'.";
            }
            
            if (originalError.Contains("ambiguous reference"))
            {
                return "Class reference is ambiguous. Please explicitly write UnityEngine.Object.";
            }
            
            if (originalError.Contains("Identifier expected"))
            {
                return "Syntax error. Please verify the code syntax.";
            }
            
            return originalError; // Fallback
        }
    }

    /// <summary>
    /// Error Pattern Definition
    /// </summary>
    public class ErrorPattern
    {
        public string PatternRegex { get; set; } = "";
        public string FriendlyMessage { get; set; } = "";
        public string Explanation { get; set; } = "";
        public string Example { get; set; } = "";
        public List<string> Solutions { get; set; } = new();
    }

    /// <summary>
    /// User-friendly error DTO for API/UI surfaces
    /// </summary>
    public class UserFriendlyErrorDto
    {
        public string OriginalError { get; set; } = "";
        public string FriendlyMessage { get; set; } = "";
        public string Explanation { get; set; } = "";
        public string Example { get; set; } = "";
        public List<string> SuggestedSolutions { get; set; } = new();
        public List<string> LearningTips { get; set; } = new();
        public ErrorSeverity Severity { get; set; }
    }

    /// <summary>
    /// Error Severity
    /// </summary>
    public enum ErrorSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
}