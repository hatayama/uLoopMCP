using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Unity Game Viewをキャプチャしてファイルに保存するMCPツール
    /// 関連クラス: CaptureGameViewSchema, CaptureGameViewResponse
    /// </summary>
    [McpTool(Description = "Capture Unity Game View and save as PNG image")]
    public class CaptureGameViewTool : AbstractUnityTool<CaptureGameViewSchema, CaptureGameViewResponse>
    {
        public override string ToolName => "capture-gameview";

        private const string OUTPUT_DIRECTORY_NAME = "GameViewCaptures";

        protected override async Task<CaptureGameViewResponse> ExecuteAsync(
            CaptureGameViewSchema parameters, 
            CancellationToken cancellationToken)
        {
            string correlationId = McpConstants.GenerateCorrelationId();
            
            // VibeLogger開始ログ
            VibeLogger.LogInfo(
                "capture_gameview_start",
                "Game view capture started",
                new { ResolutionScale = parameters.ResolutionScale },
                correlationId: correlationId,
                humanNote: "User requested Game View screenshot",
                aiTodo: "Monitor capture performance and file size"
            );

            try
            {
                // パラメータ検証
                ValidateParameters(parameters);
                
                // 出力ディレクトリ作成
                string outputDirectory = EnsureOutputDirectoryExists();
                
                // Unity ScreenCapture API直接呼び出し
                Texture2D texture = await CaptureGameViewAsync(parameters.ResolutionScale, cancellationToken);
                
                try
                {
                    // ファイル保存
                    string savedPath = SaveTextureAsPng(texture, outputDirectory);
                    
                    // レスポンス生成
                    var fileInfo = new FileInfo(savedPath);
                    var response = new CaptureGameViewResponse(savedPath, fileInfo.Length);
                    
                    // VibeLogger成功ログ
                    VibeLogger.LogInfo(
                        "capture_gameview_success",
                        "Game view captured successfully",
                        new { 
                            ImagePath = response.ImagePath,
                            FileSizeBytes = response.FileSizeBytes,
                            ResolutionScale = parameters.ResolutionScale
                        },
                        correlationId: correlationId,
                        humanNote: "Screenshot saved successfully",
                        aiTodo: "Analyze capture quality if needed"
                    );
                    
                    return response;
                }
                finally
                {
                    // テクスチャのメモリ解放
                    if (texture != null)
                    {
                        UnityEngine.Object.DestroyImmediate(texture);
                    }
                    texture = null;
                }
            }
            catch (Exception ex)
            {
                // VibeLoggerエラーログ
                VibeLogger.LogError(
                    "capture_gameview_error",
                    "Game view capture failed",
                    new { Error = ex.Message, ResolutionScale = parameters.ResolutionScale },
                    correlationId: correlationId,
                    humanNote: "Screenshot capture encountered an error",
                    aiTodo: "Investigate capture failure cause"
                );
                
                // 例外を投げずに失敗レスポンスを返す
                return new CaptureGameViewResponse(failure: true);
            }
            finally
            {
                // VibeLogger完了ログ
                VibeLogger.LogInfo(
                    "capture_gameview_complete", 
                    "GameView capture operation completed",
                    correlationId: correlationId
                );
            }
        }

        /// <summary>
        /// パラメータを検証する
        /// </summary>
        private void ValidateParameters(CaptureGameViewSchema parameters)
        {
            if (parameters.ResolutionScale < 0.1f || parameters.ResolutionScale > 1.0f)
            {
                throw new ArgumentException(
                    $"ResolutionScale must be between 0.1 and 1.0, got: {parameters.ResolutionScale}");
            }
        }

        /// <summary>
        /// 出力ディレクトリが存在することを確認し、なければ作成する
        /// </summary>
        private string EnsureOutputDirectoryExists()
        {
            string projectRoot = Application.dataPath.Replace("/Assets", "");
            string outputDirectory = Path.Combine(projectRoot, "uLoopMCPOutputs", OUTPUT_DIRECTORY_NAME);
            
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
            
            return outputDirectory;
        }

        /// <summary>
        /// Game Viewをキャプチャしてテクスチャとして返す
        /// </summary>
        private async Task<Texture2D> CaptureGameViewAsync(float resolutionScale, CancellationToken cancellationToken)
        {
            // キャンセレーション確認
            cancellationToken.ThrowIfCancellationRequested();
            
            string tempFileName = $"temp_screenshot_{System.DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), tempFileName);
            
            try
            {
                // Unity再生中と停止中の両対応（万能キャプチャ処理）
                ScreenCapture.CaptureScreenshot(tempFileName, 1);
                
                if (!Application.isPlaying)
                {
                    // 停止中：GameView を1フレーム強制再描画
                    Assembly asm = typeof(EditorWindow).Assembly;
                    EditorWindow gameView = EditorWindow.GetWindow(asm.GetType("UnityEditor.GameView"));
                    gameView.Repaint();
                }
                else
                {
                    // Play中：次フレームで保存完了するようキューを回す
                    EditorApplication.QueuePlayerLoopUpdate();
                }
                
                // ファイルが作成されるまで待機（最大5秒）
                int maxAttempts = 50;
                int attempts = 0;
                while (!File.Exists(fullPath) && attempts < maxAttempts)
                {
                    await TimerDelay.Wait(100, cancellationToken);
                    attempts++;
                }
                
                if (!File.Exists(fullPath))
                {
                    throw new InvalidOperationException($"Failed to capture Game View - screenshot file was not created at: {fullPath}");
                }
                
                // ファイルサイズ確認
                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length == 0)
                {
                    throw new InvalidOperationException("Failed to capture Game View - screenshot file is empty");
                }
                
                // PNGファイルからテクスチャを読み込み
                byte[] fileData = File.ReadAllBytes(fullPath);
                Texture2D texture = new(2, 2);
                
                if (!texture.LoadImage(fileData))
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    throw new InvalidOperationException("Failed to load captured screenshot as texture");
                }
                
                // 解像度スケーリング適用
                if (!Mathf.Approximately(resolutionScale, 1.0f))
                {
                    texture = ApplyResolutionScaling(texture, resolutionScale);
                }
                
                return texture;
            }
            finally
            {
                // 一時ファイルを削除
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
        }

        /// <summary>
        /// 解像度スケーリングを適用する
        /// </summary>
        private Texture2D ApplyResolutionScaling(Texture2D originalTexture, float scale)
        {
            if (Mathf.Approximately(scale, 1.0f))
                return originalTexture;
            
            int newWidth = Mathf.RoundToInt(originalTexture.width * scale);
            int newHeight = Mathf.RoundToInt(originalTexture.height * scale);
            
            Texture2D scaledTexture = new(newWidth, newHeight, originalTexture.format, false);
            
            // RenderTextureを使用してスケーリング
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
            try
            {
                Graphics.Blit(originalTexture, rt);
                
                RenderTexture.active = rt;
                scaledTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
                scaledTexture.Apply();
                RenderTexture.active = null;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
            }
            
            // 元のテクスチャは破棄
            UnityEngine.Object.DestroyImmediate(originalTexture);
            
            return scaledTexture;
        }

        /// <summary>
        /// テクスチャをPNGファイルとして保存する
        /// </summary>
        private string SaveTextureAsPng(Texture2D texture, string outputDirectory)
        {
            // ファイル名生成
            string fileName = GenerateFileName();
            string fullPath = Path.Combine(outputDirectory, fileName);
            
            // PNG エンコード
            byte[] pngData = texture.EncodeToPNG();
            
            // ファイル書き込み
            File.WriteAllBytes(fullPath, pngData);
            
            return fullPath;
        }

        /// <summary>
        /// 一意のファイル名を生成する
        /// </summary>
        private string GenerateFileName()
        {
            DateTime now = DateTime.Now;
            string baseFileName = $"gameview_{now:yyyyMMdd_HHmmss_fff}.png";
            
            return baseFileName;
        }
    }
}