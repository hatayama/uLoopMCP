#if ULOOPMCP_HAS_ROSLYN
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Roslynの構文木を走査してセキュリティ違反を検出するSyntaxWalker
    /// 危険なAPI呼び出し、型の継承、インスタンス生成などを包括的に検査する。
    /// CSharpSyntaxWalkerを継承し、各種構文要素に対するVisitメソッドを実装。
    /// 検出した違反はSecurityViolationとして記録し、呼び出し元に報告する。
    /// 関連クラス: DangerousApiDetector（危険API判定）, SecurityValidator（検証エントリポイント）
    /// </summary>
    public class SecuritySyntaxWalker : CSharpSyntaxWalker
    {
        private readonly List<SecurityViolation> violations = new();
        private readonly SemanticModel semanticModel;
        private readonly DangerousApiDetector apiDetector;
        
        public IReadOnlyList<SecurityViolation> Violations => violations;
        
        public SecuritySyntaxWalker(SemanticModel model) : base(SyntaxWalkerDepth.Node)
        {
            semanticModel = model;
            apiDetector = new DangerousApiDetector();
        }
        
        /// <summary>
        /// クラス宣言ノードを訪問し、継承関係のセキュリティ違反を検査する。
        /// 基底クラスやインターフェースが危険な型でないかをチェックし、
        /// 違反が見つかった場合はDangerousInheritanceタイプの違反として記録する。
        /// </summary>
        /// <param name="node">検査対象のクラス宣言構文ノード</param>
        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            // 基底クラスの検査
            if (node.BaseList != null)
            {
                foreach (BaseTypeSyntax baseType in node.BaseList.Types)
                {
                    ITypeSymbol typeSymbol = semanticModel.GetTypeInfo(baseType.Type).Type;
                    if (typeSymbol != null && apiDetector.IsDangerousType(typeSymbol))
                    {
                        violations.Add(new SecurityViolation
                        {
                            Type = SecurityViolationType.DangerousInheritance,
                            Description = $"Class inherits from dangerous type: {typeSymbol}",
                            Message = $"Class inherits from dangerous type: {typeSymbol}",
                            Location = node.GetLocation(),
                            ApiName = typeSymbol.ToDisplayString()
                        });
                    }
                }
            }
            
            base.VisitClassDeclaration(node);
        }
        
        /// <summary>
        /// メソッド宣言ノードを訪問し、メソッド本体内のセキュリティ違反を検査する。
        /// ブロック形式とExpression-bodied形式の両方に対応し、
        /// メソッド内で使用される危険なAPIや型を検出する。
        /// </summary>
        /// <param name="node">検査対象のメソッド宣言構文ノード</param>
        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            // メソッド内の処理を検査
            CheckMethodBody(node.Body, node.ExpressionBody);
            base.VisitMethodDeclaration(node);
        }
        
        /// <summary>
        /// プロパティ宣言ノードを訪問し、getter/setter内のセキュリティ違反を検査する。
        /// 従来のアクセサ形式とExpression-bodied形式の両方に対応し、
        /// プロパティ実装内で使用される危険なAPIや型を検出する。
        /// </summary>
        /// <param name="node">検査対象のプロパティ宣言構文ノード</param>
        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            // getterとsetterの検査
            if (node.AccessorList != null)
            {
                foreach (AccessorDeclarationSyntax accessor in node.AccessorList.Accessors)
                {
                    CheckMethodBody(accessor.Body, accessor.ExpressionBody);
                }
            }
            
            // Expression-bodied propertyの検査
            if (node.ExpressionBody != null)
            {
                CheckExpression(node.ExpressionBody.Expression);
            }
            
            base.VisitPropertyDeclaration(node);
        }
        
        /// <summary>
        /// コンストラクタ宣言ノードを訪問し、初期化処理のセキュリティ違反を検査する。
        /// コンストラクタ本体内で使用される危険なAPIや型のインスタンス化を検出し、
        /// オブジェクト初期化時のセキュリティリスクを特定する。
        /// </summary>
        /// <param name="node">検査対象のコンストラクタ宣言構文ノード</param>
        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            CheckMethodBody(node.Body, node.ExpressionBody);
            base.VisitConstructorDeclaration(node);
        }
        
        /// <summary>
        /// 単純ラムダ式（パラメータが1つでかっこ無し）を訪問し、セキュリティ違反を検査する。
        /// ラムダ本体内で使用される危険なAPIや型を検出し、
        /// 匿名関数として実行されるコードのセキュリティリスクを特定する。
        /// </summary>
        /// <param name="node">検査対象の単純ラムダ式構文ノード</param>
        public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
        {
            CheckLambdaBody(node.Body);
            base.VisitSimpleLambdaExpression(node);
        }
        
        /// <summary>
        /// 括弧付きラムダ式（複数パラメータまたは型指定あり）を訪問し、セキュリティ違反を検査する。
        /// ラムダ本体内で使用される危険なAPIや型を検出し、
        /// デリゲートやイベントハンドラとして使用されるコードのセキュリティリスクを特定する。
        /// </summary>
        /// <param name="node">検査対象の括弧付きラムダ式構文ノード</param>
        public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
            CheckLambdaBody(node.Body);
            base.VisitParenthesizedLambdaExpression(node);
        }
        
        /// <summary>
        /// 匿名メソッド（delegate キーワード使用）を訪問し、セキュリティ違反を検査する。
        /// 匿名メソッド本体内で使用される危険なAPIや型を検出し、
        /// デリゲートとして実行される匿名コードのセキュリティリスクを特定する。
        /// </summary>
        /// <param name="node">検査対象の匿名メソッド式構文ノード</param>
        public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
        {
            CheckLambdaBody(node.Body);
            base.VisitAnonymousMethodExpression(node);
        }
        
        /// <summary>
        /// ローカル関数（メソッド内で定義される関数）を訪問し、セキュリティ違反を検査する。
        /// ローカル関数本体内で使用される危険なAPIや型を検出し、
        /// メソッド内で定義・実行される関数のセキュリティリスクを特定する。
        /// </summary>
        /// <param name="node">検査対象のローカル関数ステートメント構文ノード</param>
        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            CheckMethodBody(node.Body, node.ExpressionBody);
            base.VisitLocalFunctionStatement(node);
        }
        
        /// <summary>
        /// メンバーアクセス式（ドット演算子による呼び出し）を訪問し、危険なAPI呼び出しを検出する。
        /// プロパティ、メソッド、フィールドへのアクセスを検査し、
        /// DangerousApiDetectorと連携して危険なAPIパターンをマッチングする。
        /// 違反はDangerousApiCallタイプとして記録される。
        /// </summary>
        /// <param name="node">検査対象のメンバーアクセス式構文ノード</param>
        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            ISymbol symbol = semanticModel.GetSymbolInfo(node).Symbol;
            if (symbol != null)
            {
                string fullName = GetFullSymbolName(symbol);
                if (apiDetector.IsDangerousApi(fullName))
                {
                    violations.Add(new SecurityViolation
                    {
                        Type = SecurityViolationType.DangerousApiCall,
                        Description = $"Dangerous API detected: {fullName}",
                        Message = $"Dangerous API detected: {fullName}",
                        Location = node.GetLocation(),
                        ApiName = fullName
                    });
                }
            }
            
            base.VisitMemberAccessExpression(node);
        }
        
        /// <summary>
        /// 識別子名（変数名、型名、メソッド名など）を訪問し、危険な型の直接参照を検出する。
        /// 型として解決される識別子が危険な型でないかをチェックし、
        /// System.Diagnostics.Processなどの危険な型への直接参照を特定する。
        /// 違反はDangerousApiCallタイプとして記録される。
        /// </summary>
        /// <param name="node">検査対象の識別子名構文ノード</param>
        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            ISymbol symbol = semanticModel.GetSymbolInfo(node).Symbol;
            if (symbol is ITypeSymbol typeSymbol)
            {
                string fullName = typeSymbol.ToDisplayString();
                if (apiDetector.IsDangerousApi(fullName))
                {
                    violations.Add(new SecurityViolation
                    {
                        Type = SecurityViolationType.DangerousApiCall,
                        Description = $"Dangerous type usage detected: {fullName}",
                        Message = $"Dangerous type usage detected: {fullName}",
                        Location = node.GetLocation(),
                        ApiName = fullName
                    });
                }
            }
            
            base.VisitIdentifierName(node);
        }
        
        /// <summary>
        /// オブジェクト生成式（new演算子）を訪問し、危険な型のインスタンス化を検出する。
        /// 生成される型がDangerousApiDetectorの危険型リストに含まれるかをチェックし、
        /// ProcessやWebClientなどの危険なオブジェクトの生成を特定する。
        /// 違反はDangerousTypeCreationタイプとして記録される。
        /// </summary>
        /// <param name="node">検査対象のオブジェクト生成式構文ノード</param>
        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            ITypeSymbol typeSymbol = semanticModel.GetTypeInfo(node).Type;
            if (typeSymbol != null && apiDetector.IsDangerousType(typeSymbol))
            {
                violations.Add(new SecurityViolation
                {
                    Type = SecurityViolationType.DangerousTypeCreation,
                    Description = $"Creating instance of dangerous type: {typeSymbol}",
                    Message = $"Creating instance of dangerous type: {typeSymbol}",
                    Location = node.GetLocation(),
                    ApiName = typeSymbol.ToDisplayString()
                });
            }
            
            base.VisitObjectCreationExpression(node);
        }
        
        /// <summary>
        /// メソッド呼び出し式を訪問し、危険なメソッドの実行を検出する。
        /// 呼び出されるメソッドのシンボル情報を解析し、
        /// File.Delete、Process.Startなどの危険なメソッド呼び出しを特定する。
        /// 違反はDangerousApiCallタイプとして記録される。
        /// </summary>
        /// <param name="node">検査対象の呼び出し式構文ノード</param>
        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            IMethodSymbol methodSymbol = semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;
            if (methodSymbol != null)
            {
                string fullName = GetFullMethodName(methodSymbol);
                if (apiDetector.IsDangerousApi(fullName))
                {
                    violations.Add(new SecurityViolation
                    {
                        Type = SecurityViolationType.DangerousApiCall,
                        Description = $"Dangerous method call detected: {fullName}",
                        Message = $"Dangerous method call detected: {fullName}",
                        Location = node.GetLocation(),
                        ApiName = fullName
                    });
                }
            }
            
            base.VisitInvocationExpression(node);
        }
        
        /// <summary>
        /// メソッド本体の検査を共通化したヘルパーメソッド。
        /// ブロック形式（{}で囲まれた複数ステートメント）とExpression-bodied形式（=>式）の
        /// 両方に対応し、適切な検査メソッドを呼び出す。
        /// メソッド、コンストラクタ、プロパティアクセサ等から共通で使用される。
        /// </summary>
        /// <param name="block">ブロック形式のメソッド本体（nullの場合あり）</param>
        /// <param name="expressionBody">Expression-bodied形式のメソッド本体（nullの場合あり）</param>
        private void CheckMethodBody(BlockSyntax block, ArrowExpressionClauseSyntax expressionBody)
        {
            if (block != null)
            {
                Visit(block);
            }
            
            if (expressionBody != null)
            {
                CheckExpression(expressionBody.Expression);
            }
        }
        
        /// <summary>
        /// ラムダ式や匿名メソッドの本体を検査する共通ヘルパーメソッド。
        /// ラムダ本体がブロック形式か単一式かを判定し、
        /// 適切な検査メソッドを呼び出す。
        /// 単純ラムダ、括弧付きラムダ、匿名メソッド全てから共通で使用される。
        /// </summary>
        /// <param name="body">ラムダまたは匿名メソッドの本体（BlockSyntaxまたはExpressionSyntax）</param>
        private void CheckLambdaBody(CSharpSyntaxNode body)
        {
            if (body is BlockSyntax block)
            {
                Visit(block);
            }
            else if (body is ExpressionSyntax expression)
            {
                CheckExpression(expression);
            }
        }
        
        /// <summary>
        /// 単一の式を検査するヘルパーメソッド。
        /// Expression-bodiedメンバーやラムダ式の単一式本体を検査する際に使用され、
        /// Visitメソッドを呼び出して再帰的に構文木を走査する。
        /// </summary>
        /// <param name="expression">検査対象の式構文ノード</param>
        private void CheckExpression(ExpressionSyntax expression)
        {
            Visit(expression);
        }
        
        /// <summary>
        /// シンボルの完全修飾名を取得するヘルパーメソッド。
        /// 名前空間を含む完全な名前（例：System.Diagnostics.Process）を返し、
        /// DangerousApiDetectorでのマッチングに使用される。
        /// RoslynのSymbolDisplayFormat.FullyQualifiedFormatを使用して正確な名前を取得する。
        /// </summary>
        /// <param name="symbol">名前を取得するシンボル</param>
        /// <returns>完全修飾名の文字列</returns>
        private string GetFullSymbolName(ISymbol symbol)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
        
        /// <summary>
        /// メソッドシンボルの完全名（型名.メソッド名）を取得するヘルパーメソッド。
        /// メソッドが所属する型の名前とメソッド名を結合して返し（例：System.IO.File.Delete）、
        /// DangerousApiDetectorでの危険なメソッドパターンマッチングに使用される。
        /// 所属型が不明な場合はメソッド名のみを返す。
        /// </summary>
        /// <param name="method">名前を取得するメソッドシンボル</param>
        /// <returns>型名.メソッド名形式の文字列</returns>
        private string GetFullMethodName(IMethodSymbol method)
        {
            string typeName = method.ContainingType?.ToDisplayString() ?? "";
            string methodName = method.Name;
            return string.IsNullOrEmpty(typeName) ? methodName : $"{typeName}.{methodName}";
        }
    }
}
#endif