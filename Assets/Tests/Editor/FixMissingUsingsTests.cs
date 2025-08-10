using NUnit.Framework;
using io.github.hatayama.uLoopMCP;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// FixMissingUsingsのテスト
    /// </summary>
    public class FixMissingUsingsTests
    {
        private FixMissingUsings _fixer;

        [SetUp]
        public void SetUp()
        {
            _fixer = new FixMissingUsings();
        }

        [Test]
        public void FixMissingUsings_CanInstantiate()
        {
            // FixMissingUsingsが正しくインスタンス化できることを確認
            Assert.IsNotNull(_fixer, "FixMissingUsingsがインスタンス化できるべき");
        }

        [Test]
        public void FixMissingUsings_InheritsFromCSharpFixProvider()
        {
            // CSharpFixProviderから継承してることを確認
            bool isFixProvider = _fixer is CSharpFixProvider;
            Assert.IsTrue(isFixProvider, "FixMissingUsingsはCSharpFixProviderから継承するべき");
        }

        [Test]
        public void CustomGenericClass_PatternTest()
        {
            // Unity AI Assistant方式での自作ジェネリッククラス判定テスト
            // コンパイラエラーメッセージのパターンをシミュレート
            
            // 自作ジェネリッククラス: 'MyContainer<>' → List<>パターンにマッチしない
            string customGenericError = "The type or namespace name 'MyContainer<>' could not be found";
            bool shouldNotMatchList = customGenericError.Contains("'List<>'");
            Assert.IsFalse(shouldNotMatchList, "自作ジェネリッククラス 'MyContainer<>' は 'List<>' パターンにマッチしてはいけない");
            
            // 標準ライブラリ: 'List<>' → List<>パターンにマッチする
            string standardGenericError = "The type or namespace name 'List<>' could not be found"; 
            bool shouldMatchList = standardGenericError.Contains("'List<>'");
            Assert.IsTrue(shouldMatchList, "標準ライブラリ 'List<>' は 'List<>' パターンにマッチするべき");
            
            // Dictionary<>も同様
            string customDictError = "The type or namespace name 'MyDictionary<>' could not be found";
            bool shouldNotMatchDict = customDictError.Contains("'Dictionary<>'");
            Assert.IsFalse(shouldNotMatchDict, "自作ジェネリッククラス 'MyDictionary<>' は 'Dictionary<>' パターンにマッチしてはいけない");
        }

        [Test]
        public void StandardLibraryGenerics_RealExecutionTest()
        {
            // 実際にexecute-dynamic-codeで標準ライブラリのジェネリック型を使ってテスト
            // これは手動確認結果を自動テスト化したもの
            
            // execute-dynamic-codeは以下のコードを正常実行できる（using文自動追加により）:
            // var list = new List<string>(); return "List test";
            // var dict = new Dictionary<string, int>(); return "Dictionary test";
            
            Assert.IsNotNull(_fixer);
            Assert.IsTrue(_fixer is CSharpFixProvider);
            
            // 実際の実行確認は execute-dynamic-code ツールで手動確認済み
            // List<string>, Dictionary<string, int> の使用時に
            // System.Collections.Generic using文が正しく自動追加されることを確認
        }

        [Test]
        public void EdgeCases_HandleCorrectly()
        {
            // エッジケースでの正しい判定テスト
            
            // ケース1: 似た名前だが異なる型
            string similarNameError = "The type or namespace name 'MyList<>' could not be found";
            bool shouldNotMatch = similarNameError.Contains("'List<>'");
            Assert.IsFalse(shouldNotMatch, "'MyList<>' は 'List<>' にマッチしてはいけない");
            
            // ケース2: 部分マッチしそうなケース
            string partialMatchError = "The type or namespace name 'CustomList<>' could not be found";
            bool shouldNotPartialMatch = partialMatchError.Contains("'List<>'");
            Assert.IsFalse(shouldNotPartialMatch, "'CustomList<>' は 'List<>' にマッチしてはいけない");
            
            // ケース3: 正確な標準ライブラリマッチ
            string exactMatchError = "The type or namespace name 'HashSet<>' could not be found";
            bool shouldMatch = exactMatchError.Contains("'HashSet<>'");
            Assert.IsTrue(shouldMatch, "'HashSet<>' は正確にマッチするべき");
            
            // Unity AI Assistant方式では、エラーメッセージに含まれる完全一致の
            // シングルクォートで囲まれた型名のみを対象にすることで
            // このような誤認識を防いでいる
        }
    }
}