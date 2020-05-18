#!/usr/bin/env python
# -*- coding: utf-8 -*-

import clr
import System
import System.Windows.Forms as WinForms

from System.IO import File
from System.Text import Encoding
from System.Drawing import Color, Point, Size
from System.Threading import ApartmentState, Thread, ThreadStart


class Wordpad(WinForms.Form):
    """A simple example winforms application similar to wordpad."""

    def __init__(self):
        self.filename = ''
        self.word_wrap = 1
        self.doctype = 1
        self.InitializeComponent()
        self.NewDocument()

    def InitializeComponent(self):
        """Initialize form components."""
        self.components = System.ComponentModel.Container()

        self.openFileDialog = WinForms.OpenFileDialog()
        self.saveFileDialog = WinForms.SaveFileDialog()

        self.mainMenu = WinForms.MainMenu()

        self.fileMenu = WinForms.MenuItem()
        self.menuFileNew = WinForms.MenuItem()
        self.menuFileOpen = WinForms.MenuItem()
        self.menuFileSave = WinForms.MenuItem()
        self.menuFileSaveAs = WinForms.MenuItem()
        self.menuFileSep_1 = WinForms.MenuItem()
        self.menuFileExit = WinForms.MenuItem()

        self.editMenu = WinForms.MenuItem()
        self.menuEditUndo = WinForms.MenuItem()
        self.menuEditRedo = WinForms.MenuItem()
        self.menuEditSep_1 = WinForms.MenuItem()
        self.menuEditCut = WinForms.MenuItem()
        self.menuEditCopy = WinForms.MenuItem()
        self.menuEditPaste = WinForms.MenuItem()
        self.menuEditSep_2 = WinForms.MenuItem()
        self.menuEditSelectAll = WinForms.MenuItem()

        self.formatMenu = WinForms.MenuItem()
        self.menuFormatFont = WinForms.MenuItem()
        self.menuFormatWordWrap = WinForms.MenuItem()

        self.aboutMenu = WinForms.MenuItem()
        self.menuHelpAbout = WinForms.MenuItem()

        self.richTextBox = WinForms.RichTextBox()
        self.statusBarPanel1 = WinForms.StatusBarPanel()
        self.statusBar = WinForms.StatusBar()
        self.fontDialog = WinForms.FontDialog()
        self.statusBarPanel1.BeginInit()

        # ===================================================================
        # File Menu
        # ===================================================================

        self.menuFileNew.Text = "&New"
        self.menuFileNew.Shortcut = WinForms.Shortcut.CtrlN
        self.menuFileNew.ShowShortcut = False
        self.menuFileNew.Index = 0
        self.menuFileNew.Click += self.OnClickFileNew

        self.menuFileOpen.Text = "&Open"
        self.menuFileOpen.Shortcut = WinForms.Shortcut.CtrlO
        self.menuFileOpen.ShowShortcut = False
        self.menuFileOpen.Index = 1
        self.menuFileOpen.Click += self.OnClickFileOpen

        self.menuFileSave.Text = "&Save"
        self.menuFileSave.Shortcut = WinForms.Shortcut.CtrlS
        self.menuFileSave.ShowShortcut = False
        self.menuFileSave.Index = 2
        self.menuFileSave.Click += self.OnClickFileSave

        self.menuFileSaveAs.Text = "Save &As"
        self.menuFileSaveAs.Index = 3
        self.menuFileSaveAs.Click += self.OnClickFileSaveAs

        self.menuFileSep_1.Text = "-"
        self.menuFileSep_1.Index = 4

        self.menuFileExit.Text = "E&xit"
        self.menuFileExit.Shortcut = WinForms.Shortcut.AltF4
        self.menuFileExit.ShowShortcut = False
        self.menuFileExit.Index = 5
        self.menuFileExit.Click += self.OnClickFileExit

        self.fileMenu.Text = "&File"
        self.fileMenu.Index = 0

        items = (self.menuFileNew, self.menuFileOpen,
                 self.menuFileSave, self.menuFileSaveAs,
                 self.menuFileSep_1, self.menuFileExit)

        self.fileMenu.MenuItems.AddRange(items)

        # ===================================================================
        # Edit menu
        # ===================================================================

        self.menuEditUndo.Text = "&Undo"
        self.menuEditUndo.Shortcut = WinForms.Shortcut.CtrlZ
        self.menuEditUndo.Index = 0
        self.menuEditUndo.Click += self.OnClickEditUndo

        self.menuEditRedo.Text = "&Redo"
        self.menuEditRedo.Shortcut = WinForms.Shortcut.CtrlY
        self.menuEditRedo.Index = 1
        self.menuEditRedo.Click += self.OnClickEditRedo

        self.menuEditSep_1.Text = "-"
        self.menuEditSep_1.Index = 2

        self.menuEditCut.Text = "Cut"
        self.menuEditCut.Shortcut = WinForms.Shortcut.CtrlX
        self.menuEditCut.Index = 3
        self.menuEditCut.Click += self.OnClickEditCut

        self.menuEditCopy.Text = "Copy"
        self.menuEditCopy.Shortcut = WinForms.Shortcut.CtrlC
        self.menuEditCopy.Index = 4
        self.menuEditCopy.Click += self.OnClickEditCopy

        self.menuEditPaste.Text = "Paste"
        self.menuEditPaste.Shortcut = WinForms.Shortcut.CtrlV
        self.menuEditPaste.Index = 5
        self.menuEditPaste.Click += self.OnClickEditPaste

        self.menuEditSelectAll.Text = "Select All"
        self.menuEditSelectAll.Shortcut = WinForms.Shortcut.CtrlA
        self.menuEditSelectAll.Index = 7
        self.menuEditSelectAll.Click += self.OnClickEditSelectAll

        self.menuEditSep_2.Text = "-"
        self.menuEditSep_2.Index = 6

        self.editMenu.Text = "&Edit"
        self.editMenu.Index = 1

        items = (self.menuEditUndo, self.menuEditRedo,
                 self.menuEditSep_1, self.menuEditCut,
                 self.menuEditCopy, self.menuEditPaste,
                 self.menuEditSep_2, self.menuEditSelectAll)

        self.editMenu.MenuItems.AddRange(items)

        # ===================================================================
        # Format Menu
        # ===================================================================

        self.menuFormatWordWrap.Text = "Word Wrap"
        self.menuFormatWordWrap.Checked = self.word_wrap
        self.menuFormatWordWrap.Index = 1
        self.menuFormatWordWrap.Click += self.OnClickFormatWordWrap

        self.menuFormatFont.Text = "Fo&nt"
        self.menuFormatFont.Index = 0
        self.menuFormatFont.Click += self.OnClickFormatFont

        self.formatMenu.Text = "F&ormat"
        self.formatMenu.Index = 2

        items = (self.menuFormatWordWrap, self.menuFormatFont)

        self.formatMenu.MenuItems.AddRange(items)

        # ===================================================================
        # About menu
        # ===================================================================

        self.menuHelpAbout.Text = "&About"
        self.menuHelpAbout.Index = 0
        self.menuHelpAbout.Click += self.OnClickHelpAbout

        self.aboutMenu.Text = "&Help"
        self.aboutMenu.Index = 3
        self.aboutMenu.MenuItems.Add(self.menuHelpAbout)

        self.statusBarPanel1.Dock = WinForms.DockStyle.Fill
        self.statusBarPanel1.Text = "Ready"
        self.statusBarPanel1.Width = 755

        self.richTextBox.Dock = WinForms.DockStyle.Fill
        self.richTextBox.Size = System.Drawing.Size(795, 485)
        self.richTextBox.TabIndex = 0
        self.richTextBox.AutoSize = 1
        self.richTextBox.ScrollBars = WinForms.RichTextBoxScrollBars.ForcedBoth
        self.richTextBox.Font = System.Drawing.Font("Tahoma", 10.0)
        self.richTextBox.AcceptsTab = 1
        self.richTextBox.Location = System.Drawing.Point(0, 0)

        self.statusBar.BackColor = System.Drawing.SystemColors.Control
        self.statusBar.Location = System.Drawing.Point(0, 518)
        self.statusBar.Size = System.Drawing.Size(775, 19)
        self.statusBar.TabIndex = 1
        self.statusBar.ShowPanels = True
        self.statusBar.Panels.Add(self.statusBarPanel1)

        items = (self.fileMenu, self.editMenu, self.formatMenu, self.aboutMenu)
        self.mainMenu.MenuItems.AddRange(items)

        self.openFileDialog.Filter = "Text documents|*.txt|RTF document|*.rtf"
        self.openFileDialog.Title = "Open document"

        self.saveFileDialog.Filter = "Text Documents|*.txt|" \
                                     "Rich Text Format|*.rtf"
        self.saveFileDialog.Title = "Save document"
        self.saveFileDialog.FileName = "Untitled"

        self.AutoScaleBaseSize = System.Drawing.Size(5, 13)
        self.ClientSize = System.Drawing.Size(775, 537)
        self.Menu = self.mainMenu
        self.Text = "Python Wordpad"

        self.Controls.Add(self.statusBar)
        self.Controls.Add(self.richTextBox)
        self.statusBarPanel1.EndInit()

    def Dispose(self):
        self.components.Dispose()
        WinForms.Form.Dispose(self)

    def OnClickFileNew(self, sender, args):
        self.SaveChangesDialog()
        self.NewDocument()

    def OnClickFileOpen(self, sender, args):
        self.SaveChangesDialog()
        self.OpenDocument()

    def OnClickFileSave(self, sender, args):
        self.SaveDocument()

    def OnClickFileSaveAs(self, sender, args):
        self.filename = ''
        self.SaveDocument()

    def OnClickFileExit(self, sender, args):
        self.SaveChangesDialog()
        self.Close()

    def OnClickEditUndo(self, sender, args):
        self.richTextBox.Undo()

    def OnClickEditRedo(self, sender, args):
        self.richTextBox.Redo()

    def OnClickEditCut(self, sender, args):
        self.richTextBox.Cut()

    def OnClickEditCopy(self, sender, args):
        self.richTextBox.Copy()

    def OnClickEditPaste(self, sender, args):
        self.richTextBox.Paste()

    def OnClickEditSelectAll(self, sender, args):
        self.richTextBox.SelectAll()

    def OnClickFormatWordWrap(self, sender, args):
        value = not self.word_wrap
        self.richTextBox.WordWrap = value
        self.menuFormatWordWrap.Checked = value
        self.word_wrap = value

    def OnClickFormatFont(self, sender, args):
        if self.fontDialog.ShowDialog() == WinForms.DialogResult.OK:
            self.richTextBox.SelectionFont = self.fontDialog.Font

    def OnClickHelpAbout(self, sender, args):
        AboutForm().ShowDialog(self)

    def NewDocument(self):
        self.doctype = 1
        self.richTextBox.Rtf = ''
        self.richTextBox.Text = ''
        self.Text = 'Python Wordpad - (New Document)'
        self.filename = ''

    def OpenDocument(self):
        if self.openFileDialog.ShowDialog() != WinForms.DialogResult.OK:
            return

        filename = self.openFileDialog.FileName

        stream = File.OpenRead(filename)

        buff = System.Array.CreateInstance(System.Byte, 1024)
        buff.Initialize()
        data = []
        read = 1

        while read > 0:
            read, _ = stream.Read(buff, 0, 1024)
            temp = Encoding.ASCII.GetString(buff, 0, read)
            data.append(temp)

        data = ''.join(data)
        stream.Close()

        filename = self.filename = filename.lower()

        if filename.endswith('.rtf'):
            self.richTextBox.Rtf = data
            self.doctype = 2
        else:
            self.richTextBox.Text = data
            self.doctype = 1

        self.Text = 'Python Wordpad - %s' % filename
        self.richTextBox.Select(0, 0)

    def SaveDocument(self):
        filename = self.filename

        if not filename:
            if self.saveFileDialog.ShowDialog() != WinForms.DialogResult.OK:
                return
            filename = self.saveFileDialog.FileName

        filename = self.filename = filename.lower()
        self.Text = 'Python Wordpad - %s' % filename

        self.richTextBox.Select(0, 0)

        stream = File.OpenWrite(filename)

        if filename.endswith('.rtf'):
            data = self.richTextBox.Rtf
        else:
            data = self.richTextBox.Text

        data = System.Text.Encoding.ASCII.GetBytes(System.String(data))

        stream.Write(data, 0, data.Length)
        stream.Close()

    def SaveChangesDialog(self):
        if self.richTextBox.Modified:
            if WinForms.MessageBox.Show(
                    "Save changes?", "Word Pad",
                            WinForms.MessageBoxButtons.OK |
                            WinForms.MessageBoxButtons.YesNo
            ) == WinForms.DialogResult.Yes:
                self.SaveDocument()
                return 1
        return 0


class AboutForm(WinForms.Form):
    def __init__(self):
        self.InitializeComponent()

    def InitializeComponent(self):
        """Initialize form components."""
        self.Text = "Python Wordpad"
        self.components = System.ComponentModel.Container()

        self.btnClose = WinForms.Button()
        self.label1 = WinForms.Label()
        self.SuspendLayout()

        self.btnClose.Location = System.Drawing.Point(360, 181)
        self.btnClose.Name = "bnClose"
        self.btnClose.TabIndex = 1
        self.btnClose.Text = "&Close"
        self.btnClose.Click += self.OnClickClose

        self.label1.Location = System.Drawing.Point(20, 20)
        self.label1.Name = "label1"
        self.label1.Size = System.Drawing.Size(296, 140)
        self.label1.TabIndex = 2
        self.label1.Text = "Python Wordpad - an example winforms " \
                           "application using Python.NET"

        self.AutoScaleBaseSize = System.Drawing.Size(5, 13)
        self.ClientSize = System.Drawing.Size(300, 150)

        self.Controls.AddRange((self.label1, self.btnClose))

        self.FormBorderStyle = WinForms.FormBorderStyle.FixedDialog
        self.MaximizeBox = 0
        self.MinimizeBox = 0
        self.Name = "AboutForm"
        self.ShowInTaskbar = False
        self.StartPosition = WinForms.FormStartPosition.CenterScreen
        self.Text = "About"
        self.ResumeLayout(False)

    def OnClickClose(self, sender, args):
        self.Close()


def app_thread():
    app = Wordpad()
    WinForms.Application.Run(app)
    app.Dispose()


def main():
    thread = Thread(ThreadStart(app_thread))
    thread.SetApartmentState(ApartmentState.STA)
    thread.Start()
    thread.Join()


if __name__ == '__main__':
    main()
