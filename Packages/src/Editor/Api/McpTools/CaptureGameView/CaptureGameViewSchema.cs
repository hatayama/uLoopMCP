using System.ComponentModel;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// GameViewキャプチャツールのパラメータスキーマ
    /// 関連クラス: CaptureGameViewTool, CaptureGameViewResponse
    /// 設計ドキュメント: docs/design/gameview-capture-tool-design.md
    /// </summary>
    public class CaptureGameViewSchema : BaseToolSchema
    {
        /// <summary>
        /// 解像度倍率 (0.1 - 1.0, デフォルト: 1.0)
        /// 1.0で等倍、0.5で半分の解像度、0.1で10%の解像度
        /// </summary>
        [Description("Resolution scale multiplier (0.1 to 1.0, where 1.0 is original size)")]
        public float ResolutionScale { get; set; } = 1.0f;
    }
}