#nullable enable

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// GameViewキャプチャツールのレスポンス
    /// 関連クラス: CaptureGameViewTool, CaptureGameViewSchema
    /// </summary>
    public class CaptureGameViewResponse : BaseToolResponse
    {
        /// <summary>
        /// 保存された画像ファイルの絶対パス（キャプチャ失敗時はnull）
        /// </summary>
        public string? ImagePath { get; set; }

        /// <summary>
        /// 保存されたファイルのサイズ（バイト単位、キャプチャ失敗時はnull）
        /// </summary>
        public long? FileSizeBytes { get; set; }

        /// <summary>
        /// 成功レスポンス生成用コンストラクタ
        /// </summary>
        public CaptureGameViewResponse(string imagePath, long fileSizeBytes)
        {
            ImagePath = imagePath;
            FileSizeBytes = fileSizeBytes;
        }

        /// <summary>
        /// 失敗レスポンス生成用コンストラクタ
        /// </summary>
        public CaptureGameViewResponse(bool failure)
        {
            ImagePath = null;
            FileSizeBytes = null;
        }

        /// <summary>
        /// デフォルトコンストラクタ（JSON デシリアライゼーション用）
        /// </summary>
        public CaptureGameViewResponse()
        {
        }
    }
}