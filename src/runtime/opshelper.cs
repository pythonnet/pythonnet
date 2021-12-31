using System;
using System.Linq.Expressions;
using System.Reflection;

using static Python.Runtime.OpsHelper;

namespace Python.Runtime
{
    static class OpsHelper
    {
        public static BindingFlags BindingFlags => BindingFlags.Public | BindingFlags.Static;

        public static Func<T, T, T> Binary<T>(Func<Expression, Expression, Expression> func)
        {
            var a = Expression.Parameter(typeof(T), "a");
            var b = Expression.Parameter(typeof(T), "b");
            var body = func(a, b);
            var lambda = Expression.Lambda<Func<T, T, T>>(body, a, b);
            return lambda.Compile();
        }

        public static Func<T, T> Unary<T>(Func<Expression, Expression> func)
        {
            var value = Expression.Parameter(typeof(T), "value");
            var body = func(value);
            var lambda = Expression.Lambda<Func<T, T>>(body, value);
            return lambda.Compile();
        }

        public static bool IsOpsHelper(this MethodBase method)
            => method.DeclaringType.GetCustomAttribute<OpsAttribute>() is not null;

        public static Expression EnumUnderlyingValue(Expression enumValue)
            => Expression.Convert(enumValue, enumValue.Type.GetEnumUnderlyingType());
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal class OpsAttribute: Attribute { }

    [Ops]
    internal static class FlagEnumOps<T> where T : Enum
    {
        static readonly Func<T, T, T> and = BinaryOp(Expression.And);
        static readonly Func<T, T, T> or = BinaryOp(Expression.Or);
        static readonly Func<T, T, T> xor = BinaryOp(Expression.ExclusiveOr);

        static readonly Func<T, T> invert = UnaryOp(Expression.OnesComplement);

        public static T op_BitwiseAnd(T a, T b) => and(a, b);
        public static T op_BitwiseOr(T a, T b) => or(a, b);
        public static T op_ExclusiveOr(T a, T b) => xor(a, b);
        public static T op_OnesComplement(T value) => invert(value);

        static Expression FromNumber(Expression number)
            => Expression.Convert(number, typeof(T));

        static Func<T, T, T> BinaryOp(Func<Expression, Expression, BinaryExpression> op)
        {
            return Binary<T>((a, b) =>
            {
                var numericA = EnumUnderlyingValue(a);
                var numericB = EnumUnderlyingValue(b);
                var numericResult = op(numericA, numericB);
                return FromNumber(numericResult);
            });
        }
        static Func<T, T> UnaryOp(Func<Expression, UnaryExpression> op)
        {
            return Unary<T>(value =>
            {
                var numeric = EnumUnderlyingValue(value);
                var numericResult = op(numeric);
                return FromNumber(numericResult);
            });
        }
    }

    [Ops]
    internal static class EnumOps<T> where T : Enum
    {
        [ForbidPythonThreads]
#pragma warning disable IDE1006 // Naming Styles - must match Python
        public static PyInt __int__(T value)
#pragma warning restore IDE1006 // Naming Styles
            => typeof(T).GetEnumUnderlyingType() == typeof(UInt64)
            ? new PyInt(Convert.ToUInt64(value))
            : new PyInt(Convert.ToInt64(value));
    }
}
