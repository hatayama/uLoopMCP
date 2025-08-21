using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using io.github.hatayama.uLoopMCP;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// 動的コード実行統合実装
    /// v4.0 明示的セキュリティレベル管理
    /// 関連クラス: RoslynCompiler, SecurityValidator, CommandRunner
    /// </summary>
    public class DynamicCodeExecutor : IDynamicCodeExecutor
    {
#if ULOOPMCP_HAS_ROSLYN
        private readonly RoslynCompiler _compiler;
        private readonly SecurityValidator _validator;
        private readonly DynamicCodeSecurityLevel _securityLevel;
#endif
        private readonly CommandRunner _runner;
        private readonly ExecutionStatistics _statistics;
        private readonly object _statsLock = new();

        /// <summary>コンストラクタ</summary>
        public DynamicCodeExecutor(
#if ULOOPMCP_HAS_ROSLYN
            RoslynCompiler compiler,
            SecurityValidator validator,
            DynamicCodeSecurityLevel securityLevel,
#endif
            CommandRunner runner)
        {
#if ULOOPMCP_HAS_ROSLYN
            _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _securityLevel = securityLevel;
#endif
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _statistics = new ExecutionStatistics();

            VibeLogger.LogInfo(
                "dynamic_code_executor_initialized",
                "DynamicCodeExecutor initialized with dependencies",
                new
                {
#if ULOOPMCP_HAS_ROSLYN
                    compiler_type = compiler.GetType().Name,
                    validator_type = validator.GetType().Name,
#endif
                    runner_type = runner.GetType().Name
                },
                humanNote: "動的コード実行統合システムの初期化",
                aiTodo: "実行統計の収集開始"
            );
        }

        /// <summary>コード実行</summary>
        public ExecutionResult ExecuteCode(
            string code,
            string className = "DynamicCommand",
            object[] parameters = null,
            CancellationToken cancellationToken = default,
            bool compileOnly = false)
        {
            string correlationId = McpConstants.GenerateCorrelationId();
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
#if !ULOOPMCP_HAS_ROSLYN
                // Roslyn無効時のエラーレスポンス
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"{McpConstants.ERROR_ROSLYN_REQUIRED}: {McpConstants.ERROR_MESSAGE_ROSLYN_REQUIRED}",
                    ExecutionTime = stopwatch.Elapsed,
                    Statistics = _statistics
                };
#else
                VibeLogger.LogInfo(
                    "execute_code_start",
                    "Code execution started",
                    new
                    {
                        class_name = className,
                        parameter_count = parameters?.Length ?? 0,
                        code_length = code.Length,
                        compile_only = compileOnly
                    },
                    correlationId,
                    "動的コード実行開始",
                    "実行フローのステップ追跡"
                );

                // Phase 1: セキュリティ検証
                SecurityValidationResult validationResult = ValidateCodeSecurity(code, correlationId);
                if (!validationResult.IsValid)
                {
                    return CreateFailureResult("セキュリティ違反が検出されました", 
                        stopwatch.Elapsed, validationResult.Violations);
                }

                // Phase 2: コンパイル
                CompilationResult compilationResult = CompileCode(code, className, correlationId);
                if (!compilationResult.Success)
                {
                    // セキュリティ違反がある場合は専用のエラーメッセージ
                    if (compilationResult.HasSecurityViolations)
                    {
                        return CreateFailureResult("セキュリティ違反が検出されました",
                            stopwatch.Elapsed, ConvertToSecurityViolations(compilationResult.SecurityViolations));
                    }
                    
                    return CreateCompilationFailureResult("コンパイルエラーが発生しました",
                        stopwatch.Elapsed, compilationResult.Errors);
                }

                // コンパイル成功後もセキュリティ違反をチェック（Restrictedモード）
                if (compilationResult.HasSecurityViolations)
                {
                    return CreateFailureResult("セキュリティ違反が検出されました（危険なAPIコール）",
                        stopwatch.Elapsed, compilationResult.SecurityViolations);
                }

                // CompileOnly=true の場合はここで終了
                if (compileOnly)
                {
                    VibeLogger.LogInfo(
                        "compile_only_complete",
                        "Compilation completed successfully (compile-only mode)",
                        new
                        {
                            compile_time_ms = stopwatch.ElapsedMilliseconds,
                            assembly_name = compilationResult.CompiledAssembly?.FullName
                        },
                        correlationId,
                        "コンパイル専用モード完了",
                        "コンパイル結果の検証"
                    );

                    return new ExecutionResult
                    {
                        Success = true,
                        Result = null,  // CompileOnlyの場合はnullを返す（v4.0仕様）
                        ExecutionTime = stopwatch.Elapsed,
                        Logs = new List<string> { "Code compiled successfully (no execution)" }
                    };
                }

                // Phase 3: 実行 (CompileOnly=false の場合のみ)
                ExecutionResult executionResult = ExecuteCompiledCode(
                    compilationResult.CompiledAssembly, 
                    className, 
                    parameters, 
                    correlationId, 
                    cancellationToken);

                executionResult.ExecutionTime = stopwatch.Elapsed;

                // 統計更新
                UpdateStatistics(executionResult, stopwatch.Elapsed);

                VibeLogger.LogInfo(
                    "execute_code_complete",
                    $"Code execution completed: {(executionResult.Success ? "Success" : "Failed")}",
                    new
                    {
                        success = executionResult.Success,
                        execution_time_ms = stopwatch.ElapsedMilliseconds,
                        result_length = executionResult.Result?.ToString()?.Length ?? 0
                    },
                    correlationId,
                    "動的コード実行完了",
                    "実行結果とパフォーマンスの記録"
                );

                return executionResult;
#endif
            }
            catch (Exception ex)
            {
                ExecutionResult result = CreateExceptionResult("実行中に予期しないエラーが発生しました", 
                    ex, stopwatch.Elapsed);
                UpdateStatistics(result, stopwatch.Elapsed);

                VibeLogger.LogError(
                    "execute_code_exception",
                    "Unexpected error during code execution",
                    new
                    {
                        exception_type = ex.GetType().Name,
                        execution_time_ms = stopwatch.ElapsedMilliseconds
                    },
                    correlationId,
                    "動的コード実行エラー",
                    "予期しない例外の調査"
                );

                return result;
            }
        }

        /// <summary>非同期コード実行</summary>
        public async Task<ExecutionResult> ExecuteCodeAsync(
            string code,
            string className = "DynamicCommand",
            object[] parameters = null,
            CancellationToken cancellationToken = default,
            bool compileOnly = false)
        {
#if ULOOPMCP_HAS_ROSLYN
            // 実行時セキュリティチェック追加
            if (_securityLevel == DynamicCodeSecurityLevel.Disabled)
            {
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"{McpConstants.ERROR_EXECUTION_DISABLED}: {McpConstants.ERROR_MESSAGE_EXECUTION_DISABLED}",
                    ExecutionTime = TimeSpan.Zero,
                    Result = null,
                    Statistics = _statistics
                };
            }
            
            // Restrictedモードでセキュリティレベル変更をブロック（実行時チェック）
            // CurrentLevelの読み取りは許可（変更のみ禁止）
            if (_securityLevel == DynamicCodeSecurityLevel.Restricted)
            {
                if (code.Contains("SetDynamicCodeSecurityLevel") || 
                    code.Contains("InitializeFromSettings") ||
                    code.Contains("McpEditorSettings.SetDynamicCodeSecurityLevel"))
                {
                    return new ExecutionResult
                    {
                        Success = false,
                        ErrorMessage = $"{McpConstants.ERROR_SECURITY_VIOLATION}: {McpConstants.ERROR_MESSAGE_SECURITY_LEVEL_CHANGE_BLOCKED}",
                        ExecutionTime = TimeSpan.Zero,
                        Result = null,
                        Statistics = _statistics
                    };
                }
            }
#else
            // Roslyn無効時のエラーレスポンス
            return new ExecutionResult
            {
                Success = false,
                ErrorMessage = $"{McpConstants.ERROR_ROSLYN_REQUIRED}: {McpConstants.ERROR_MESSAGE_ROSLYN_REQUIRED}",
                ExecutionTime = TimeSpan.Zero,
                Statistics = _statistics
            };
#endif
            
            // JsonRpcProcessorで既にMainThreadに切り替え済み
            return await Task.FromResult(ExecuteCode(code, className, parameters, cancellationToken, compileOnly));
        }

        /// <summary>セキュリティポリシー設定</summary>
        public void SetSecurityPolicy(SecurityPolicy policy)
        {
            // SecurityValidatorにポリシーを設定する実装は後で拡張
            VibeLogger.LogInfo(
                "security_policy_updated",
                "Security policy updated",
                new
                {
                    max_execution_time = policy.MaxExecutionTimeSeconds,
                    allow_file_system = policy.AllowFileSystemAccess,
                    forbidden_namespaces_count = policy.ForbiddenNamespaces.Count
                },
                humanNote: "セキュリティポリシーの更新",
                aiTodo: "ポリシー適用の確認"
            );
        }

        /// <summary>実行統計取得</summary>
        public ExecutionStatistics GetStatistics()
        {
            lock (_statsLock)
            {
                return new ExecutionStatistics
                {
                    TotalExecutions = _statistics.TotalExecutions,
                    SuccessfulExecutions = _statistics.SuccessfulExecutions,
                    FailedExecutions = _statistics.FailedExecutions,
                    AverageExecutionTime = _statistics.AverageExecutionTime,
                    SecurityViolations = _statistics.SecurityViolations,
                    CompilationErrors = _statistics.CompilationErrors
                };
            }
        }

        // プライベートヘルパーメソッド
#if ULOOPMCP_HAS_ROSLYN
        private SecurityValidationResult ValidateCodeSecurity(string code, string correlationId)
        {
            // Restrictedモードの場合、Roslyn経由でのセキュリティ検証に任せる
            // ここでは簡易チェックのみ実施（空文字列チェックなど）
            if (string.IsNullOrWhiteSpace(code))
            {
                return new SecurityValidationResult
                {
                    IsValid = false,
                    Violations = new List<SecurityViolation>
                    {
                        new SecurityViolation
                        {
                            Type = SecurityViolationType.DangerousApiCall,
                            Description = "コードが空です",
                            LineNumber = 0,
                            CodeSnippet = string.Empty
                        }
                    }
                };
            }

            // Roslyn使用時はコンパイル時にSecurityValidatorで詳細チェックされるため
            // ここでは基本的な検証のみで通す
            VibeLogger.LogInfo(
                "security_pre_validation_passed",
                "Pre-validation passed, detailed check will be done during compilation",
                new { code_length = code.Length },
                correlationId,
                "事前検証パス（詳細はコンパイル時）",
                "コンパイル時のRoslyn検証に注目"
            );

            return new SecurityValidationResult
            {
                IsValid = true,
                Violations = new List<SecurityViolation>()
            };
        }

        private CompilationResult CompileCode(string code, string className, string correlationId)
        {
            CompilationRequest request = new CompilationRequest
            {
                Code = code,
                ClassName = className,
                Namespace = "uLoopMCP.Dynamic"
            };

            CompilationResult result = _compiler.Compile(request);

            if (!result.Success)
            {
                lock (_statsLock)
                {
                    _statistics.CompilationErrors++;
                }

                VibeLogger.LogWarning(
                    "compilation_failed",
                    $"Code compilation failed with {result.Errors.Count} errors",
                    new
                    {
                        error_count = result.Errors.Count,
                        warning_count = result.Warnings.Count
                    },
                    correlationId,
                    "コンパイル失敗",
                    "コンパイルエラーの原因分析"
                );
            }

            return result;
        }

        private ExecutionResult ExecuteCompiledCode(
            System.Reflection.Assembly assembly, 
            string className,
            object[] parameters, 
            string correlationId,
            CancellationToken cancellationToken)
        {
            // ExecutionContextを作成してExecuteメソッドを呼び出す
            ExecutionContext context = new ExecutionContext
            {
                CompiledAssembly = assembly,
                Parameters = ConvertParametersToDict(parameters ?? new object[0]),
                CancellationToken = cancellationToken
            };
            return _runner.Execute(context);
        }

        private ExecutionResult CreateFailureResult(string message, TimeSpan executionTime, 
            List<SecurityViolation> violations)
        {
            List<string> violationMessages = new List<string>();
            foreach (SecurityViolation violation in violations)
            {
                // 新しいプロパティ（Message, ApiName）を優先的に使用
                string violationMessage = !string.IsNullOrEmpty(violation.Message) 
                    ? violation.Message 
                    : violation.Description;
                    
                if (!string.IsNullOrEmpty(violation.ApiName))
                {
                    violationMessages.Add($"{violation.Type}: {violationMessage} (API: {violation.ApiName})");
                }
                else
                {
                    violationMessages.Add($"{violation.Type}: {violationMessage}");
                }
            }

            // エラーメッセージに詳細を追加
            if (violations.Count > 0)
            {
                message = $"{message} {string.Join(" ", violationMessages)}";
            }

            return new ExecutionResult
            {
                Success = false,
                ErrorMessage = message,
                Logs = violationMessages,
                ExecutionTime = executionTime
            };
        }

        private ExecutionResult CreateCompilationFailureResult(string message, TimeSpan executionTime,
            List<CompilationError> errors)
        {
            List<string> errorMessages = new List<string>();
            foreach (CompilationError error in errors)
            {
                errorMessages.Add($"Line {error.Line}: {error.Message}");
            }

            return new ExecutionResult
            {
                Success = false,
                ErrorMessage = message,
                Logs = errorMessages,
                ExecutionTime = executionTime
            };
        }

        /// <summary>
        /// CompilationResult.SecurityViolationsをSecurityValidatorのSecurityViolationに変換
        /// </summary>
        private List<SecurityViolation> ConvertToSecurityViolations(List<SecurityViolation> compilationSecurityViolations)
        {
            // 同じ型なのでそのまま返す
            return compilationSecurityViolations ?? new List<SecurityViolation>();
        }
#endif

        private ExecutionResult CreateExceptionResult(string message, Exception ex, TimeSpan executionTime)
        {
            return new ExecutionResult
            {
                Success = false,
                ErrorMessage = message,
                Exception = ex,
                Logs = new List<string> { ex.ToString() },
                ExecutionTime = executionTime
            };
        }

        private void UpdateStatistics(ExecutionResult result, TimeSpan executionTime)
        {
            lock (_statsLock)
            {
                _statistics.TotalExecutions++;

                if (result.Success)
                {
                    _statistics.SuccessfulExecutions++;
                }
                else
                {
                    _statistics.FailedExecutions++;
                }

                // 平均実行時間の更新（単純移動平均）
                double totalMs = _statistics.AverageExecutionTime.TotalMilliseconds * (_statistics.TotalExecutions - 1);
                totalMs += executionTime.TotalMilliseconds;
                _statistics.AverageExecutionTime = TimeSpan.FromMilliseconds(totalMs / _statistics.TotalExecutions);
            }
        }

        private Dictionary<string, object> ConvertParametersToDict(object[] parameters)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            if (parameters != null)
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    dict[$"param{i}"] = parameters[i];
                }
            }
            return dict;
        }
    }
}