using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Eto.Drawing;
using Eto.Forms;
using JxqyTools;

namespace Settings
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            JxqyTools.ToolApp.Run<SettingForm>();
        }
    }

    /// <summary>
    /// Cross-platform port of the original WinForms Settings tool.
    /// Reads/writes the [Setting] section of Jxqy.ini in the Data directory.
    /// </summary>
    public class SettingForm : Form
    {
        private static readonly int[,] Resolutions =
        {
            { 800, 600 }, { 1024, 768 }, { 1280, 720 }, { 1280, 800 },
            { 1366, 768 }, { 1440, 900 }, { 1600, 900 }, { 1680, 1050 },
            { 1920, 1080 }, { 2560, 1440 }, { 3840, 2160 }
        };

        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
        private readonly string _iniPath;
        private readonly ComboBox _resolution;
        private readonly CheckBox _windowMode;
        private readonly Slider _soundVolume, _musicVolume, _maxMagic, _runSpeed;
        private readonly Label _soundText, _musicText, _speedText;

        public SettingForm()
        {
            Title = "游戏设置";
            _iniPath = Path.Combine(ToolPaths.FindDataDir(), "Jxqy.ini");
            LoadIni();

            _resolution = new ComboBox();
            var curW = Get("Width", 800);
            var curH = Get("Height", 600);
            var selected = -1;
            for (var i = 0; i < Resolutions.GetLength(0); i++)
            {
                _resolution.Items.Add(Resolutions[i, 0] + " x " + Resolutions[i, 1]);
                if (Resolutions[i, 0] == curW && Resolutions[i, 1] == curH) selected = i;
            }
            _resolution.SelectedIndex = selected >= 0 ? selected : 0;

            _windowMode = new CheckBox { Text = "窗口模式", Checked = Get("FullScreen", 0) == 0 };

            _soundVolume = Slider(0, 100, (int)(GetFloat("SoundEffectVolume", 1f) * 100));
            _musicVolume = Slider(0, 100, (int)(GetFloat("MusicVolume", 1f) * 100));
            _maxMagic = Slider(0, 1000, Get("MaxMagicUnit", 200));
            _runSpeed = Slider(1, 10, Get("RunSpeedFold", 1));

            _soundText = new Label();
            _musicText = new Label();
            _speedText = new Label();
            UpdateLabels();
            _soundVolume.ValueChanged += (s, e) => UpdateLabels();
            _musicVolume.ValueChanged += (s, e) => UpdateLabels();
            _runSpeed.ValueChanged += (s, e) => UpdateLabels();

            var save = new Button { Text = "保存" };
            save.Click += (s, e) => Save();
            var begin = new Button { Text = "开始游戏" };
            begin.Click += (s, e) =>
            {
                var dll = ToolPaths.FindGameDll();
                if (dll == null) MessageBox.Show(this, "找不到 Jxqy.dll，请先构建游戏。");
                else { ToolPaths.Launch(dll); Close(); }
            };

            Content = new TableLayout
            {
                Padding = new Padding(12),
                Spacing = new Size(8, 8),
                Rows =
                {
                    Row("分辨率", _resolution),
                    Row("", _windowMode),
                    Row("音效音量", _soundVolume, _soundText),
                    Row("音乐音量", _musicVolume, _musicText),
                    Row("最大武功单位", _maxMagic, new Label { Text = Get("MaxMagicUnit", 200).ToString() }),
                    Row("游戏速度", _runSpeed, _speedText),
                    new TableRow(save, begin)
                }
            };
        }

        private static TableRow Row(string label, Control c, Control extra = null)
        {
            var cells = new List<TableCell> { new TableCell(new Label { Text = label }), new TableCell(c, true) };
            if (extra != null) cells.Add(new TableCell(extra));
            return new TableRow(cells.ToArray());
        }

        private static Slider Slider(int min, int max, int value)
        {
            return new Slider { MinValue = min, MaxValue = max, Value = Math.Max(min, Math.Min(max, value)) };
        }

        private void UpdateLabels()
        {
            _soundText.Text = _soundVolume.Value + "%";
            _musicText.Text = _musicVolume.Value + "%";
            _speedText.Text = _runSpeed.Value.ToString();
        }

        private int Get(string key, int def)
        {
            string s;
            int v;
            return _values.TryGetValue(key, out s) && int.TryParse(s, out v) ? v : def;
        }

        private float GetFloat(string key, float def)
        {
            string s;
            float v;
            return _values.TryGetValue(key, out s) && float.TryParse(s, out v) ? v : def;
        }

        private void LoadIni()
        {
            try
            {
                if (!File.Exists(_iniPath)) return;
                var inSetting = false;
                foreach (var raw in File.ReadAllLines(_iniPath, Encoding.GetEncoding(936)))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("["))
                        inSetting = line.Equals("[Setting]", StringComparison.OrdinalIgnoreCase);
                    else if (inSetting && line.Contains("="))
                    {
                        var i = line.IndexOf('=');
                        _values[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                    }
                }
            }
            catch (Exception) { }
        }

        private void Save()
        {
            var i = _resolution.SelectedIndex;
            Set("Width", Resolutions[i, 0].ToString());
            Set("Height", Resolutions[i, 1].ToString());
            Set("FullScreen", _windowMode.Checked == true ? "0" : "1");
            Set("SoundEffectVolume", (_soundVolume.Value / 100f).ToString("0.##"));
            Set("MusicVolume", (_musicVolume.Value / 100f).ToString("0.##"));
            Set("MaxMagicUnit", _maxMagic.Value.ToString());
            Set("RunSpeedFold", _runSpeed.Value.ToString());

            try
            {
                var lines = File.Exists(_iniPath)
                    ? new List<string>(File.ReadAllLines(_iniPath, Encoding.GetEncoding(936)))
                    : new List<string>();
                var inSetting = false;
                var sectionFound = false;
                var written = new HashSet<string>();
                for (var li = 0; li < lines.Count; li++)
                {
                    var line = lines[li].Trim();
                    if (line.StartsWith("["))
                        inSetting = line.Equals("[Setting]", StringComparison.OrdinalIgnoreCase);
                    else if (inSetting)
                    {
                        sectionFound = true;
                        if (line.Contains("="))
                        {
                            var key = line.Substring(0, line.IndexOf('=')).Trim();
                            if (_values.ContainsKey(key))
                            {
                                lines[li] = key + "=" + _values[key];
                                written.Add(key);
                            }
                        }
                    }
                }
                if (!sectionFound)
                {
                    lines.Add("[Setting]");
                }
                // Append keys that were not already present in the section.
                var insertAt = lines.Count;
                for (var li = lines.Count - 1; li >= 0; li--)
                {
                    var t = lines[li].Trim();
                    if (t.Equals("[Setting]", StringComparison.OrdinalIgnoreCase)) { insertAt = li + 1; break; }
                    if (t.StartsWith("[")) break;
                    if (t.Length > 0) insertAt = li;
                }
                foreach (var kv in _values)
                    if (!written.Contains(kv.Key))
                        lines.Insert(insertAt++, kv.Key + "=" + kv.Value);

                File.WriteAllLines(_iniPath, lines.ToArray(), Encoding.GetEncoding(936));
                MessageBox.Show(this, "已保存");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "保存失败: " + ex.Message);
            }
        }

        private void Set(string key, string value) => _values[key] = value;
    }
}
