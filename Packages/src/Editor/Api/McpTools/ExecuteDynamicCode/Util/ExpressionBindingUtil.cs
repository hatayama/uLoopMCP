// Expression-based binding overloads (compile-time name safety for accessible members)
#if UNITY_EDITOR
using System;
using System.Linq.Expressions;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public static class ExpressionBindingUtil
    {
        public static bool BindObject<TComponent>(TComponent component,
            Expression<Func<TComponent, UnityEngine.Object>> field,
            UnityEngine.Object value,
            DryRunContext ctx = null, OperationSummary summary = null) where TComponent : Component
        {
            string fieldName = GetMemberName(field);
            return SerializedBindingUtil.BindObject(component, fieldName, value, ctx, summary);
        }

        public static bool BindFloat<TComponent>(TComponent component,
            Expression<Func<TComponent, float>> field,
            float value,
            DryRunContext ctx = null, OperationSummary summary = null) where TComponent : Component
        {
            string fieldName = GetMemberName(field);
            return SerializedBindingUtil.BindFloat(component, fieldName, value, ctx, summary);
        }

        public static bool BindInt<TComponent>(TComponent component,
            Expression<Func<TComponent, int>> field,
            int value,
            DryRunContext ctx = null, OperationSummary summary = null) where TComponent : Component
        {
            string fieldName = GetMemberName(field);
            return SerializedBindingUtil.BindInt(component, fieldName, value, ctx, summary);
        }

        public static bool BindBool<TComponent>(TComponent component,
            Expression<Func<TComponent, bool>> field,
            bool value,
            DryRunContext ctx = null, OperationSummary summary = null) where TComponent : Component
        {
            string fieldName = GetMemberName(field);
            return SerializedBindingUtil.BindBool(component, fieldName, value, ctx, summary);
        }

        public static bool BindVector3<TComponent>(TComponent component,
            Expression<Func<TComponent, Vector3>> field,
            Vector3 value,
            DryRunContext ctx = null, OperationSummary summary = null) where TComponent : Component
        {
            string fieldName = GetMemberName(field);
            return SerializedBindingUtil.BindVector3(component, fieldName, value, ctx, summary);
        }

        private static string GetMemberName<TComponent, TValue>(Expression<Func<TComponent, TValue>> expression)
        {
            if (expression?.Body is MemberExpression member)
            {
                return member.Member.Name;
            }
            if (expression?.Body is UnaryExpression unary && unary.Operand is MemberExpression m2)
            {
                return m2.Member.Name;
            }
            throw new ArgumentException("Expression does not reference a field or property");
        }
    }
}
#endif


