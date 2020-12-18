
using System;
using System.IO;

namespace Editor
{
    public class Output
    {
        private TextWriter _textWriter = null;

        public void write(string str)
        {
            if (_textWriter != null)
            {
                _textWriter.Write(str);
            }
            else
            {
                Console.Write(str);
            }
        }

        public void writelines(string[] str)
        {
            foreach (string line in str)
            {
                if (_textWriter != null)
                {
                    _textWriter.Write(line);
                }
                else
                {
                    Console.Write(line);
                }
            }
        }

        public void flush()
        {
            if (_textWriter != null)
            {
                _textWriter.Flush();
            }
        }

        public void close()
        {
            if (_textWriter != null)
            {
                _textWriter.Close();
            }
        }

        public Output(TextWriter textWriter)
        {
            _textWriter = textWriter;
        }
    }
}
