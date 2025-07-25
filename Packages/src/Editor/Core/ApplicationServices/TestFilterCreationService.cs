using System;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// テストフィルター作成サービス
    /// 単一機能：テスト実行用フィルターの作成を行う
    /// 関連クラス: RunTestsTool, RunTestsUseCase, TestExecutionFilter
    /// 設計書参照: DDDリファクタリング仕様 - Application Service Layer
    /// </summary>
    public class TestFilterCreationService
    {
        /// <summary>
        /// テスト実行フィルターを作成する
        /// </summary>
        /// <param name="filterType">フィルタータイプ</param>
        /// <param name="filterValue">フィルター値</param>
        /// <returns>テスト実行フィルター</returns>
        public TestExecutionFilter CreateFilter(TestFilterType filterType, string filterValue)
        {
            return filterType switch
            {
                TestFilterType.all => TestExecutionFilter.All(),
                TestFilterType.exact => TestExecutionFilter.ByTestName(filterValue),
                TestFilterType.regex => TestExecutionFilter.ByClassName(filterValue),
                TestFilterType.assembly => TestExecutionFilter.ByAssemblyName(filterValue),
                _ => throw new ArgumentException($"Unsupported filter type: {filterType}")
            };
        }
    }
}