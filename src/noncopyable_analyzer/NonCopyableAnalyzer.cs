using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace NonCopyable
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NonCopyableAnalyzer : DiagnosticAnalyzer
    {
        private static DiagnosticDescriptor CreateRule(int num, string type)
            => new DiagnosticDescriptor("NoCopy" + num.ToString("00"), "non-copyable", "🚫 " + type + ". '{0}' is non-copyable.", "Correction", DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static DiagnosticDescriptor FieldDeclarationRule = CreateRule(1, "field declaration");
        private static DiagnosticDescriptor InitializerRule = CreateRule(2, "initializer");
        private static DiagnosticDescriptor AssignmentRule = CreateRule(3, "assignment");
        private static DiagnosticDescriptor ArgumentRule = CreateRule(4, "argument");
        private static DiagnosticDescriptor ReturnRule = CreateRule(5, "return");
        private static DiagnosticDescriptor ConversionRule = CreateRule(6, "conversion");
        private static DiagnosticDescriptor PatternRule = CreateRule(7, "pattern matching");
        private static DiagnosticDescriptor TupleRule = CreateRule(8, "tuple");
        private static DiagnosticDescriptor MemberRule = CreateRule(9, "member reference");
        private static DiagnosticDescriptor ReadOnlyInvokeRule = CreateRule(10, "readonly invoke");
        private static DiagnosticDescriptor GenericConstraintRule = CreateRule(11, "generic constraint");
        private static DiagnosticDescriptor DelegateRule = CreateRule(12, "delegate");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(FieldDeclarationRule, InitializerRule, AssignmentRule, ArgumentRule, ReturnRule, ConversionRule, PatternRule, TupleRule, MemberRule, ReadOnlyInvokeRule, GenericConstraintRule, DelegateRule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(csc =>
            {
                csc.RegisterOperationAction(oc =>
                {
                    var op = (ISymbolInitializerOperation)oc.Operation;
                    CheckCopyability(oc, op.Value, InitializerRule);
                }, OperationKind.FieldInitializer,
                OperationKind.ParameterInitializer,
                OperationKind.PropertyInitializer,
                OperationKind.VariableInitializer);

                csc.RegisterOperationAction(oc =>
                {
                    // including member initializer
                    // including collection element initializer
                    var op = (ISimpleAssignmentOperation)oc.Operation;
                    if (op.IsRef) return;
                    CheckCopyability(oc, op.Value, AssignmentRule);
                }, OperationKind.SimpleAssignment);

                csc.RegisterOperationAction(oc =>
                {
                    // including non-ref extension method invocation
                    var op = (IArgumentOperation)oc.Operation;
                    if (op.Parameter.RefKind != RefKind.None) return;
                    CheckCopyability(oc, op.Value, ArgumentRule);
                }, OperationKind.Argument);

                csc.RegisterOperationAction(oc =>
                {
                    var op = (IReturnOperation)oc.Operation;
                    if (op.ReturnedValue == null) return;
                    CheckCopyability(oc, op.ReturnedValue, ReturnRule);
                }, OperationKind.Return,
                OperationKind.YieldReturn);

                csc.RegisterOperationAction(oc =>
                {
                    var op = (IConversionOperation)oc.Operation;
                    var v = op.Operand;
                    if (v.Kind == OperationKind.DefaultValue) return;
                    var t = v.Type;
                    if (!t.IsNonCopyable()) return;

                    if (op.OperatorMethod != null && op.OperatorMethod.Parameters.Length == 1)
                    {
                        var parameter = op.OperatorMethod.Parameters[0];
                        if (parameter.RefKind != RefKind.None) return;
                    }

                    if (op.Parent is IForEachLoopOperation &&
                        op == ((IForEachLoopOperation)op.Parent).Collection &&
                        op.Conversion.IsIdentity)
                    {
                        return; 
                    }

                    oc.ReportDiagnostic(Error(v.Syntax, ConversionRule, t.Name));
                }, OperationKind.Conversion);

                csc.RegisterOperationAction(oc =>
                {
                    var op = (IArrayInitializerOperation)oc.Operation;

                    if (!((IArrayTypeSymbol)((IArrayCreationOperation)op.Parent).Type).ElementType.IsNonCopyable()) return;

                    foreach (var v in op.ElementValues)
                    {
                        CheckCopyability(oc, v, InitializerRule);
                    }
                }, OperationKind.ArrayInitializer);

                csc.RegisterOperationAction(oc =>
                {
                    var op = (IDeclarationPatternOperation)oc.Operation;
                    var t = ((ILocalSymbol)op.DeclaredSymbol).Type;
                    if (!t.IsNonCopyable()) return;
                    oc.ReportDiagnostic(Error(op.Syntax, PatternRule, t.Name));
                }, OperationKind.DeclarationPattern);

                csc.RegisterOperationAction(oc =>
                {
                    var op = (ITupleOperation)oc.Operation;

                    // exclude ParenthesizedVariableDesignationSyntax
                    if (op.Syntax.Kind() != SyntaxKind.TupleExpression) return;

                    foreach (var v in op.Elements)
                    {
                        CheckCopyability(oc, v, TupleRule);
                    }
                }, OperationKind.Tuple);

                csc.RegisterOperationAction(oc =>
                {
                    // instance property/event should not be referenced with in parameter/ref readonly local/readonly field
                    var op = (IMemberReferenceOperation)oc.Operation;
                    CheckInstanceReadonly(oc, op.Instance, MemberRule);
                }, OperationKind.PropertyReference,
                OperationKind.EventReference);

                csc.RegisterOperationAction(oc =>
                {
                    // instance method should not be invoked with in parameter/ref readonly local/readonly field
                    var op = (IInvocationOperation)oc.Operation;

                    CheckGenericConstraints(oc, op, GenericConstraintRule);
                    CheckInstanceReadonly(oc, op.Instance, ReadOnlyInvokeRule);

                }, OperationKind.Invocation);

                csc.RegisterOperationAction(oc => {
                    var op = (IDynamicInvocationOperation)oc.Operation;

                    foreach(var arg in op.Arguments) {
                        if (!arg.Type.IsNonCopyable()) continue;

                        oc.ReportDiagnostic(Error(arg.Syntax, GenericConstraintRule));
                    }

                }, OperationKind.DynamicInvocation);

                csc.RegisterOperationAction(oc =>
                {
                    // delagate creation
                    var op = (IMemberReferenceOperation)oc.Operation;
                    if (op.Instance == null) return;
                    if (!op.Instance.Type.IsNonCopyable()) return;
                    oc.ReportDiagnostic(Error(op.Instance.Syntax, DelegateRule, op.Instance.Type.Name));
                }, OperationKind.MethodReference);

                csc.RegisterSymbolAction(sac =>
                {
                    var f = (IFieldSymbol)sac.Symbol;
                    if (f.IsStatic) return;
                    if (!f.Type.IsNonCopyable()) return;
                    if (f.ContainingType.IsReferenceType) return;
                    if (f.ContainingType.IsNonCopyable()) return;
                    sac.ReportDiagnostic(Error(f.DeclaringSyntaxReferences[0].GetSyntax(), FieldDeclarationRule, f.Type.Name));
                }, SymbolKind.Field);
            });

            // not supported yet:
            //    OperationKind.CompoundAssignment,
            //    OperationKind.UnaryOperator,
            //    OperationKind.BinaryOperator,
        }

        private static void CheckGenericConstraints(in OperationAnalysisContext oc, IInvocationOperation op, DiagnosticDescriptor rule)
        {
            var m = op.TargetMethod;

            if (m.IsGenericMethod)
            {
                var parameters = m.TypeParameters;
                var arguments = m.TypeArguments;
                for (int i = 0; i < parameters.Length; i++)
                {
                    var p = parameters[i];
                    var a = arguments[i];

                    if (a.IsNonCopyable() && !p.IsNonCopyable())
                        oc.ReportDiagnostic(Error(op.Syntax, rule, a.Name));
                }
            }
        }

        private static void CheckInstanceReadonly(in OperationAnalysisContext oc, IOperation instance, DiagnosticDescriptor rule)
        {
            if (instance == null) return;

            var t = instance.Type;
            if (!t.IsNonCopyable()) return;

            if (IsInstanceReadonly(instance))
            {
                oc.ReportDiagnostic(Error(instance.Syntax, rule, t.Name));
            }
        }

        private static Diagnostic Error(SyntaxNode at, DiagnosticDescriptor rule, string name = null)
            => name is null
                ? Diagnostic.Create(rule, at.GetLocation())
                : Diagnostic.Create(rule, at.GetLocation(), name);

        private static bool IsInstanceReadonly(IOperation instance)
        {
            bool isReadOnly = false;
            switch (instance)
            {
                case IFieldReferenceOperation r:
                    isReadOnly = r.Field.IsReadOnly;
                    break;
                case ILocalReferenceOperation r:
                    isReadOnly = r.Local.RefKind == RefKind.In;
                    break;
                case IParameterReferenceOperation r:
                    isReadOnly = r.Parameter.RefKind == RefKind.In;
                    break;
            }

            return isReadOnly;
        }

        private static bool HasNonCopyableParameter(IMethodSymbol m)
        {
            foreach (var p in m.Parameters)
            {
                if(p.RefKind == RefKind.None)
                {
                    if (p.Type.IsNonCopyable()) return true;
                }
            }
            return false;
        }

        private static void CheckCopyability(in OperationAnalysisContext oc, IOperation v, DiagnosticDescriptor rule)
        {
            var t = v.Type;
            if (!t.IsNonCopyable()) return;
            if (v.CanCopy()) return;
            oc.ReportDiagnostic(Error(v.Syntax, rule, t.Name));
        }
    }
}
