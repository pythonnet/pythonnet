// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using System.Reflection;

namespace Python.Runtime {

    //========================================================================
    // A ConstructorBinder encapsulates information about one or more managed
    // constructors, and is responsible for selecting the right constructor
    // given a set of Python arguments. This is slightly different than the
    // standard MethodBinder because of a difference in invoking constructors
    // using reflection (which is seems to be a CLR bug).
    //========================================================================

    internal class ConstructorBinder : MethodBinder {

        internal ConstructorBinder () : base() {}

        //====================================================================
        // Constructors get invoked when an instance of a wrapped managed
        // class or a subclass of a managed class is created. This differs
        // from the MethodBinder implementation in that we return the raw
        // result of the constructor rather than wrapping it as a Python 
        // object - the reason is that only the caller knows the correct
        // Python type to use when wrapping the result (may be a subclass).
        //====================================================================

         internal object InvokeRaw(IntPtr inst, IntPtr args, IntPtr kw) {
            return this.InvokeRaw(inst, args, kw, null);
        }

        internal object InvokeRaw(IntPtr inst, IntPtr args, IntPtr kw,
                                  MethodBase info) {
            Binding binding = this.Bind(inst, args, kw);
            Object result;

            if (binding == null) {
                Exceptions.SetError(Exceptions.TypeError, 
                                    "no constructor matches given arguments"
                                    );
                return null;
            }

            // Object construction is presumed to be non-blocking and fast
            // enough that we shouldn't really need to release the GIL.

            ConstructorInfo ci = (ConstructorInfo)binding.info;
            try {
                result = ci.Invoke(binding.args);
            }
            catch (Exception e) {
                if (e.InnerException != null) {
                    e = e.InnerException;
                }
                Exceptions.SetError(e);
                return null;
            }

            return result;
        }


    }


}
