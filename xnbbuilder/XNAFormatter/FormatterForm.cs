using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Eto.Drawing;
using Eto.Forms;

namespace XNAFormatter
{
    /// <summary>
    /// Cross-platform port of the original XNB builder GUI. Instead of the
    /// Windows-only XNA Content Pipeline it shells out to the MonoGame
    /// Content Builder (dotnet mgcb) which produces .xnb files on any OS.
    /// </summary>
    public class FormatterForm : Form
    {
        private readonly TextBox _source, _output;
        private readonly ComboBox _platform;
        private readonly CheckBox _compress;
        private readonly TextArea _status;
        private string[] _files;

        public FormatterForm()
        {
            Title = "内容构建器 (mgcb)";
            ClientSize = new Size(560, 320);

            _source = new TextBox { ReadOnly = true, PlaceholderText = "源文件" };
            var browse = new Button { Text = "浏览" };
            browse.Click += (s, e) =>
            {
                var dlg = new OpenFileDialog
                {
                    MultiSelect = true,
                    Filters =
                    {
                        new FileFilter("所有可用类型", ".bmp", ".dds", ".jpg", ".png", ".tga",
                            ".fbx", ".fx", ".spritefont", ".x", ".xml", ".mp3", ".wav", ".ogg")
                    }
                };
                if (dlg.ShowDialog(this) == DialogResult.Ok)
                {
                    _files = new List<string>(dlg.Filenames).ToArray();
                    _source.Text = _files.Length > 30
                        ? "多个文件"
                        : string.Join(", ", Array.ConvertAll(_files, Path.GetFileName));
                }
            };

            _output = new TextBox { Text = Path.Combine(Directory.GetCurrentDirectory(), "Content") };
            var outBrowse = new Button { Text = "浏览" };
            outBrowse.Click += (s, e) =>
            {
                var dlg = new SelectFolderDialog();
                if (dlg.ShowDialog(this) == DialogResult.Ok)
                    _output.Text = dlg.Directory;
            };

            _platform = new ComboBox();
            foreach (var p in new[] { "DesktopGL", "Windows", "Android", "iOS" })
                _platform.Items.Add(p);
            _platform.SelectedIndex = 0;
            _compress = new CheckBox { Text = "压缩内容" };

            var convert = new Button { Text = "转换" };
            convert.Click += (s, e) => Convert();
            _status = new TextArea { ReadOnly = true };

            Content = new TableLayout
            {
                Padding = new Padding(10),
                Spacing = new Size(8, 8),
                Rows =
                {
                    new TableRow(new Label { Text = "源文件" }, _source, browse),
                    new TableRow(new Label { Text = "输出目录" }, _output, outBrowse),
                    new TableRow(new Label { Text = "平台" }, _platform, _compress),
                    new TableRow(null, convert, null),
                    new TableRow(_status) { ScaleHeight = true }
                }
            };
        }

        private void Convert()
        {
            if (_files == null || _files.Length == 0)
            {
                _status.Text = "没设置源文件";
                return;
            }
            _status.Text = "";
            var ok = 0;
            foreach (var file in _files)
            {
                int exitCode;
                var output = JxqyTools.ToolPaths.RunMgcb(file, _output.Text,
                    _platform.Text, _compress.Checked == true, out exitCode);
                if (exitCode == 0)
                {
                    ok++;
                    _status.Text += "OK   " + Path.GetFileName(file) + "\n";
                }
                else
                {
                    _status.Text += "FAIL " + Path.GetFileName(file) + "\n    " +
                                    output.Trim().Replace("\n", "\n    ") + "\n";
                }
            }
            _status.Text += $"完成: {ok}/{_files.Length} 成功";
            _files = null;
        }
    }
}
