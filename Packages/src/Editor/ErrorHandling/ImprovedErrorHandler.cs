using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// 設計ドキュメント: Dynamic Tool使い勝手改善プロジェクト  
    /// 関連クラス: FriendlyMessageGenerator, ErrorTranslationDictionary
    /// </summary>
    public class ImprovedErrorHandler
    {
        private readonly ErrorTranslationDictionary _dictionary;
        private readonly FriendlyMessageGenerator _messageGenerator;

        public ImprovedErrorHandler()
        {
            _dictionary = new ErrorTranslationDictionary();
            _messageGenerator = new FriendlyMessageGenerator();
        }

        /// <summary>
        /// 実行結果のエラーを分かりやすい形に変換
        /// </summary>
        public EnhancedErrorResponse ProcessError(
            ExecutionResult originalResult, 
            string originalCode)
        {
            // エラーメッセージを取得（コンパイルエラーの場合はLogsからも確認）
            string errorMessage = originalResult.ErrorMessage ?? "";
            if (originalResult.Logs?.Any() == true)
            {
                string combinedErrors = string.Join(" ", originalResult.Logs);
                if (!string.IsNullOrEmpty(combinedErrors))
                {
                    errorMessage += " " + combinedErrors;
                }
            }
            
            // エラーパターンマッチング
            ErrorPattern pattern = _dictionary.FindPattern(errorMessage);
            
            EnhancedErrorResponse response = new()
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

        private List<string> GetLearningTips(string errorMessage)
        {
            List<string> tips = new();
            
            if (errorMessage.Contains("Top-level statements"))
            {
                tips.Add("Dynamic Toolでは、直接コードを書くだけで大丈夫です");
                tips.Add("クラスやnamespace宣言は不要です");
            }
            
            if (errorMessage.Contains("not all code paths return"))
            {
                tips.Add("コードの最後に return 文を追加してください");
                tips.Add("実行結果を文字列で返すようにしましょう");
            }
            
            if (errorMessage.Contains("ambiguous reference"))
            {
                tips.Add("UnityEngine.Object と System.Object を明確に区別しましょう");
                tips.Add("フルネーム（UnityEngine.Object）で書くと安全です");
            }
            
            return tips;
        }

        private ErrorSeverity DetermineErrorSeverity(string errorMessage)
        {
            if (errorMessage.Contains("Top-level statements") || 
                errorMessage.Contains("not all code paths"))
            {
                return ErrorSeverity.High; // よくあるパターンで重要
            }
            
            if (errorMessage.Contains("ambiguous reference"))
            {
                return ErrorSeverity.Medium;
            }
            
            return ErrorSeverity.Low;
        }
    }

    /// <summary>
    /// エラー翻訳辞書 - よくあるエラーパターンを分かりやすく翻訳
    /// </summary>
    public class ErrorTranslationDictionary
    {
        private static readonly Dictionary<string, ErrorPattern> Patterns = new()
        {
            ["Top-level statements must precede"] = new ErrorPattern
            {
                PatternRegex = @"Top-level statements must precede",
                FriendlyMessage = "コードの書き方に問題があります",
                Explanation = "Dynamic Toolでは、クラスやnamespace宣言は不要です。Unity APIを使った処理を直接書いてください。",
                Example = @"AVOID: namespace Test { class MyClass { void Method() { ... } } }

CORRECT: GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
cube.transform.position = Vector3.zero;
return ""Cube作成完了"";",
                Solutions = new List<string>
                {
                    "クラス定義を削除してメソッド内のコードのみ記述",
                    "namespace宣言を削除", 
                    "using文は残してメイン処理のみ抽出"
                }
            },
            
            ["not all code paths return a value"] = new ErrorPattern
            {
                PatternRegex = @"not all code paths return",
                FriendlyMessage = "コードの最後に戻り値が必要です",
                Explanation = "Dynamic Toolでは実行結果を返す必要があります。処理の最後に return 文を追加してください。",
                Example = @"AVOID: GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
Debug.Log(""完了"");

CORRECT: GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
Debug.Log(""完了"");
return ""Cube作成完了"";",
                Solutions = new List<string>
                {
                    "コード末尾に 'return \"実行完了\";' を追加",
                    "条件分岐の全パスにreturn文を追加",
                    "戻り値として実行結果の文字列を返す"
                }
            },

            ["Object' is an ambiguous reference"] = new ErrorPattern
            {
                PatternRegex = @"'Object' is an ambiguous reference",
                FriendlyMessage = "Objectクラスの参照が曖昧です",
                Explanation = "UnityEngine.ObjectとSystem.Objectが混在しています。明示的に指定してください。",
                Example = @"AVOID: Object.DestroyImmediate(obj);

CORRECT: UnityEngine.Object.DestroyImmediate(obj);",
                Solutions = new List<string>
                {
                    "UnityEngine.Objectを明示的に指定",
                    "フルネームでの記述を使用"
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
    /// 分かりやすいメッセージ生成機能
    /// </summary>
    public class FriendlyMessageGenerator
    {
        public string TranslateError(string originalError)
        {
            // 基本的なエラー翻訳ロジック
            if (originalError.Contains("Top-level statements"))
            {
                return "コードの書き方に問題があります。クラスやnamespace定義は不要です。";
            }
            
            if (originalError.Contains("not all code paths return"))
            {
                return "コードの最後に戻り値が必要です。'return \"完了\";'を追加してください。";
            }
            
            if (originalError.Contains("ambiguous reference"))
            {
                return "クラス参照が曖昧です。UnityEngine.Objectと明示的に書いてください。";
            }
            
            if (originalError.Contains("Identifier expected"))
            {
                return "文法エラーです。コードの構文を確認してください。";
            }
            
            return originalError; // フォールバック
        }
    }

    /// <summary>
    /// エラーパターン定義
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
    /// 拡張されたエラーレスポンス
    /// </summary>
    public class EnhancedErrorResponse
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
    /// エラーの重要度
    /// </summary>
    public enum ErrorSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
}