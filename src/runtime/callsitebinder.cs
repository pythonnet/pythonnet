using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

using Microsoft.CSharp.RuntimeBinder;

using CSharpBinder = Microsoft.CSharp.RuntimeBinder.Binder;

namespace Python.Runtime
{
    public class PythonNetCallSiteBinder : CallSiteBinder
    {
        readonly CallSiteBinder voidBinder;
        readonly CallSiteBinder resultBinder;

        PythonNetCallSiteBinder(string methodName,
            IEnumerable<Type> typeArguments,
            Type context,
            IEnumerable<CSharpArgumentInfo> argumentInfo)
        {
            this.voidBinder = CSharpBinder.InvokeMember(CSharpBinderFlags.ResultDiscarded,
                methodName,
                typeArguments: typeArguments,
                context: context,
                argumentInfo
            );
            this.resultBinder = CSharpBinder.InvokeMember(CSharpBinderFlags.None,
                methodName,
                typeArguments: typeArguments,
                context: context,
                argumentInfo
            );
        }

        public override Expression Bind(object[] args,
            ReadOnlyCollection<ParameterExpression> parameters,
            LabelTarget returnLabel)
        {
            var result = this.resultBinder.Bind(args, parameters, returnLabel);
            if (result.Type == typeof(void))
            {
                var voidExpr = this.voidBinder.Bind(args, parameters, returnLabel);
                return Expression.Block(
                    voidExpr,
                    Expression.Return(returnLabel, Expression.Constant(null))
                );
            }

            return result;
        }

        public static PythonNetCallSiteBinder InvokeMember(string methodName,
            IEnumerable<Type> typeArguments,
            Type context,
            IEnumerable<CSharpArgumentInfo> argumentInfo)
            => new PythonNetCallSiteBinder(methodName, typeArguments, context, argumentInfo);
    }
}
