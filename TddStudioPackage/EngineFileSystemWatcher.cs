using R4nd0mApps.TddStud10.Engine.Core;
using R4nd0mApps.TddStud10.Logger;
using System;
using System.IO;

namespace R4nd0mApps.TddStud10.Hosts.VS
{
    internal sealed class EngineFileSystemWatcher : IDisposable
    {
        private static readonly ILogger Logger = LoggerFactory.logger;

        private Action _action;

        public static EngineFileSystemWatcher Create(EngineParams engineParams, Action runEngine)
        {
            var efsWatcher = new EngineFileSystemWatcher
            {
                _action = runEngine,
                _fsWatcher = new FileSystemWatcher
                {
                    Filter = "*",
                    Path = Path.GetDirectoryName(engineParams.SolutionPath.ToString()),
                    IncludeSubdirectories = true
                }
            };


            efsWatcher.SubscribeToEvents();

            return efsWatcher;
        }

        private EngineFileSystemWatcher()
        {
        }

        internal bool IsEnabled()
        {
            return _fsWatcher.EnableRaisingEvents;
        }

        public void Enable()
        {
            _fsWatcher.EnableRaisingEvents = true;
        }

        internal void Disable()
        {
            _fsWatcher.EnableRaisingEvents = false;
        }

        #region IDisposable

        private bool _disposed;

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    UnsubscribeToEvents();
                    _fsWatcher.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~EngineFileSystemWatcher()
        {
            Dispose(false);
        }

        #endregion IDisposable

        #region FileSystemWatcher

        private FileSystemWatcher _fsWatcher;

        private void SubscribeToEvents()
        {
            _fsWatcher.Created += FsWatcher_Created;
            _fsWatcher.Changed += FsWatcher_Changed;
            _fsWatcher.Renamed += FsWatcher_Renamed;
            _fsWatcher.Deleted += FsWatcher_Deleted;
            _fsWatcher.Error += FsWatcher_Error;
        }

        private void UnsubscribeToEvents()
        {
            _fsWatcher.Error -= FsWatcher_Error;
            _fsWatcher.Deleted -= FsWatcher_Deleted;
            _fsWatcher.Renamed -= FsWatcher_Renamed;
            _fsWatcher.Changed -= FsWatcher_Changed;
            _fsWatcher.Created -= FsWatcher_Created;
        }

        void FsWatcher_Error(object sender, ErrorEventArgs e)
        {
            Logger.LogError(e.ToString());
            _action();
        }

        void FsWatcher_Created(object sender, FileSystemEventArgs e)
        {
            Logger.LogInfo("########: FSWatcher: Got created event: {0}", e.FullPath);
            _action();
        }

        void FsWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            Logger.LogInfo("########: FSWatcher: Got changed event: {0}", e.FullPath);
            _action();
        }

        void FsWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            Logger.LogInfo("########: FSWatcher: Got renamed event: {0}", e.FullPath);
            _action();
        }

        void FsWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            Logger.LogInfo("########: FSWatcher: Got deleted event: {0}", e.FullPath);
            _action();
        }

        #endregion
    }
}
