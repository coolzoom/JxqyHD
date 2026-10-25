using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace GameEditor
{
    /// <summary>
    /// Line-based TCP client for the Engine edit-mode debug server
    /// (Engine/EditServer.cs, 127.0.0.1:9110 by default).
    /// </summary>
    public class GameConnection
    {
        public const int Port = 9110;

        private TcpClient _client;
        private StreamWriter _writer;
        private Thread _reader;
        public bool Connected => _client != null && _client.Connected;

        /// <summary>Raised on a background thread for each server line.</summary>
        public event Action<string> OnLine;
        public event Action OnDisconnected;

        public bool Connect()
        {
            Disconnect();
            try
            {
                _client = new TcpClient();
                var async = _client.BeginConnect("127.0.0.1", Port, null, null);
                if (!async.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2)))
                {
                    _client.Close();
                    _client = null;
                    return false;
                }
                _client.EndConnect(async);
                _writer = new StreamWriter(_client.GetStream(), Encoding.UTF8) { AutoFlush = true };
                _reader = new Thread(ReadLoop) { IsBackground = true };
                _reader.Start();
                return true;
            }
            catch (Exception)
            {
                Disconnect();
                return false;
            }
        }

        private void ReadLoop()
        {
            try
            {
                var reader = new StreamReader(_client.GetStream(), Encoding.UTF8);
                string line;
                while ((line = reader.ReadLine()) != null)
                    OnLine?.Invoke(line);
            }
            catch (Exception) { }
            OnDisconnected?.Invoke();
        }

        public void Send(string command)
        {
            try
            {
                if (Connected) _writer.WriteLine(command);
            }
            catch (Exception)
            {
                OnDisconnected?.Invoke();
            }
        }

        public void Disconnect()
        {
            try { _client?.Close(); } catch (Exception) { }
            _client = null;
            _writer = null;
        }
    }
}
