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

    /// <summary>
    /// The implementation of <see cref="IPyArgumentConverter"/> used by default
    /// </summary>
    public class DefaultPyArgumentConverter: IPyArgumentConverter {
        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static DefaultPyArgumentConverter Instance { get; } = new DefaultPyArgumentConverter();

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

    /// <summary>
    /// Specifies an argument converter to be used, when methods in this class/assembly are called from Python.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct)]
    public class PyArgConverterAttribute : Attribute
    {
        static readonly Type[] EmptyArgTypeList = new Type[0];
        static readonly object[] EmptyArgList = new object[0];

        /// <summary>
        /// Gets the instance of the converter, that will be used when calling methods
        /// of this class/assembly from Python
        /// </summary>
        public IPyArgumentConverter Converter { get; }
        /// <summary>
        /// Gets the type of the converter, that will be used when calling methods
        /// of this class/assembly from Python
        /// </summary>
        public Type ConverterType { get; }

        /// <summary>
        /// Specifies an argument converter to be used, when methods
        /// in this class/assembly are called from Python.
        /// </summary>
        /// <param name="converterType">Type of the converter to use.
        /// Must implement <see cref="IPyArgumentConverter"/>.</param>
        public PyArgConverterAttribute(Type converterType)
        {
            if (converterType == null) throw new ArgumentNullException(nameof(converterType));
            var ctor = converterType.GetConstructor(EmptyArgTypeList);
            if (ctor == null) throw new ArgumentException("Specified converter must have public parameterless constructor");
            this.Converter = (IPyArgumentConverter)ctor.Invoke(EmptyArgList);
            this.ConverterType = converterType;
        }
    }
}
