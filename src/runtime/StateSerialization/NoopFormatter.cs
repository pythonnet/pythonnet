using System;
using System.IO;
using System.Runtime.Serialization;

namespace Python.Runtime;

public class NoopFormatter : IFormatter {
    
    public object Deserialize(Stream s) => throw new NotImplementedException(
        "Cannot deserialize using 'NoopFormatter', implement 'Python.Runtime.RuntimeData.FormatterType' if needed"
    );

    public void Serialize(Stream s, object o) {}

    public SerializationBinder? Binder { get; set; }
    public StreamingContext Context { get; set; }
    public ISurrogateSelector? SurrogateSelector { get; set; }
}
