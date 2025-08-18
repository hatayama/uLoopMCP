namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// 別アセンブリでのテスト用クラス（安全なAPI版）
    /// DynamicAssemblyAdditionTestsで使用される
    /// ForDynamicAssemblyTestとは異なり、危険なAPIは使用しない
    /// </summary>
    public class DynamicAssemblyTest
    {
        public string HelloWorld()
        {
            return "Hello from DynamicAssemblyTest";
        }
        
        public int Add(int a, int b)
        {
            return a + b;
        }
        
        public string GetAssemblyName()
        {
            return GetType().Assembly.GetName().Name;
        }
    }
}