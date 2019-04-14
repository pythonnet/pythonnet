namespace Python.Runtime {
    using System;

    /// <summary>
    /// The implementation of <see cref="T:Python.Runtime.IPyArgumentConverter" /> used by default
    /// </summary>
    public class DefaultPyArgumentConverter: IPyArgumentConverter
    {
        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static DefaultPyArgumentConverter Instance { get; } = new DefaultPyArgumentConverter();

        /// <inheritdoc />
        /// <summary>
        /// Attempts to convert an argument passed by Python to the specified parameter type.
        /// </summary>
        /// <param name="pyarg">Unmanaged pointer to the Python argument value</param>
        /// <param name="parameterType">The expected type of the parameter</param>
        /// <param name="needsResolution"><c>true</c> if the method is overloaded</param>
        /// <param name="arg">This parameter will receive the converted value, matching the specified type</param>
        /// <param name="isOut">This parameter will be set to <c>true</c>,
        /// if the final type needs to be marshaled as an out argument.</param>
        /// <returns><c>true</c>, if the object matches requested type,
        /// and conversion was successful, otherwise <c>false</c></returns>
        public virtual bool TryConvertArgument(
            IntPtr pyarg, Type parameterType, bool needsResolution,
            out object arg, out bool isOut)
        {
            arg = null;
            isOut = false;
            Type clrType = TryComputeClrArgumentType(parameterType, pyarg, needsResolution: needsResolution);
            if (clrType == null)
            {
                return false;
            }

            if (!Converter.ToManaged(pyarg, clrType, out arg, false))
            {
                Exceptions.Clear();
                return false;
            }

            isOut = clrType.IsByRef;
            return true;
        }

        static Type TryComputeClrArgumentType(Type parameterType, IntPtr argument, bool needsResolution)
        {
            // this logic below handles cases when multiple overloading methods
            // are ambiguous, hence comparison between Python and CLR types
            // is necessary
            Type clrType = null;
            IntPtr pyArgType;
            if (needsResolution)
            {
                // HACK: each overload should be weighted in some way instead
                pyArgType = Runtime.PyObject_Type(argument);
                Exceptions.Clear();
                if (pyArgType != IntPtr.Zero)
                {
                    clrType = Converter.GetTypeByAlias(pyArgType);
                }
                Runtime.XDecref(pyArgType);
            }

            if (clrType != null)
            {
                if ((parameterType != typeof(object)) && (parameterType != clrType))
                {
                    IntPtr pyParamType = Converter.GetPythonTypeByAlias(parameterType);
                    pyArgType = Runtime.PyObject_Type(argument);
                    Exceptions.Clear();

                    bool typeMatch = false;
                    if (pyArgType != IntPtr.Zero && pyParamType == pyArgType)
                    {
                        typeMatch = true;
                        clrType = parameterType;
                    }
                    if (!typeMatch)
                    {
                        // this takes care of enum values
                        TypeCode argTypeCode = Type.GetTypeCode(parameterType);
                        TypeCode paramTypeCode = Type.GetTypeCode(clrType);
                        if (argTypeCode == paramTypeCode)
                        {
                            typeMatch = true;
                            clrType = parameterType;
                        }
                    }
                    Runtime.XDecref(pyArgType);
                    if (!typeMatch)
                    {
                        return null;
                    }
                }
                else
                {
                    clrType = parameterType;
                }
            }
            else
            {
                clrType = parameterType;
            }

            return clrType;
        }
    }
}
