using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// コンパイル要求
    /// 
    /// 関連クラス: RoslynCompiler
    /// </summary>
    public class CompilationRequest
    {
        /// <summary>コード</summary>
        public string Code { get; set; }

        /// <summary>クラス名</summary>
        public string ClassName { get; set; }

        /// <summary>名前空間</summary>
        public string Namespace { get; set; }

        /// <summary>
        /// 追加参照（外部DLLファイルのパス）
        /// AppDomainに読み込まれていない外部ライブラリ（NuGetパッケージのDLL等）を
        /// ファイルパスで指定してコンパイル時に参照に追加する
        /// 例: ["C:/MyLibrary/CustomLibrary.dll", "D:/ThirdParty/SomePackage.dll"]
        /// </summary>
        public List<string> AdditionalReferences { get; set; } = new();
        
        /// <summary>アセンブリ読み込みモード（デフォルト：全アセンブリ追加）</summary>
        public AssemblyLoadingMode AssemblyMode { get; set; } = AssemblyLoadingMode.AllAssemblies;
    }

/// <summary>
/// アセンブリ読み込みモード
/// </summary>
public enum AssemblyLoadingMode
{
    /// <summary>選択されたアセンブリのみを参照</summary>
    SelectiveReference,
    
    /// <summary>全てのアセンブリを追加</summary>
    AllAssemblies
}
}