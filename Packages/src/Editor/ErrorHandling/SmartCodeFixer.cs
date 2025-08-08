using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace io.github.hatayama.uLoopMCP.DynamicExecution
{
    /// <summary>
    /// 設計ドキュメント: Dynamic Tool使い勝手改善プロジェクト
    /// 関連クラス: ICodePattern, CodeTransformer, ValidationChecker
    /// </summary>
    public class SmartCodeFixer
    {
        private readonly List<ICodePattern> _patterns;
        private readonly CodeTransformer _transformer;

        public SmartCodeFixer()
        {
            _patterns = InitializePatterns();
            _transformer = new CodeTransformer();
        }

        /// <summary>
        /// コードを分析して自動修正を試行
        /// </summary>
        public SmartFixResult AnalyzeAndFix(string originalCode)
        {
            if (string.IsNullOrWhiteSpace(originalCode))
            {
                return new SmartFixResult
                {
                    OriginalCode = originalCode,
                    FixedCode = originalCode,
                    IsValid = false,
                    Confidence = 0.0
                };
            }

            // 1. パターン検出
            List<DetectedIssue> issues = DetectIssues(originalCode);
            
            // 2. 修正適用（優先度順）
            string fixedCode = ApplyFixes(originalCode, issues);
            
            // 3. 信頼度計算
            double confidence = CalculateConfidence(issues, fixedCode);
            
            return new SmartFixResult
            {
                OriginalCode = originalCode,
                FixedCode = fixedCode,
                AppliedFixes = ConvertToAppliedFixes(issues),
                IsValid = confidence > 0.5,
                Confidence = confidence,
                TotalIssuesFound = issues.Count,
                IssuesFixed = issues.Count(i => i.CanFix)
            };
        }

        private List<ICodePattern> InitializePatterns()
        {
            return new List<ICodePattern>
            {
                new MissingReturnPattern(),
                new ObjectAmbiguityPattern(),
                new UsingStatementPattern()
            };
        }

        private List<DetectedIssue> DetectIssues(string code)
        {
            List<DetectedIssue> issues = new();
            
            foreach (ICodePattern pattern in _patterns.OrderBy(p => p.Priority))
            {
                if (pattern.CanDetect(code))
                {
                    DetectedIssue issue = new()
                    {
                        PatternName = pattern.PatternName,
                        Position = pattern.FindPosition(code),
                        Severity = pattern.Severity,
                        Description = pattern.GetDescription(),
                        CanFix = true,
                        Fix = pattern.CreateFix(code)
                    };
                    issues.Add(issue);
                }
            }
            
            return issues;
        }

        private string ApplyFixes(string originalCode, List<DetectedIssue> issues)
        {
            string currentCode = originalCode;
            
            // 優先度順に修正を適用
            foreach (DetectedIssue issue in issues.Where(i => i.CanFix).OrderBy(i => i.Severity))
            {
                try
                {
                    currentCode = _transformer.ApplyFix(currentCode, issue.Fix);
                }
                catch (Exception ex)
                {
                    VibeLogger.LogWarning(
                        "smart_fix_failed",
                        $"Failed to apply fix for pattern: {issue.PatternName}",
                        new { pattern = issue.PatternName, error = ex.Message },
                        humanNote: "自動修正の適用に失敗",
                        aiTodo: "修正パターンの改善が必要"
                    );
                }
            }
            
            return currentCode;
        }

        private double CalculateConfidence(List<DetectedIssue> issues, string fixedCode)
        {
            if (!issues.Any()) return 1.0; // 問題なし
            
            int fixableIssues = issues.Count(i => i.CanFix);
            int totalIssues = issues.Count;
            
            double baseConfidence = (double)fixableIssues / totalIssues;
            
            // 高優先度の問題が修正できた場合は信頼度を上げる
            bool hasHighPriorityFixes = issues.Any(i => i.Severity == IssueSeverity.High && i.CanFix);
            if (hasHighPriorityFixes)
            {
                baseConfidence += 0.2;
            }
            
            return Math.Min(1.0, baseConfidence);
        }

        private List<AppliedFix> ConvertToAppliedFixes(List<DetectedIssue> issues)
        {
            return issues.Where(i => i.CanFix).Select(issue => new AppliedFix
            {
                PatternName = issue.PatternName,
                Description = issue.Description,
                Position = issue.Position,
                Severity = issue.Severity,
                Type = issue.Fix.FixType
            }).ToList();
        }
    }

    /// <summary>
    /// コード修正パターンのインターフェース
    /// </summary>
    public interface ICodePattern
    {
        string PatternName { get; }
        bool CanDetect(string code);
        CodeFix CreateFix(string code);
        int Priority { get; }
        IssueSeverity Severity { get; }
        int FindPosition(string code);
        string GetDescription();
    }

    /// <summary>
    /// 戻り値なしパターンの検出・修正
    /// </summary>
    public class MissingReturnPattern : ICodePattern
    {
        public string PatternName => "MissingReturn";
        public int Priority => 100;
        public IssueSeverity Severity => IssueSeverity.High;
        
        public bool CanDetect(string code)
        {
            string trimmedCode = code.Trim();
            
            // return文がない場合
            if (!trimmedCode.Contains("return "))
            {
                return true;
            }
            
            // メソッド呼び出しで終わっている場合（戻り値を忘れがち）
            if (trimmedCode.EndsWith(");") && !trimmedCode.EndsWith("return "))
            {
                return true;
            }
            
            return false;
        }
        
        public CodeFix CreateFix(string code)
        {
            return new CodeFix
            {
                FixType = FixType.Append,
                Position = code.Length,
                NewCode = "\n\nreturn \"実行完了\";",
                Description = "戻り値を追加しました"
            };
        }
        
        public int FindPosition(string code) => code.Length;
        public string GetDescription() => "メソッドに戻り値が必要です";
    }

    /// <summary>
    /// Object曖昧参照パターンの検出・修正
    /// </summary>
    public class ObjectAmbiguityPattern : ICodePattern
    {
        public string PatternName => "ObjectAmbiguity";
        public int Priority => 90;
        public IssueSeverity Severity => IssueSeverity.Medium;
        
        public bool CanDetect(string code)
        {
            // "Object."で始まる行があり、UnityEngine.Objectの明示がない
            return Regex.IsMatch(code, @"\bObject\.", RegexOptions.Multiline) &&
                   !code.Contains("UnityEngine.Object");
        }
        
        public CodeFix CreateFix(string code)
        {
            string fixedCode = Regex.Replace(code, @"\bObject\.", "UnityEngine.Object.");
            
            return new CodeFix
            {
                FixType = FixType.Replace,
                Position = 0,
                NewCode = fixedCode,
                Description = "ObjectをUnityEngine.Objectに明示しました"
            };
        }
        
        public int FindPosition(string code)
        {
            Match match = Regex.Match(code, @"\bObject\.");
            return match.Success ? match.Index : 0;
        }
        
        public string GetDescription() => "Object参照が曖昧です";
    }

    /// <summary>
    /// Using文不足パターンの検出・修正
    /// </summary>
    public class UsingStatementPattern : ICodePattern
    {
        public string PatternName => "MissingUsing";
        public int Priority => 80;
        public IssueSeverity Severity => IssueSeverity.Low;
        
        public bool CanDetect(string code)
        {
            // UnityEditor APIを使っているがusing文がない
            return (code.Contains("EditorApplication") || 
                    code.Contains("EditorUtility") ||
                    code.Contains("AssetDatabase")) &&
                   !code.Contains("using UnityEditor");
        }
        
        public CodeFix CreateFix(string code)
        {
            return new CodeFix
            {
                FixType = FixType.Prepend,
                Position = 0,
                NewCode = "using UnityEditor;\n",
                Description = "UnityEditor using文を追加しました"
            };
        }
        
        public int FindPosition(string code) => 0;
        public string GetDescription() => "UnityEditor using文が不足しています";
    }

    /// <summary>
    /// コード変換機能
    /// </summary>
    public class CodeTransformer
    {
        public string ApplyFix(string code, CodeFix fix)
        {
            switch (fix.FixType)
            {
                case FixType.Replace:
                    return fix.NewCode;
                    
                case FixType.Append:
                    return code + fix.NewCode;
                    
                case FixType.Prepend:
                    return fix.NewCode + code;
                    
                case FixType.Insert:
                    if (fix.Position >= 0 && fix.Position <= code.Length)
                    {
                        return code.Insert(fix.Position, fix.NewCode);
                    }
                    return code;
                    
                default:
                    return code;
            }
        }
    }

    /// <summary>
    /// 検出された問題
    /// </summary>
    public class DetectedIssue
    {
        public string PatternName { get; set; } = "";
        public int Position { get; set; }
        public IssueSeverity Severity { get; set; }
        public string Description { get; set; } = "";
        public bool CanFix { get; set; }
        public CodeFix Fix { get; set; }
    }

    /// <summary>
    /// コード修正定義
    /// </summary>
    public class CodeFix
    {
        public FixType FixType { get; set; }
        public int Position { get; set; }
        public string NewCode { get; set; } = "";
        public string Description { get; set; } = "";
    }

    /// <summary>
    /// 修正タイプ
    /// </summary>
    public enum FixType
    {
        Replace,
        Insert,
        Append,
        Prepend,
        Remove
    }

    /// <summary>
    /// スマート修正結果
    /// </summary>
    public class SmartFixResult
    {
        public string OriginalCode { get; set; } = "";
        public string FixedCode { get; set; } = "";
        public List<AppliedFix> AppliedFixes { get; set; } = new();
        public bool IsValid { get; set; }
        public double Confidence { get; set; }
        public int TotalIssuesFound { get; set; }
        public int IssuesFixed { get; set; }
    }

    /// <summary>
    /// 適用された修正
    /// </summary>
    public class AppliedFix
    {
        public string PatternName { get; set; } = "";
        public string Description { get; set; } = "";
        public int Position { get; set; }
        public IssueSeverity Severity { get; set; }
        public FixType Type { get; set; }
    }

    /// <summary>
    /// 問題の重要度
    /// </summary>
    public enum IssueSeverity
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }
}