
namespace Editor.Forms
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this._mainToolStrip = new System.Windows.Forms.ToolStrip();
            this._loadScriptToolStripButton = new System.Windows.Forms.ToolStripButton();
            this._runToolStripButton = new System.Windows.Forms.ToolStripButton();
            this._stopToolStripButton = new System.Windows.Forms.ToolStripButton();
            this._examplesToolStripDropDownButton = new System.Windows.Forms.ToolStripDropDownButton();
            this._helloWorldToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this._interruptToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this._messageBoxToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this._mainSplitContainer = new System.Windows.Forms.SplitContainer();
            this._scriptRichTextBox = new System.Windows.Forms.RichTextBox();
            this._outputRichTextBox = new System.Windows.Forms.RichTextBox();
            this._mainToolStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._mainSplitContainer)).BeginInit();
            this._mainSplitContainer.Panel1.SuspendLayout();
            this._mainSplitContainer.Panel2.SuspendLayout();
            this._mainSplitContainer.SuspendLayout();
            this.SuspendLayout();
            // 
            // _mainToolStrip
            // 
            this._mainToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._loadScriptToolStripButton,
            this._runToolStripButton,
            this._stopToolStripButton,
            this._examplesToolStripDropDownButton});
            this._mainToolStrip.Location = new System.Drawing.Point(0, 0);
            this._mainToolStrip.Name = "_mainToolStrip";
            this._mainToolStrip.Size = new System.Drawing.Size(1008, 25);
            this._mainToolStrip.TabIndex = 3;
            this._mainToolStrip.Text = "toolStrip1";
            // 
            // _loadScriptToolStripButton
            // 
            this._loadScriptToolStripButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this._loadScriptToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._loadScriptToolStripButton.Name = "_loadScriptToolStripButton";
            this._loadScriptToolStripButton.Size = new System.Drawing.Size(79, 22);
            this._loadScriptToolStripButton.Text = "Load Script...";
            this._loadScriptToolStripButton.Click += new System.EventHandler(this.LoadScriptToolStripButtonClick);
            // 
            // _runToolStripButton
            // 
            this._runToolStripButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this._runToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._runToolStripButton.Name = "_runToolStripButton";
            this._runToolStripButton.Size = new System.Drawing.Size(32, 22);
            this._runToolStripButton.Text = "Run";
            this._runToolStripButton.Click += new System.EventHandler(this.RunToolStripButtonClick);
            // 
            // _stopToolStripButton
            // 
            this._stopToolStripButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this._stopToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._stopToolStripButton.Name = "_stopToolStripButton";
            this._stopToolStripButton.Size = new System.Drawing.Size(35, 22);
            this._stopToolStripButton.Text = "Stop";
            this._stopToolStripButton.Click += new System.EventHandler(this.StopToolStripButtonClick);
            // 
            // _examplesToolStripDropDownButton
            // 
            this._examplesToolStripDropDownButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this._examplesToolStripDropDownButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._helloWorldToolStripMenuItem,
            this._interruptToolStripMenuItem,
            this._messageBoxToolStripMenuItem});
            this._examplesToolStripDropDownButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._examplesToolStripDropDownButton.Name = "_examplesToolStripDropDownButton";
            this._examplesToolStripDropDownButton.Size = new System.Drawing.Size(70, 22);
            this._examplesToolStripDropDownButton.Text = "Examples";
            // 
            // _helloWorldToolStripMenuItem
            // 
            this._helloWorldToolStripMenuItem.Name = "_helloWorldToolStripMenuItem";
            this._helloWorldToolStripMenuItem.Size = new System.Drawing.Size(190, 22);
            this._helloWorldToolStripMenuItem.Text = "Hello World";
            this._helloWorldToolStripMenuItem.Click += new System.EventHandler(this.HelloWorldToolStripMenuItemClick);
            // 
            // _interruptToolStripMenuItem
            // 
            this._interruptToolStripMenuItem.Name = "_interruptToolStripMenuItem";
            this._interruptToolStripMenuItem.Size = new System.Drawing.Size(190, 22);
            this._interruptToolStripMenuItem.Text = "Interrupt Thread Sleep";
            this._interruptToolStripMenuItem.Click += new System.EventHandler(this.InterruptToolStripMenuItemClick);
            // 
            // _messageBoxToolStripMenuItem
            // 
            this._messageBoxToolStripMenuItem.Name = "_messageBoxToolStripMenuItem";
            this._messageBoxToolStripMenuItem.Size = new System.Drawing.Size(190, 22);
            this._messageBoxToolStripMenuItem.Text = "Message Box";
            this._messageBoxToolStripMenuItem.Click += new System.EventHandler(this.MessageBoxToolStripMenuItemClick);
            // 
            // _mainSplitContainer
            // 
            this._mainSplitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this._mainSplitContainer.Location = new System.Drawing.Point(0, 25);
            this._mainSplitContainer.Name = "_mainSplitContainer";
            this._mainSplitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // _mainSplitContainer.Panel1
            // 
            this._mainSplitContainer.Panel1.Controls.Add(this._scriptRichTextBox);
            // 
            // _mainSplitContainer.Panel2
            // 
            this._mainSplitContainer.Panel2.Controls.Add(this._outputRichTextBox);
            this._mainSplitContainer.Size = new System.Drawing.Size(1008, 704);
            this._mainSplitContainer.SplitterDistance = 336;
            this._mainSplitContainer.TabIndex = 4;
            // 
            // _scriptRichTextBox
            // 
            this._scriptRichTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this._scriptRichTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._scriptRichTextBox.Location = new System.Drawing.Point(0, 0);
            this._scriptRichTextBox.Name = "_scriptRichTextBox";
            this._scriptRichTextBox.Size = new System.Drawing.Size(1008, 336);
            this._scriptRichTextBox.TabIndex = 2;
            this._scriptRichTextBox.Text = "";
            this._scriptRichTextBox.TextChanged += new System.EventHandler(this.ScriptRichTextBoxTextChanged);
            // 
            // _outputRichTextBox
            // 
            this._outputRichTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this._outputRichTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._outputRichTextBox.Location = new System.Drawing.Point(0, 0);
            this._outputRichTextBox.Name = "_outputRichTextBox";
            this._outputRichTextBox.ReadOnly = true;
            this._outputRichTextBox.Size = new System.Drawing.Size(1008, 364);
            this._outputRichTextBox.TabIndex = 1;
            this._outputRichTextBox.Text = "";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1008, 729);
            this.Controls.Add(this._mainSplitContainer);
            this.Controls.Add(this._mainToolStrip);
            this.MinimumSize = new System.Drawing.Size(800, 600);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Editor";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainFormFormClosing);
            this.Load += new System.EventHandler(this.MainFormLoad);
            this._mainToolStrip.ResumeLayout(false);
            this._mainToolStrip.PerformLayout();
            this._mainSplitContainer.Panel1.ResumeLayout(false);
            this._mainSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._mainSplitContainer)).EndInit();
            this._mainSplitContainer.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStrip _mainToolStrip;
        private System.Windows.Forms.ToolStripButton _runToolStripButton;
        private System.Windows.Forms.ToolStripButton _stopToolStripButton;
        private System.Windows.Forms.ToolStripDropDownButton _examplesToolStripDropDownButton;
        private System.Windows.Forms.ToolStripMenuItem _helloWorldToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem _interruptToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem _messageBoxToolStripMenuItem;
        private System.Windows.Forms.SplitContainer _mainSplitContainer;
        private System.Windows.Forms.RichTextBox _scriptRichTextBox;
        private System.Windows.Forms.RichTextBox _outputRichTextBox;
        private System.Windows.Forms.ToolStripButton _loadScriptToolStripButton;
    }
}

