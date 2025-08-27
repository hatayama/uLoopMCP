// Helper to extract field names via lambda to avoid magic strings
#if UNITY_EDITOR
using System;
using System.Linq.Expressions;

namespace io.github.hatayama.uLoopMCP
{
    public static class FieldName
    {
        public static string Of<T, TValue>(Expression<Func<T, TValue>> expr)
        {
            if (expr?.Body is MemberExpression m)
            {
                return m.Member.Name;
            }
            if (expr?.Body is UnaryExpression u && u.Operand is MemberExpression m2)
            {
                return m2.Member.Name;
            }
            throw new ArgumentException("Expression does not reference a field or property");
        }
    }
}
#endif
