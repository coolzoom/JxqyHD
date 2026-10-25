using System;
using Eto.Drawing;
using Eto.Forms;
using JxqyTools;

namespace GameStarter
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            new Application(Eto.Platform.Detect).Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        public MainForm()
        {
            Title = "剑侠情缘";
            ClientSize = new Size(260, 170);
            Resizable = false;

            var start = new Button { Text = "开始游戏" };
            start.Click += (s, e) => { Launch("Jxqy"); Application.Instance.Quit(); };

            var setting = new Button { Text = "游戏设置" };
            setting.Click += (s, e) => { Launch("Settings"); Application.Instance.Quit(); };

            var debug = new Button { Text = "游戏调试" };
            debug.Click += (s, e) => { Launch("GameEditor"); Application.Instance.Quit(); };

            Content = new TableLayout
            {
                Padding = new Padding(20),
                Spacing = new Size(10, 10),
                Rows =
                {
                    new TableRow(new TableCell(start, true)),
                    new TableRow(new TableCell(setting, true)),
                    new TableRow(new TableCell(debug, true))
                }
            };
        }

        private void Launch(string project)
        {
            var dll = ToolPaths.FindToolDll(project);
            if (dll == null)
            {
                MessageBox.Show(this, "找不到 " + project + ".dll，请先构建该项目。");
                return;
            }
            ToolPaths.Launch(dll);
        }
    }
}
