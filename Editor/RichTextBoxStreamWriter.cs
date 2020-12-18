
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Editor
{
    public class RichTextBoxStreamWriter : TextWriter
    {
        RichTextBox _richTextBox = null;

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            base.Write(value);
            MethodInvoker action = delegate
            {
                _richTextBox.AppendText(value.ToString());
            };
            _richTextBox.BeginInvoke(action);
        }        

        public RichTextBoxStreamWriter(RichTextBox richTextBox)
        {
            _richTextBox = richTextBox;
        }        
    }
}
