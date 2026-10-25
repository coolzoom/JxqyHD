using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;

namespace Engine
{
    /// <summary>
    /// Cross-platform movie player replacing XNA's VideoPlayer (unimplemented
    /// on DesktopGL). Decodes via two ffmpeg child processes:
    ///   video -> rawvideo rgba frames pushed into a Texture2D
    ///   audio -> s16le 48kHz stereo fed into a DynamicSoundEffectInstance
    /// Requires ffmpeg/ffprobe on PATH. If missing, Play() returns false and
    /// the game skips the movie.
    /// </summary>
    public sealed class MoviePlayer : IDisposable
    {
        private const int AudioRate = 48000;
        private const int AudioChunk = 8192; // s16le stereo bytes

        private Process _videoProc;
        private Process _audioProc;
        private DynamicSoundEffectInstance _audio;
        private Texture2D _texture;
        private byte[] _frameBuf;
        private byte[] _audioBuf;
        private int _frameFilled;
        private bool _audioEof;
        private readonly Stopwatch _clock = new Stopwatch();
        private long _nextFrame;
        private double _frameDurationMs;
        private readonly string _path;

        public MediaState State { get; private set; } = MediaState.Stopped;

        private MoviePlayer(string path) { _path = path; }

        /// <summary>Create a player for a media file, or null if unplayable.</summary>
        public static MoviePlayer Play(string filePath)
        {
            var player = new MoviePlayer(filePath);
            try
            {
                if (!player.Start()) { player.Dispose(); return null; }
            }
            catch (Exception)
            {
                player.Dispose();
                return null;
            }
            return player;
        }

        private bool Start()
        {
            if (!Probe(_path, out var w, out var h, out var fps)) return false;
            _frameBuf = new byte[w * h * 4];
            _audioBuf = new byte[AudioChunk];
            _frameDurationMs = 1000.0 / fps;
            _texture = new Texture2D(Globals.TheGame.GraphicsDevice, w, h);

            _videoProc = Spawn("ffmpeg",
                "-v error -an -i \"" + _path + "\" -f rawvideo -pix_fmt rgba -");
            _audioProc = Spawn("ffmpeg",
                "-v error -vn -i \"" + _path + "\" -ac 2 -ar " + AudioRate +
                " -f s16le -");
            _audio = new DynamicSoundEffectInstance(AudioRate, AudioChannels.Stereo);
            _clock.Start();
            _nextFrame = 0;
            State = MediaState.Playing;
            _audio.Play();
            return true;
        }

        private static Process Spawn(string exe, string args)
        {
            var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            p.Start();
            return p;
        }

        private static bool Probe(string file, out int w, out int h, out double fps)
        {
            w = h = 0; fps = 0;
            try
            {
                using (var p = Spawn("ffprobe",
                    "-v error -select_streams v:0 -show_entries " +
                    "stream=width,height,avg_frame_rate -of csv=p=0 \"" + file + "\""))
                {
                    var line = p.StandardOutput.ReadLine();
                    p.WaitForExit(5000);
                    if (string.IsNullOrEmpty(line)) return false;
                    var parts = line.Split(',');
                    if (parts.Length < 3) return false;
                    w = int.Parse(parts[0]);
                    h = int.Parse(parts[1]);
                    var frac = parts[2].Split('/');
                    fps = double.Parse(frac[0], CultureInfo.InvariantCulture);
                    if (frac.Length > 1)
                        fps /= double.Parse(frac[1], CultureInfo.InvariantCulture);
                    return w > 0 && h > 0 && fps > 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Advance decoding; call once per frame before drawing.</summary>
        public void Update()
        {
            if (State != MediaState.Playing) return;
            FeedAudio();
            PumpVideo();
        }

        private void FeedAudio()
        {
            if (_audioEof || _audio == null) return;
            try
            {
                while (_audio.PendingBufferCount < 4)
                {
                    int got = ReadFull(_audioProc.StandardOutput.BaseStream,
                        _audioBuf, 0, _audioBuf.Length);
                    if (got <= 0) { _audioEof = true; break; }
                    _audio.SubmitBuffer(_audioBuf, 0, got);
                }
            }
            catch (Exception) { _audioEof = true; }
        }

        private void PumpVideo()
        {
            var stream = _videoProc.StandardOutput.BaseStream;
            var due = (long)(_clock.ElapsedMilliseconds / _frameDurationMs);
            // Catch-up cap: skip at most 60 stale frames, then resync.
            var target = Math.Min(due, _nextFrame + 60);
            bool updated = false;
            while (_nextFrame <= target)
            {
                var got = ReadFull(stream, _frameBuf, _frameFilled,
                    _frameBuf.Length - _frameFilled);
                _frameFilled += got > 0 ? got : 0;
                if (_frameFilled < _frameBuf.Length)
                {
                    if (got <= 0) { EndOfStream(); return; }
                    return; // partial frame; wait for more data
                }
                _frameFilled = 0;
                _nextFrame++;
                updated = true;
            }
            if (updated)
            {
                try { _texture.SetData(_frameBuf); }
                catch (Exception) { }
            }
            if (target < due) _nextFrame = due; // drop lag, resync to clock
        }

        private void EndOfStream()
        {
            State = MediaState.Stopped;
            try { _audioProc?.Kill(); } catch (Exception) { }
        }

        private static int ReadFull(Stream s, byte[] buf, int off, int len)
        {
            var total = 0;
            while (total < len)
            {
                var n = s.Read(buf, off + total, len - total);
                if (n <= 0) break;
                total += n;
            }
            return total;
        }

        public Texture2D GetTexture()
        {
            Update();
            return _texture;
        }

        public void Pause()
        {
            if (State != MediaState.Playing) return;
            State = MediaState.Paused;
            _clock.Stop();
            try { _audio?.Pause(); } catch (Exception) { }
        }

        public void Resume()
        {
            if (State != MediaState.Paused) return;
            State = MediaState.Playing;
            _clock.Start();
            try { _audio?.Play(); } catch (Exception) { }
        }

        public void Stop()
        {
            State = MediaState.Stopped;
            Dispose();
        }

        public void Dispose()
        {
            State = MediaState.Stopped;
            try { _audio?.Stop(); _audio?.Dispose(); } catch (Exception) { }
            _audio = null;
            foreach (var p in new[] { _videoProc, _audioProc })
            {
                try { if (p != null && !p.HasExited) p.Kill(); p?.Dispose(); }
                catch (Exception) { }
            }
            _videoProc = _audioProc = null;
            try { _texture?.Dispose(); } catch (Exception) { }
            _texture = null;
        }
    }
}
