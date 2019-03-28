namespace Python.Runtime {
    using System;

    /// <summary>
    /// Specifies how to convert Python objects, passed to .NET functions to the expected CLR types.
    /// </summary>
    public interface IPyArgumentConverter
    {
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
        bool TryConvertArgument(IntPtr pyarg, Type parameterType,
            bool needsResolution, out object arg, out bool isOut);
    }

    public class DefaultPyArgumentConverter: IPyArgumentConverter {
        public static DefaultPyArgumentConverter Instance { get; }= new DefaultPyArgumentConverter();

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
            return MethodBinder.TryConvertArgument(
                pyarg, parameterType, needsResolution,
                out arg, out isOut);
        }
    }
}
