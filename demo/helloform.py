#!/usr/bin/env python
# -*- coding: utf-8 -*-

import clr

SWF = clr.AddReference("System.Windows.Forms")
print (SWF.Location)
import System.Windows.Forms as WinForms
from System.Drawing import Size, Point


class HelloApp(WinForms.Form):
    """A simple hello world app that demonstrates the essentials of
       winforms programming and event-based programming in Python."""

    def __init__(self):
        self.Text = "Hello World From Python"
        self.AutoScaleBaseSize = Size(5, 13)
        self.ClientSize = Size(392, 117)
        h = WinForms.SystemInformation.CaptionHeight
        self.MinimumSize = Size(392, (117 + h))

        # Create the button
        self.button = WinForms.Button()
        self.button.Location = Point(160, 64)
        self.button.Size = Size(820, 20)
        self.button.TabIndex = 2
        self.button.Text = "Click Me!"

        # Register the event handler
        self.button.Click += self.button_Click

        # Create the text box
        self.textbox = WinForms.TextBox()
        self.textbox.Text = "Hello World"
        self.textbox.TabIndex = 1
        self.textbox.Size = Size(1260, 40)
        self.textbox.Location = Point(160, 24)

        # Add the controls to the form
        self.AcceptButton = self.button
        self.Controls.Add(self.button)
        self.Controls.Add(self.textbox)

    def button_Click(self, sender, args):
        """Button click event handler"""
        print ("Click")
        WinForms.MessageBox.Show("Please do not press this button again.")

    def run(self):
        WinForms.Application.Run(self)


def main():
    form = HelloApp()
    print ("form created")
    app = WinForms.Application
    print ("app referenced")
    app.Run(form)


if __name__ == '__main__':
    main()
