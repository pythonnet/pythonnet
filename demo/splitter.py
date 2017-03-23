#!/usr/bin/env python
# -*- coding: utf-8 -*-

import clr

import System
import System.Windows.Forms as WinForms

from System.Drawing import Color, Size, Point


class Splitter(WinForms.Form):
    """A WinForms example transcribed to Python from the MSDN article:
       'Creating a Multipane User Interface with Windows Forms'."""

    def __init__(self):

        # Create an instance of each control being used.
        self.components = System.ComponentModel.Container()
        self.treeView1 = WinForms.TreeView()
        self.listView1 = WinForms.ListView()
        self.richTextBox1 = WinForms.RichTextBox()
        self.splitter1 = WinForms.Splitter()
        self.splitter2 = WinForms.Splitter()
        self.panel1 = WinForms.Panel()

        # Set properties of TreeView control.
        self.treeView1.Dock = WinForms.DockStyle.Left
        self.treeView1.Width = self.ClientSize.Width / 3
        self.treeView1.TabIndex = 0
        self.treeView1.Nodes.Add("TreeView")

        # Set properties of ListView control.
        self.listView1.Dock = WinForms.DockStyle.Top
        self.listView1.Height = self.ClientSize.Height * 2 / 3
        self.listView1.TabIndex = 0
        self.listView1.Items.Add("ListView")

        # Set properties of RichTextBox control.
        self.richTextBox1.Dock = WinForms.DockStyle.Fill
        self.richTextBox1.TabIndex = 2
        self.richTextBox1.Text = "richTextBox1"

        # Set properties of Panel's Splitter control.
        self.splitter2.Dock = WinForms.DockStyle.Top

        # Width is irrelevant if splitter is docked to Top.
        self.splitter2.Height = 3

        # Use a different color to distinguish the two splitters.
        self.splitter2.BackColor = Color.Blue
        self.splitter2.TabIndex = 1

        # Set TabStop to false for ease of use when negotiating UI.
        self.splitter2.TabStop = 0

        # Set properties of Form's Splitter control.
        self.splitter1.Location = System.Drawing.Point(121, 0)
        self.splitter1.Size = System.Drawing.Size(3, 273)
        self.splitter1.BackColor = Color.Red
        self.splitter1.TabIndex = 1

        # Set TabStop to false for ease of use when negotiating UI.
        self.splitter1.TabStop = 0

        # Add the appropriate controls to the Panel.
        for item in (self.richTextBox1, self.splitter2, self.listView1):
            self.panel1.Controls.Add(item)

        # Set properties of Panel control.
        self.panel1.Dock = WinForms.DockStyle.Fill
        self.panel1.TabIndex = 2

        # Add the rest of the controls to the form.
        for item in (self.panel1, self.splitter1, self.treeView1):
            self.Controls.Add(item)

        self.Text = "Intricate UI Example"

    def Dispose(self):
        self.components.Dispose()
        WinForms.Form.Dispose(self)


def main():
    app = Splitter()
    WinForms.Application.Run(app)
    app.Dispose()


if __name__ == '__main__':
    main()
