using System;
using System.IO;
using System.Text;

namespace VikingsForHire.Diagnostics
{
    /// <summary>Appends to BepInEx/VikingsForHire.log, rotating to VikingsForHire.1.log … .3.log at the size limit.</summary>
    internal sealed class LogFileSink : IDisposable
    {
        private const int Backups = 3;

        private readonly string _path;
        private readonly object _lock = new();
        private long _maxBytes;
        private StreamWriter? _writer;
        private long _length;

        public string Path => _path;

        public LogFileSink(string path, int maxMegabytes)
        {
            _path = path;
            _maxBytes = Math.Max(1, maxMegabytes) * 1024L * 1024L;
            Open();
        }

        public void SetMaxMegabytes(int maxMegabytes)
        {
            lock (_lock)
                _maxBytes = Math.Max(1, maxMegabytes) * 1024L * 1024L;
        }

        public void Write(string line)
        {
            lock (_lock)
            {
                if (_writer == null)
                    return;
                if (_length + line.Length + 1 > _maxBytes)
                    Rotate();
                _writer!.WriteLine(line);
                _length += Encoding.UTF8.GetByteCount(line) + 1;
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _writer?.Dispose();
                _writer = null;
            }
        }

        private void Open()
        {
            if (File.Exists(_path) && new FileInfo(_path).Length > _maxBytes)
                Shift();
            _writer = new StreamWriter(new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false))
            {
                AutoFlush = true,
            };
            _length = new FileInfo(_path).Length;
        }

        private void Rotate()
        {
            _writer?.Dispose();
            Shift();
            Open();
        }

        private void Shift()
        {
            string dir = System.IO.Path.GetDirectoryName(_path)!;
            string stem = System.IO.Path.GetFileNameWithoutExtension(_path);
            string Backup(int i) => System.IO.Path.Combine(dir, $"{stem}.{i}.log");

            if (File.Exists(Backup(Backups)))
                File.Delete(Backup(Backups));
            for (int i = Backups - 1; i >= 1; i--)
                if (File.Exists(Backup(i)))
                    File.Move(Backup(i), Backup(i + 1));
            File.Move(_path, Backup(1));
        }
    }
}
