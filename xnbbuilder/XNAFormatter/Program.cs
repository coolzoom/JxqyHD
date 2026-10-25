using System;
using Eto.Forms;

namespace XNAFormatter
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            new Application(Eto.Platform.Detect).Run(new FormatterForm());
        }
    }
}
