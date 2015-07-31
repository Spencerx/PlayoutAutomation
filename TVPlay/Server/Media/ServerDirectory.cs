﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.Remoting.Messaging;
using TAS.Common;

namespace TAS.Server
{
    public class ServerDirectory : MediaDirectory
    {
        public readonly PlayoutServer Server;
        public ServerDirectory(PlayoutServer server): base()
        {
            Server = server;
        }

        public override void Initialize()
        {
            DatabaseConnector.ServerLoadMediaDirectory(this, Server);
            base.Initialize();
            InvokeVerifyMedia();
            Debug.WriteLine(this, "Directory initialized");
        }

        protected override Media CreateMedia()
        {
            ServerMedia newMedia = new ServerMedia() { 
                Directory = this,
            };
            return newMedia;
        }

        public event EventHandler<MediaEventArgs> MediaSaved;
        internal virtual void OnMediaSaved(Media media)
        {
            var handler = MediaSaved;
            if (handler != null)
                handler(media, new MediaEventArgs(media));
        }

        public override void MediaAdd(Media media)
        {
            base.MediaAdd(media);
            media.PropertyChanged += OnMediaPropertyChanged;
            if (media.MediaStatus != TMediaStatus.Required)
                media.InvokeVerify();
        }

        public override void MediaRemove(Media media)
        {
            ServerMedia m = (ServerMedia)media;
            m.MediaStatus = TMediaStatus.Deleted;
            m.Verified = false;
            m.Save();
            media.PropertyChanged -= OnMediaPropertyChanged;
            base.MediaRemove(media);
        }

        public event PropertyChangedEventHandler MediaPropertyChanged;

        internal virtual void OnMediaPropertyChanged(object o, PropertyChangedEventArgs e)
        {
            if (this.MediaPropertyChanged != null)
                this.MediaPropertyChanged(o, e);
        }

        public override void SweepStaleMedia()
        {
            DateTime currentDateTime = DateTime.UtcNow.Date;
            IEnumerable<Media> StaleMediaList;
            _files.Lock.EnterReadLock();
            try
            {
                StaleMediaList = _files.Where(m => (m is ServerMedia) && currentDateTime > (m as ServerMedia).KillDate);
            }
            finally
            {
                _files.Lock.ExitReadLock();
            }
            foreach (Media m in StaleMediaList)
                m.Delete();
        }

        public ServerMedia GetServerMedia(Media media, bool searchExisting = true)
        {
            if (media == null)
                return null;
            ServerMedia fm = null;
            _files.Lock.EnterUpgradeableReadLock();
            try
            {
                fm = (ServerMedia)FindMedia(media);
                if (fm == null || !searchExisting)
                {
                    _files.Lock.EnterWriteLock();
                    try
                    {
                        fm = (new ServerMedia()
                        {
                            _mediaName = media.MediaName,
                            _folder = string.Empty,
                            _fileName = (media is IngestMedia) ? (VideoFileTypes.Any(ext => ext == Path.GetExtension(media.FileName).ToLower()) ? Path.GetFileNameWithoutExtension(media.FileName) : media.FileName) + DefaultFileExtension(media.MediaType) : media.FileName,
                            MediaType = (media.MediaType == TMediaType.Unknown) ? (StillFileTypes.Any(ve => ve == Path.GetExtension(media.FullPath).ToLowerInvariant()) ? TMediaType.Still : TMediaType.Movie) : media.MediaType,
                            _mediaStatus = TMediaStatus.Required,
                            _tCStart = media.TCStart,
                            _tCPlay = media.TCPlay,
                            _duration = media.Duration,
                            _durationPlay = media.DurationPlay,
                            _videoFormat = media.VideoFormat,
                            _audioChannelMapping = media.AudioChannelMapping,
                            _audioVolume = media.AudioVolume,
                            _audioLevelIntegrated = media.AudioLevelIntegrated,
                            _audioLevelPeak = media.AudioLevelPeak,
                            KillDate = (media is PersistentMedia) ? (media as PersistentMedia).KillDate : ((media is IngestMedia && (media.Directory as IngestDirectory).MediaRetnentionDays > 0) ? DateTime.Today + TimeSpan.FromDays(((IngestDirectory)media.Directory).MediaRetnentionDays) : default(DateTime)),
                            DoNotArchive = (media is ServerMedia && (media as ServerMedia).DoNotArchive)
                                         || media is IngestMedia && ((media as IngestMedia).Directory as IngestDirectory).MediaDoNotArchive,
                            HasExtraLines = media is ServerMedia && (media as ServerMedia).HasExtraLines,
                            _mediaCategory = media.MediaCategory,
                            _parental = media.Parental,
                            idAux = (media is PersistentMedia) ? (media as PersistentMedia).idAux : string.Empty,
                            idFormat = (media is PersistentMedia) ? (media as PersistentMedia).idFormat : 0L,
                            idProgramme = (media is PersistentMedia) ? (media as PersistentMedia).idProgramme : 0L,
                            _mediaGuid = fm == null ? media.MediaGuid : Guid.NewGuid(), // in case file with the same GUID already exists and we need to get new one
                            OriginalMedia = media,
                            Directory = this,
                        });
                    }
                    finally
                    {
                        _files.Lock.ExitWriteLock();
                    }
                    fm.PropertyChanged += MediaPropertyChanged;
                }
                else
                    if (fm.MediaStatus == TMediaStatus.Deleted)
                        fm.MediaStatus = TMediaStatus.Required;
            }
            finally
            {
                _files.Lock.ExitUpgradeableReadLock();
            }
            return fm;
        }

        public override Media FindMedia(Media media)
        {
            if (media is ServerMedia && media.Directory == this)
                return media;
            if (media == null)
                return null;
            _files.Lock.EnterReadLock();
            try
            {
                return _files.FirstOrDefault(m => m.MediaGuid == (media.MediaGuid));
            }
            finally
            {
                _files.Lock.ExitReadLock();
            }
        }
        
        protected override void OnMediaRenamed(Media media, string newName)
        {
            base.OnMediaRenamed(media, newName);
            ((ServerMedia)media).Save();
        }
        
        public void InvokeVerifyMedia()
        {
            VerifyMediaDelegate worker = new VerifyMediaDelegate(VerifyMediaWorker);
            IEnumerable<Media> unverifiedFiles;
            _files.Lock.EnterReadLock();
            try
            {
                unverifiedFiles = _files.Where(mf => ((ServerMedia)mf).Verified == false).ToList();
            }
            finally
            {
                _files.Lock.ExitReadLock();
            }
            lock (_VerifyWorkerSyncObject)
            {
                if (_IsVerificationRunning)
                {
                    _RepeatVerificationAfterFinish = true;
                    return;
                }
                AsyncOperation verificationAsyncOperation = AsyncOperationManager.CreateOperation(null);
                worker.BeginInvoke(unverifiedFiles, VerifyMediaCompletedCallback, verificationAsyncOperation);
                _IsVerificationRunning = true;
            }
        }
        private bool _IsVerificationRunning = false;
        private bool _RepeatVerificationAfterFinish = false;
        private readonly object _VerifyWorkerSyncObject = new object();
        private delegate void VerifyMediaDelegate(IEnumerable<Media> MediaFiles);
        private void VerifyMediaWorker(IEnumerable<Media> unverifiedFiles)
        {
            foreach (ServerMedia mf in unverifiedFiles)
                mf.Verify();
        }
        private void VerifyMediaCompletedCallback(IAsyncResult ar)
        {
            // get the original worker delegate and the AsyncOperation instance
            VerifyMediaDelegate worker = (VerifyMediaDelegate)((AsyncResult)ar).AsyncDelegate;
            AsyncOperation asyncState = (AsyncOperation)ar.AsyncState;
            // finish the asynchronous operation
            worker.EndInvoke(ar);

            // clear the running task flag
            lock (_VerifyWorkerSyncObject)
            {
                _IsVerificationRunning = false;
            }

            if (_RepeatVerificationAfterFinish)
                InvokeVerifyMedia();
            else
            {
                // raise the completed event
                AsyncCompletedEventArgs completedArgs = new AsyncCompletedEventArgs(null,
                  false, null);
                asyncState.PostOperationCompleted(
                  delegate(object e) { OnVerificationTaskCompleted((AsyncCompletedEventArgs)e); },
                  completedArgs);
            }
        }

        public event AsyncCompletedEventHandler VerificationTaskCompleted;
        protected virtual void OnVerificationTaskCompleted(AsyncCompletedEventArgs e)
        {
            if (VerificationTaskCompleted != null)
                VerificationTaskCompleted(this, e);
        }

    }
}