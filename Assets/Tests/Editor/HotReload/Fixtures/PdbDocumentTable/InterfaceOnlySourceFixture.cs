namespace io.github.hatayama.UnityCliLoop.Tests.Editor.HotReload
{
    /// <summary>
    /// A source with no method body, so the compiled PDB lists it in its document table but no sequence point refers to it.
    /// </summary>
    internal interface IPdbDocumentTableFixture
    {
        void Run();
    }
}
