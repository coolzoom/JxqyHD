using System;
using Eto.Forms;

namespace GameEditor
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            new Application(Eto.Platform.Detect).Run(new MainForm());
        }
    }
}
