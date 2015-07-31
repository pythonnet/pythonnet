// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using System.Collections;
using System.Reflection;
using System.Security.Permissions;

namespace Python.Runtime {

    //========================================================================
    // Bundles the information required to support an indexer property.
    //========================================================================

    internal class Indexer {

        public MethodBinder GetterBinder;
        public MethodBinder SetterBinder;

        public Indexer() {
            GetterBinder = new MethodBinder();
            SetterBinder = new MethodBinder();
        }


        public bool CanGet {
            get { 
                return GetterBinder.Count > 0; 
            }
        }

        public bool CanSet {
            get { 
                return SetterBinder.Count > 0; 
            }
        }


        public void AddProperty(PropertyInfo pi) {
            MethodInfo getter = pi.GetGetMethod(true);
            MethodInfo setter = pi.GetSetMethod(true);
            if (getter != null) {
                GetterBinder.AddMethod(getter);
            }
            if (setter != null) {
                SetterBinder.AddMethod(setter);
            }
        }

        internal IntPtr GetItem(IntPtr inst, IntPtr args) {
            return GetterBinder.Invoke(inst, args, IntPtr.Zero);
        }


        internal void SetItem(IntPtr inst, IntPtr args) {
            SetterBinder.Invoke(inst, args, IntPtr.Zero);
        }

        internal bool NeedsDefaultArgs(IntPtr inst, IntPtr args){
            int pynargs = Runtime.PyTuple_Size(args) - 1;
            var methods = SetterBinder.GetMethods();
            if(methods.Length == 0)
                return false;

            MethodBase mi = methods[0];
            ParameterInfo[] pi = mi.GetParameters();
            // need to subtract one for the value
            int clrnargs = pi.Length - 1;
            if(pynargs == clrnargs)
                return false;

            for (int v = pynargs; v < clrnargs; v++){
                if (pi[v].DefaultValue == DBNull.Value)
                    return false;
            }
            return true;
        }

        internal IntPtr GetDefaultArgs(IntPtr inst, IntPtr args){
            //IntPtr real = Runtime.PyTuple_New(i + 1);
            int pynargs = Runtime.PyTuple_Size(args) - 1;
            var methods = SetterBinder.GetMethods();
            IntPtr defaultArgs;
            if(methods.Length == 0){
                 defaultArgs = Runtime.PyTuple_New(0);
                return defaultArgs;
            }
            MethodBase mi = methods[0];
            ParameterInfo[] pi = mi.GetParameters();
            int clrnargs = pi.Length - 1;
            if(pynargs == clrnargs|| pynargs > clrnargs){
                 defaultArgs = Runtime.PyTuple_New(0);
                return defaultArgs;
            }

            defaultArgs = Runtime.PyTuple_New(clrnargs - pynargs);
            for (int i = 0; i < (clrnargs - pynargs); i++) {
                // Here we own the reference to the Python value, and we
                // give the ownership to the arg tuple.
                if (pi[i + pynargs].DefaultValue == DBNull.Value)
                    continue;
                else{
                    IntPtr arg = Converter.ToPython(pi[i + pynargs].DefaultValue, pi[i + pynargs].ParameterType);
                    Type type = typeof(String);
                    Object arg2;
                    Converter.ToManaged(arg, type, out arg2, false);
                    Runtime.PyTuple_SetItem(defaultArgs, i, arg);
                }
            }
            return defaultArgs;
        }


    }

}
