using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Engine.Gui;
using Engine.ListManager;
using Engine.Map;
using Engine.Script;
using Engine.Storage;
using Microsoft.Xna.Framework;

namespace Engine
{
    /// <summary>
    /// Line-based debug server used in edit mode. The cross-platform GameEditor
    /// tool connects to 127.0.0.1:Port and sends text commands; game events
    /// (log, script calls, variables) are pushed back to connected clients.
    /// All commands are executed on the game thread via Pump().
    /// </summary>
    public class EditServer
    {
        public const int DefaultPort = 9110;
        public static EditServer Instance { get; private set; }

        private readonly ConcurrentQueue<string> _commands = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> _events = new ConcurrentQueue<string>();
        private TcpListener _listener;
        private Thread _thread;
        private volatile bool _running;

        public static void Start(int port = DefaultPort)
        {
            if (Instance != null) return;
            Instance = new EditServer();
            Instance.StartInternal(port);
        }

        private void StartInternal(int port)
        {
            _running = true;
            _thread = new Thread(() => Run(port)) { IsBackground = true };
            _thread.Start();

            var sender = Globals.TheMessageSender;
            sender.OnFunctionCall += s => Broadcast("call " + s);
            sender.OnScriptFileChange += s => Broadcast("script " + s);
            // Script variables are reported every frame; only forward when the
            // serialized form actually changes, and at most twice a second.
            string lastVars = null;
            var lastVarsTime = DateTime.MinValue;
            sender.OnScriptVariables += vars =>
            {
                var now = DateTime.UtcNow;
                if (now - lastVarsTime < TimeSpan.FromMilliseconds(500)) return;
                var s = "vars " + string.Join(",", vars.Select(kv => kv.Key + "=" + kv.Value));
                if (s == lastVars) return;
                lastVars = s;
                lastVarsTime = now;
                Broadcast(s);
            };
            sender.OnLog += s => Broadcast("log " + s);
        }

        private void Run(int port)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Loopback, port);
                _listener.Start();
            }
            catch (Exception e)
            {
                Log.LogMessage("Edit server failed to start: " + e.Message);
                return;
            }
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
                }
                catch (Exception)
                {
                    if (!_running) return;
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            {
                var stream = client.GetStream();
                var reader = new StreamReader(stream, Encoding.UTF8);
                var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                var done = new ManualResetEventSlim();

                var pump = new Thread(() =>
                {
                    try
                    {
                        while (client.Connected && _running)
                        {
                            string line;
                            while (_events.TryDequeue(out line))
                                writer.WriteLine(line);
                            Thread.Sleep(50);
                        }
                    }
                    catch (Exception) { }
                    done.Set();
                }) { IsBackground = true };
                pump.Start();

                try
                {
                    writer.WriteLine("hello jxqy-edit 1");
                    string line;
                    while ((line = reader.ReadLine()) != null)
                        _commands.Enqueue(line);
                }
                catch (Exception) { }
                finally
                {
                    done.Set();
                    client.Close();
                }
            }
        }

        private void Broadcast(string message)
        {
            _events.Enqueue(message.Replace('\n', ' ').Replace('\r', ' '));
        }

        /// <summary>
        /// Execute queued commands. Call once per frame on the game thread.
        /// </summary>
        public void Pump()
        {
            string command;
            while (_commands.TryDequeue(out command))
            {
                try
                {
                    Execute(command);
                }
                catch (Exception e)
                {
                    Broadcast("error " + e.Message);
                }
            }
        }

        private void Execute(string command)
        {
            var parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;
            var player = Globals.ThePlayer;
            switch (parts[0].ToLowerInvariant())
            {
                case "ping":
                    Broadcast("pong");
                    break;
                case "fullstate":
                    if (player == null) break;
                    player.FullLife();
                    player.FullMana();
                    player.FullThew();
                    break;
                case "levelup":
                    player?.LevelUp();
                    break;
                case "addmoney":
                    player?.AddMoney(parts.Length > 1 ? int.Parse(parts[1]) : 1000);
                    break;
                case "allenemydie":
                    NpcManager.AllEnemyDie();
                    break;
                case "setpos":
                    if (parts.Length >= 3)
                        Globals.PlayerKindCharacter?.SetPosition(
                            new Vector2(int.Parse(parts[1]), int.Parse(parts[2])));
                    break;
                case "life":
                    if (parts.Length >= 2) player?.AddLife(int.Parse(parts[1]));
                    break;
                case "manazero":
                    if (player != null) player.Mana = 0;
                    break;
                case "thewzero":
                    if (player != null) player.Thew = 0;
                    break;
                case "magicup":
                {
                    var info = player?.XiuLianMagic;
                    if (info?.TheMagic != null)
                        player.AddMagicExp(info, info.TheMagic.LevelupExp - info.Exp + 1);
                    break;
                }
                case "magicupcurrent":
                {
                    var info = player?.CurrentMagicInUse;
                    if (info?.TheMagic != null)
                        player.AddMagicExp(info, info.TheMagic.LevelupExp - info.Exp + 1);
                    break;
                }
                case "magicdown":
                {
                    var info = player?.CurrentMagicInUse;
                    if (info?.TheMagic != null && info.Level >= 2)
                    {
                        info.Exp = info.Level > 2 ? info.TheMagic.GetLevel(info.Level - 2).LevelupExp : 0;
                        info.TheMagic = info.TheMagic.GetLevel(info.Level - 1);
                        GuiManager.ShowMessage("武功 " + info.TheMagic.Name + " 降级了");
                    }
                    break;
                }
                case "magicreload":
                {
                    if (player == null) break;
                    var index = player.CurrentUseMagicIndex;
                    MagicListManager.SavePlayerList(StorageBase.MagicListFilePath);
                    MagicListManager.LoadPlayerList(StorageBase.MagicListFilePath);
                    player.CurrentUseMagicIndex = index;
                    player.XiuLianMagic = MagicListManager.GetItemInfo(MagicListManager.XiuLianIndex);
                    break;
                }
                case "disablenpcai":
                    ScriptExecuter.DisableNpcAI();
                    break;
                case "enablenpcai":
                    ScriptExecuter.EnableNpcAI();
                    break;
                case "rangedebug":
                    if (parts.Length >= 2)
                    {
                        var radius = int.Parse(parts[1]);
                        var character = Globals.PlayerKindCharacter;
                        if (parts.Length >= 3)
                            character = NpcManager.GetNpc(parts[2]);
                        if (character != null) character.ShowRangeRadius(radius);
                        else Broadcast("error npc not found");
                    }
                    break;
                case "load":
                    if (parts.Length >= 2)
                    {
                        Loader.LoadGame(int.Parse(parts[1]));
                        GuiManager.ShowSaveLoad(false);
                        GuiManager.ShowTitle(false);
                    }
                    break;
                case "save":
                    if (parts.Length >= 2)
                        Saver.SaveGame(int.Parse(parts[1]), Globals.TheGame.TakeSnapShot());
                    break;
                case "runscript":
                {
                    var file = string.Join(" ", parts.Skip(1));
                    if (File.Exists(Utils.FixPath(file)))
                    {
                        var script = new ScriptParser();
                        script.ReadFromLines(
                            File.ReadAllLines(Utils.FixPath(file), Globals.LocalEncoding), file);
                        ScriptManager.RunScript(script);
                    }
                    else Broadcast("error script not found: " + file);
                    break;
                }
                case "quit":
                    Globals.TheGame.ExitGameImmediately();
                    break;
                default:
                    Broadcast("error unknown command: " + parts[0]);
                    break;
            }
        }

        public void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch (Exception) { }
        }
    }
}
