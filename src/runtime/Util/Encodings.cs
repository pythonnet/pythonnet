using System;
using System.Text;

namespace Python.Runtime;

static class Encodings {
    public static System.Text.Encoding UTF8 = new UTF8Encoding(false, true);
    public static System.Text.Encoding UTF16 = new UnicodeEncoding(!BitConverter.IsLittleEndian, false, true);
    public static System.Text.Encoding UTF32 = new UTF32Encoding(!BitConverter.IsLittleEndian, false, true);
}
