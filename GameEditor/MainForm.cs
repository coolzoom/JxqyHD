using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Eto.Drawing;
using Eto.Forms;
using JxqyTools;

namespace GameEditor
{
    /// <summary>
    /// Cross-platform game debug/editor window. Connects to a running Jxqy
    /// instance started with --edit (Engine edit-mode debug server), or can
    /// launch that game itself.
    /// </summary>
    public class MainForm : Form
    {
        private readonly GameConnection _conn = new GameConnection();
        private readonly Label _status;
        private readonly TextArea _log;
        private readonly TextArea _scriptFile;
        private readonly TextArea _variables;
        private readonly TextBox _posX, _posY, _radius, _npcName, _slot;
        private FileSystemWatcher _watcher;
        private string _watchedScript;
        private Process _gameProcess;

        public MainForm()
        {
            Title = "剑侠情缘 - 游戏编辑器";
            ClientSize = new Size(720, 560);

            _conn.OnLine += line => Application.Instance.AsyncInvoke(() => HandleLine(line));
            _conn.OnDisconnected += () => Application.Instance.AsyncInvoke(() => SetStatus("已断开"));

            _status = new Label { Text = "未连接" };

            var launch = new Button { Text = "启动游戏" };
            launch.Click += (s, e) => LaunchGame();
            var connect = new Button { Text = "连接" };
            connect.Click += (s, e) => TryConnect();

            _posX = new TextBox { Width = 60, PlaceholderText = "X" };
            _posY = new TextBox { Width = 60, PlaceholderText = "Y" };
            _radius = new TextBox { Width = 50, PlaceholderText = "半径" };
            _npcName = new TextBox { Width = 90, PlaceholderText = "NPC名(可空)" };
            _slot = new TextBox { Width = 40, Text = "1" };

            _log = new TextArea { ReadOnly = true };
            _scriptFile = new TextArea { ReadOnly = true };
            _variables = new TextArea { ReadOnly = true };

            var tabs = new TabControl();
            tabs.Pages.Add(new TabPage(_log) { Text = "日志" });
            tabs.Pages.Add(new TabPage(_scriptFile) { Text = "脚本" });
            tabs.Pages.Add(new TabPage(_variables) { Text = "变量" });

            Content = new TableLayout
            {
                Padding = new Padding(8),
                Spacing = new Size(6, 6),
                Rows =
                {
                    new TableRow(_status, launch, connect),
                    new TableRow(MakeCheatPanel()),
                    new TableRow(MakeScriptPanel()),
                    new TableRow(new TableCell(tabs, true)) { ScaleHeight = true }
                }
            };

            Shown += (s, e) => TryConnect();
        }

        private Control MakeCheatPanel()
        {
            Button B(string text, string cmd) => Btn(text, () => Send(cmd));

            var row1 = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 6 };
            row1.Items.Add(B("满状态", "fullstate"));
            row1.Items.Add(B("升级", "levelup"));
            row1.Items.Add(B("+1000钱", "addmoney 1000"));
            row1.Items.Add(B("敌人全灭", "allenemydie"));
            row1.Items.Add(B("生命-1000", "life -1000"));
            row1.Items.Add(B("清空内力", "manazero"));
            row1.Items.Add(B("清空体力", "thewzero"));

            var row2 = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 6 };
            row2.Items.Add(new Label { Text = "传送", VerticalAlignment = VerticalAlignment.Center });
            row2.Items.Add(_posX);
            row2.Items.Add(_posY);
            row2.Items.Add(Btn("传送", () => Send($"setpos {_posX.Text} {_posY.Text}")));
            row2.Items.Add(B("武功升级", "magicupcurrent"));
            row2.Items.Add(B("武功降级", "magicdown"));
            row2.Items.Add(B("重载武功", "magicreload"));

            var row3 = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 6 };
            row3.Items.Add(new Label { Text = "存档", VerticalAlignment = VerticalAlignment.Center });
            row3.Items.Add(_slot);
            row3.Items.Add(Btn("读取", () => Send("load " + _slot.Text)));
            row3.Items.Add(Btn("保存", () => Send("save " + _slot.Text)));
            row3.Items.Add(B("禁NPC AI", "disablenpcai"));
            row3.Items.Add(B("开NPC AI", "enablenpcai"));
            row3.Items.Add(_radius);
            row3.Items.Add(_npcName);
            row3.Items.Add(Btn("显示范围", () =>
                Send(string.IsNullOrWhiteSpace(_npcName.Text)
                    ? "rangedebug " + _radius.Text
                    : $"rangedebug {_radius.Text} {_npcName.Text}")));

            return new StackLayout
            {
                Spacing = 6,
                Items = { row1, row2, row3 }
            };
        }

        private Control MakeScriptPanel()
        {
            var path = new TextBox { PlaceholderText = "脚本文件路径", Width = 260 };
            var run = new Button { Text = "运行脚本" };
            run.Click += (s, e) => Send("runscript " + path.Text);
            var browse = new Button { Text = "浏览" };
            browse.Click += (s, e) =>
            {
                var dlg = new OpenFileDialog { Directory = new Uri(ToolPaths.FindDataDir()) };
                if (dlg.ShowDialog(this) == DialogResult.Ok)
                    path.Text = dlg.FileName;
            };
            var quit = new Button { Text = "退出游戏" };
            quit.Click += (s, e) => Send("quit");

            return new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Items = { new Label { Text = "脚本", VerticalAlignment = VerticalAlignment.Center },
                          path, browse, run, quit }
            };
        }

        private Button Btn(string text, Action act)
        {
            var b = new Button { Text = text };
            b.Click += (s, e) =>
            {
                if (!_conn.Connected) { SetStatus("未连接游戏"); return; }
                act();
            };
            return b;
        }

        private void Send(string command) => _conn.Send(command);

        private void TryConnect()
        {
            SetStatus(_conn.Connect() ? "已连接" : "连接失败 - 游戏是否在编辑模式运行？");
        }

        private void LaunchGame()
        {
            var dll = ToolPaths.FindGameDll();
            if (dll == null)
            {
                SetStatus("找不到 Jxqy.dll，请先构建游戏");
                return;
            }
            _gameProcess = ToolPaths.Launch(dll, "--edit");
            SetStatus("游戏已启动，等待连接...");
            // The game takes a moment to bring up the debug server.
            var timer = new UITimer { Interval = 1.0 };
            var tries = 0;
            timer.Elapsed += (s, e) =>
            {
                if (_conn.Connected || ++tries > 15) { timer.Stop(); return; }
                TryConnect();
            };
            timer.Start();
        }

        private void HandleLine(string line)
        {
            if (line.StartsWith("log "))
                AppendLog(line.Substring(4));
            else if (line.StartsWith("call "))
                AppendLog(line.Substring(5));
            else if (line.StartsWith("vars "))
                _variables.Text = line.Substring(5).Replace(',', '\n');
            else if (line.StartsWith("script "))
                ShowScriptFile(line.Substring(7));
            else if (line.StartsWith("error "))
                AppendLog("[错误] " + line.Substring(6));
            else if (!line.StartsWith("hello"))
                AppendLog(line);
        }

        private void AppendLog(string message)
        {
            _log.Text += message + Environment.NewLine;
            _log.CaretIndex = _log.Text.Length;
        }

        private void ShowScriptFile(string path)
        {
            try
            {
                var lines = File.ReadAllLines(path, Encoding.GetEncoding(936));
                var sb = new StringBuilder();
                for (var i = 0; i < lines.Length; i++)
                    sb.AppendLine((i + 1) + "  " + lines[i]);
                _scriptFile.Text = sb.ToString();
            }
            catch (Exception)
            {
                _scriptFile.Text = "【" + path + "】 读取失败！";
            }

            if (_watchedScript != path)
            {
                _watchedScript = path;
                try
                {
                    _watcher?.Dispose();
                    var full = Path.GetFullPath(path);
                    _watcher = new FileSystemWatcher(Path.GetDirectoryName(full), Path.GetFileName(full))
                    {
                        NotifyFilter = NotifyFilters.LastWrite,
                        EnableRaisingEvents = true
                    };
                    _watcher.Changed += (s, e) =>
                        Application.Instance.AsyncInvoke(() => ShowScriptFile(path));
                }
                catch (Exception) { }
            }
        }

        private void SetStatus(string text) => _status.Text = text;

        protected override void OnClosed(EventArgs e)
        {
            _watcher?.Dispose();
            _conn.Disconnect();
            base.OnClosed(e);
        }
    }
}
