using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace NonCopyable
{
    public static class TypeExtensions
    {
        public static bool IsNonCopyable(this ITypeSymbol t)
        {
            if (t == null) return false;
            if (t.TypeKind != TypeKind.Struct && t.TypeKind != TypeKind.TypeParameter) return false;

            if (HasNonCopyableAttribute(t)) return true;

            if (t.TypeKind == TypeKind.Struct)
            {
                foreach (var ifType in t.AllInterfaces)
                {
                    if (HasNonCopyableAttribute(ifType)) return true;
                }
            }
            else
            {
                foreach (var constraint in ((ITypeParameterSymbol)t).ConstraintTypes)
                {
                    if (HasNonCopyableAttribute(constraint)) return true;
                }

            }

            return false;
        }

        private static bool HasNonCopyableAttribute(ITypeSymbol t)
        {
            foreach (var a in t.GetAttributes())
            {
                var str = a.AttributeClass.Name;
                if (str.EndsWith("NonCopyable") || str.EndsWith("NonCopyableAttribute")) return true;
            }

            return false;
        }

        private static SyntaxList<AttributeListSyntax> GetAttributes(this SyntaxNode syntax)
        {
            switch (syntax)
            {
                case StructDeclarationSyntax s:
                    return s.AttributeLists;
                case TypeParameterSyntax s:
                    return s.AttributeLists;
                default:
                    return default;
            }
        }

        /// <summary>
        /// test whether op is copyable or not even when it is a non-copyable instance.
        /// </summary>
        public static bool CanCopy(this IOperation op)
        {
            var k = op.Kind;

            if (k == OperationKind.Conversion)
            {
                var operandKind = ((IConversionOperation)op).Operand.Kind;
                // default literal (invalid if LangVersion < 7.1)
                if (operandKind == OperationKind.DefaultValue || operandKind == OperationKind.Invalid) return true;
            }

            if (k == OperationKind.LocalReference || k == OperationKind.FieldReference || k == OperationKind.PropertyReference || k == OperationKind.ArrayElementReference)
            {
                //need help: how to get ref-ness from IOperation?
                var parent = op.Syntax.Parent.Kind();
                if (parent == SyntaxKind.RefExpression) return true;
            }

            if (k == OperationKind.Conditional)
            {
                var cond = (IConditionalOperation)op;
                return cond.WhenFalse.CanCopy() && cond.WhenFalse.CanCopy();
            }

            return k == OperationKind.ObjectCreation
                || k == OperationKind.DefaultValue
                || k == OperationKind.Literal
                || k == OperationKind.Invocation
                // workaround for https://github.com/dotnet/roslyn/issues/49751
                || !IsValid(k) && op.Syntax is InvocationExpressionSyntax;

            //todo: should return value be OK?
            //todo: move semantics
        }

        static bool IsValid(OperationKind kind)
            => kind != OperationKind.None && kind != OperationKind.Invalid;
    }
}
