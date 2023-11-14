.. _codecs:

Codecs
======

Python.NET performs some conversions between .NET and Python automatically.
For example, when Python calls this C# method:

.. code:: csharp

   void Foo(int bar) { ... }

via ``Foo(42)``, Python value ``42`` of type ``int`` will be
automatically converted to .NET type ``System.Int32``. Another way to
invoke those conversions is to call ``dotNetObject.ToPython()``
(available as an extension method) or ``pyObject.As<T>()`` to convert
``PyObject`` to .NET.

An incomplete list of Python types, that are converted between Python
and .NET automatically: most numeric types, ``bool``, ``string``,
``Nullable<T>`` to its ``Value`` or ``None`` and back, etc.

A custom conversion (**Codec**) can be defined by implementing one of the (or
both) interfaces:

- ``Python.Runtime.IPyObjectDecoder`` to marshal Python objects to .NET

.. code:: csharp

   interface IPyObjectDecoder {
     bool CanDecode(PyObject objectType, System.Type targetType);
     bool TryDecode<T>(PyObject pyObj, out T value);
   }

-  ``Python.Runtime.IPyObjectEncoder`` to marshal .NET objects to Python

.. code:: csharp

   interface IPyObjectEncoder {
     bool CanEncode(System.Type);
     PyObject TryEncode(System.Object);
   }

Once implemented, instances have to be registered with
``Python.Runtime.PyObjectConversions.RegisterEncoder``/``-Decoder``. One
can override *some* of the default conversions by registering new
codecs.

Codec priorities
~~~~~~~~~~~~~~~~

When multiple codecs are registered, the runtime will first try the ones, that
were registered earlier. If you need to have some grouping of codecs by
priority, create and expose
``Python.Runtime.Codecs.EncoderGroup``/``-.DecoderGroup``. For example:

.. code:: csharp

   public static EncoderGroup HighPriorityEncoders{ get; } = new EncoderGroup();

   void Init() {
     PyObjectConversions.RegisterEncoder(HighPriorityEncoders);
     var lowPriorityEncoder = new SomeEncoder();
     PyObjectConversions.RegisterEncoder(lowPriorityEncoder);
   }

   ... some time later

   HighPriorityEncoders.Add(new SomeOtherEncoder());
