using System;
using System.IO;
using Microsoft.Xna.Framework.Media;

namespace Engine
{
    static public class BackgroundMusic
    {
        private static string _fileName;

        public static string FileName
        {
            get { return _fileName; }
        }

        public static void Play(string fileName)
        {
            _fileName = fileName;
            if (string.IsNullOrEmpty(fileName))
            {
                //Stop music if file name is empty
                Stop();
                return;
            }
            try
            {
                var name = Path.GetFileNameWithoutExtension(fileName);
                Song song = null;
                // Loose audio files (music/*.ogg|.mp3|.wav) work without the
                // content pipeline; fall back to Content.Load for .xnb.
                foreach (var ext in new[] { ".ogg", ".mp3", ".wav", ".wma" })
                {
                    var file = Utils.FixPath(@"music/" + name + ext);
                    if (File.Exists(file))
                    {
                        song = Song.FromUri(name, new Uri(Path.GetFullPath(file)));
                        break;
                    }
                }
                if (song == null)
                    song = Globals.TheGame.Content.Load<Song>(@"music/" + name);
                MediaPlayer.IsRepeating = true;
                MediaPlayer.Play(song);
            }
            catch (Exception exception)
            {
                Log.LogFileLoadError("Music file", fileName, exception);
            }
        }

        public static void Stop()
        {
            MediaPlayer.Stop();
            _fileName = string.Empty;
        }

        public static void Pause()
        {
            MediaPlayer.Pause();
        }

        public static void Resume()
        {
            MediaPlayer.Resume();
        }

        public static void SetVolume(float volume)
        {
            MediaPlayer.Volume = volume;
        }

        public static float GetVolume()
        {
            return MediaPlayer.Volume;
        }
    }
}
