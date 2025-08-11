using System.IO;
using System.Diagnostics;

namespace io.github.hatayama.uLoopMCP
{
    public class ForAssemblyCSharpTest
    {
        public string HelloWorld()
        {
            return "Hello World";
        }

        public string TestForbiddenOperations()
        {
            // 禁止されたファイル操作
            File.WriteAllText("/tmp/malicious.txt", "malicious content");
            
            // 禁止されたプロセス実行
            Process.Start("notepad.exe");
            
            return "Forbidden operations executed";
        }
    }
}