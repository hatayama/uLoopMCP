using System.Runtime.CompilerServices;

// Only the tests see these internals: tools use this module through its entry point alone, and the
// compiler keeps them away from the parts behind it.
[assembly: InternalsVisibleTo("UnityCLILoop.Tests.Editor")]
[assembly: InternalsVisibleTo("UnityCLILoop.Tests.PlayMode")]
