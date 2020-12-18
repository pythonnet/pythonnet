
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

using Python.Runtime;

namespace Editor.Forms
{
    public partial class MainForm : Form
    {
        private const string HelloWorldScript = @"print ('Hello, world!')";

        private const string ThreadSleepScript = @"import time

print('Printed immediately.')
time.sleep(10)
print('Printed after 10 seconds.')";

        private const string MessageBoxScript = @"import tkinter
from tkinter import messagebox

top = tkinter.Tk()
def hello():
    messagebox.showinfo('Say Hello', 'Hello World!')

button = tkinter.Button(top, text = 'Say Hello', command = hello)
button.pack()

top.mainloop()";

        private IntPtr _threadState;

        ulong _pythonThreadID;

        Output _output;

        private void MainFormLoad(object sender, System.EventArgs e)
        {
            _stopToolStripButton.Enabled = false;

            if (Environment.GetEnvironmentVariable("PYTHONPATH") is null)
            {
                MessageBox.Show(
                    "PYTHONPATH is missing from the Environment Variables.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Environment.Exit(1);
            }

            string pythonHome = Environment.GetEnvironmentVariable("PYTHONHOME");
            if (pythonHome is null)
            {
                MessageBox.Show(
                    "PYTHONHOME is missing from the Environment Variables.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Environment.Exit(1);
            }

            for (int i = 9; i > 5; i--)
            {
                string pythonDLLPath = Path.Combine(pythonHome, $"python3{i}.dll");
                if (File.Exists(pythonDLLPath))
                {
                    Runtime.PythonDLL = pythonDLLPath;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(Runtime.PythonDLL))
            {
                MessageBox.Show(
                    "Could not find PythonDLL.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Environment.Exit(1);
            }

            try
            {                
                PythonEngine.Initialize();
                _threadState = PythonEngine.BeginAllowThreads();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Environment.Exit(1);
            }

            RichTextBoxStreamWriter richTextBoxStreamWriter = new RichTextBoxStreamWriter(_outputRichTextBox);
            _output = new Output(richTextBoxStreamWriter);

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                string filePath = args[1];
                if (File.Exists(filePath))
                {
                    string scriptText = File.ReadAllText(filePath);
                    _scriptRichTextBox.Text = scriptText;
                }
            }
            else
            {
                _scriptRichTextBox.Text = HelloWorldScript;
            }
        }

        private void MainFormFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_runToolStripButton.Enabled && !_stopToolStripButton.Enabled)
            {
                StopToolStripButtonClick(null, new EventArgs());
            }

            try
            {
                PythonEngine.EndAllowThreads(_threadState);
                PythonEngine.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Environment.Exit(1);
            }
        }

        private void LoadScriptToolStripButtonClick(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Python files (*.py)|*.py";
            DialogResult dialogResult = openFileDialog.ShowDialog();
            if (dialogResult == DialogResult.OK)
            {
                _scriptRichTextBox.Text = File.ReadAllText(openFileDialog.FileName);
            }
        }        

        private void RunToolStripButtonClick(object sender, EventArgs e)
        {
            _loadScriptToolStripButton.Enabled = false;
            _runToolStripButton.Enabled = false;
            _stopToolStripButton.Enabled = true;
            _examplesToolStripDropDownButton.Enabled = false;
            _scriptRichTextBox.Enabled = false;
            string scriptText = _scriptRichTextBox.Text;
            _outputRichTextBox.Text = string.Empty;
            TaskScheduler uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Factory.StartNew(
                () =>
                {
                    using (Py.GIL())
                    {
                        _pythonThreadID = PythonEngine.GetPythonThreadID();
                        dynamic sys = Py.Import("sys");
                        sys.stdout = _output;
                        sys.stderr = _output;
                        PythonEngine.RunSimpleString(scriptText);
                    }
                }).ContinueWith(
                task =>
                {
                    _loadScriptToolStripButton.Enabled = true;
                    _runToolStripButton.Enabled = true;
                    _stopToolStripButton.Enabled = false;
                    _examplesToolStripDropDownButton.Enabled = true;
                    _scriptRichTextBox.Enabled = true;
                },
                uiScheduler);
        }

        private void StopToolStripButtonClick(object sender, EventArgs e)
        {
            _stopToolStripButton.Enabled = false;

            using (Py.GIL())
            {
                PythonEngine.Interrupt(_pythonThreadID);                
            }
        }

        private void HelloWorldToolStripMenuItemClick(object sender, EventArgs e)
        {
            _scriptRichTextBox.Text = HelloWorldScript;
        }

        private void InterruptToolStripMenuItemClick(object sender, EventArgs e)
        {
            _scriptRichTextBox.Text = ThreadSleepScript;
        }

        private void MessageBoxToolStripMenuItemClick(object sender, EventArgs e)
        {
            _scriptRichTextBox.Text = MessageBoxScript;
        }

        private void ScriptRichTextBoxTextChanged(object sender, EventArgs e)
        {
            _runToolStripButton.Enabled = !string.IsNullOrWhiteSpace(_scriptRichTextBox.Text);
        }

        public MainForm()
        {
            InitializeComponent();
        }
    }
}
