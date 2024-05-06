import clr
import sys

if sys.platform.lower() not in ["cli", "win32"]:
    print("only windows is supported for wpf")
clr.AddReference(r"wpf\PresentationFramework")

from System.IO import StreamReader  # noqa: E402
from System.Windows.Markup import XamlReader  # noqa: E402
from System.Threading import Thread, ThreadStart, ApartmentState  # noqa: E402
from System.Windows import Application, Window  # noqa: E402


class MyWindow(Window):
    def __init__(self):
        stream = StreamReader("DynamicGrid.xaml")
        window = XamlReader.Load(stream.BaseStream)
        Application().Run(window)


if __name__ == "__main__":
    thread = Thread(ThreadStart(MyWindow))
    thread.SetApartmentState(ApartmentState.STA)
    thread.Start()
    thread.Join()
