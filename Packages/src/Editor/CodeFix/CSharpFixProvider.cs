#if ULOOPMCP_HAS_ROSLYN
using Microsoft.CodeAnalysis;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// C#コード修正プロバイダーの抽象基底クラス
    /// Unity AI Assistantの実装パターンに完全準拠
    /// 関連クラス: FixMissingUsings, RoslynCompiler
    /// </summary>
    public abstract class CSharpFixProvider
    {
        /// <summary>
        /// 診断情報を修正可能か判定
        /// </summary>
        public abstract bool CanFix(Diagnostic diagnostic);
        
        /// <summary>
        /// SyntaxTreeに修正を適用
        /// Unity AI Assistant準拠：Compilationパラメータ不要
        /// </summary>
        public abstract SyntaxTree ApplyFix(SyntaxTree tree, Diagnostic diagnostic);
    }
}
#endif