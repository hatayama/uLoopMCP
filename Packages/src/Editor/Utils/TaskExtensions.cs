using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Task related extension methods
    /// Provides convenient methods for fire-and-forget async operations
    /// </summary>
    public static class TaskExtensions
    {
        /// <summary>
        /// Fire-and-forget extension method for Task
        /// Equivalent to "_ = task;" but more explicit and readable
        /// </summary>
        /// <param name="task">Task to fire and forget</param>
        public static void Forget(this Task task)
        {
            // Intentionally discard the task
            // This makes fire-and-forget operations explicit
        }
        
        /// <summary>
        /// Fire-and-forget extension method for DelayFrameAwaitable
        /// Allows writing: EditorDelay.DelayFrame().Forget()
        /// </summary>
        /// <param name="awaitable">DelayFrameAwaitable to fire and forget</param>
        public static void Forget(this DelayFrameAwaitable awaitable)
        {
            // Convert to Task and discard
            _ = ConvertToTask(awaitable);
        }
        
        /// <summary>
        /// Convert DelayFrameAwaitable to Task for fire-and-forget operations
        /// </summary>
        private static async Task ConvertToTask(DelayFrameAwaitable awaitable)
        {
            await awaitable;
        }
    }
}