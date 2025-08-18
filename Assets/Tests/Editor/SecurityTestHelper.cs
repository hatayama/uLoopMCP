using System.Reflection;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    /// <summary>
    /// セキュリティ機能テスト用ヘルパークラス
    /// internal メソッドへのアクセスを提供
    /// </summary>
    internal static class SecurityTestHelper
    {
        /// <summary>
        /// DynamicCodeSecurityManagerのCurrentLevelを設定する（テスト用）
        /// イベントを発火させるためにCurrentLevelプロパティのセッターを使用
        /// </summary>
        public static void SetSecurityLevel(DynamicCodeSecurityLevel level)
        {
            // 1. McpEditorSettingsを経由して設定（これがEditorPrefsに保存される）
            McpEditorSettings.SetDynamicCodeSecurityLevel(level);
            
            // 2. DynamicCodeSecurityManagerも直接更新（念のため）
            // CurrentLevelプロパティのセッターを取得して呼び出す
            PropertyInfo currentLevelProperty = typeof(DynamicCodeSecurityManager).GetProperty(
                "CurrentLevel",
                BindingFlags.Public | BindingFlags.Static);
            
            if (currentLevelProperty != null && currentLevelProperty.CanWrite)
            {
                // internal setterを呼び出す
                currentLevelProperty.SetValue(null, level, null);
            }
            else
            {
                // セッターが見つからない場合、InitializeFromSettingsを試す
                MethodInfo initMethod = typeof(DynamicCodeSecurityManager).GetMethod(
                    "InitializeFromSettings",
                    BindingFlags.NonPublic | BindingFlags.Static);
                
                if (initMethod != null)
                {
                    initMethod.Invoke(null, new object[] { level });
                }
                else
                {
                    // 最後の手段として直接フィールドを設定
                    FieldInfo currentLevelField = typeof(DynamicCodeSecurityManager).GetField(
                        "_currentLevel",
                        BindingFlags.NonPublic | BindingFlags.Static);
                    
                    if (currentLevelField != null)
                    {
                        currentLevelField.SetValue(null, level);
                    }
                }
            }
        }
    }
}