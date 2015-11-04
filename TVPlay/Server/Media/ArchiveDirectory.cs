﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.Remoting.Messaging;
using TAS.Common;
using TAS.Data;
using TAS.Server.Interfaces;

namespace TAS.Server
{
    public class ArchiveDirectory : MediaDirectory, IArchiveDirectory
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        public override void Initialize()
        {
            _isInitialized = false; // to avoid subsequent reinitializations
            DirectoryName = "Archiwum";
            GetVolumeInfo();
            Debug.WriteLine("ArchiveDirectory {0} initialized", Folder, null);
        }
        public UInt64 IdArchive { get; internal set; }

        private string _searchString;
        public string SearchString
        {
            get { return _searchString; }
            set
            {
                if (_searchString != value)
                {
                    _searchString = value;
                    NotifyPropertyChanged("SearchString");
                }
            }
        }

        public override void Refresh()
        {
            Search();
        }

        public void Search()
        {
            this.DbSearch();
        }

        public TMediaCategory? SearchMediaCategory { get; set; }

        public override void SweepStaleMedia()
        {
            DateTime currentDate = DateTime.UtcNow.Date;
            this.DbFindStaleMedia();
            IEnumerable<IMedia> StaleMediaList;
            _files.Lock.EnterReadLock();
            try
            {
                StaleMediaList = _files.Where(m => (m is ArchiveMedia) && currentDate > (m as ArchiveMedia).KillDate);
            }
            finally
            {
                _files.Lock.ExitReadLock();
            }
            foreach (Media m in StaleMediaList)
                m.Delete();
        }

        public IArchiveMedia Find(IMedia media)
        {
            return this.DbMediaFind(media);
        }

        internal void Clear()
        {
            _files.Lock.EnterUpgradeableReadLock();
            try
            {
                foreach (Media m in _files.ToList())
                    base.MediaRemove(m); //base: to not actually delete file and db
            }
            finally
            {
                _files.Lock.ExitUpgradeableReadLock();
            }
                 
        }

        protected override IMedia CreateMedia(string fileNameOnly)
        {
            return new ArchiveMedia(this) { FileName = fileNameOnly };
        }

        protected override IMedia CreateMedia(string fileNameOnly, Guid guid)
        {
            return new ArchiveMedia(this, guid) { FileName = fileNameOnly };
        }

        //public override void MediaAdd(Media media)
        //{
        //    // do not add to _files
        //}

        public override bool DeleteMedia(IMedia media)
        {
            if (base.DeleteMedia(media))
            {
                MediaRemove(media);
                return true;
            }
            return false;
        }
        
        public override void MediaRemove(IMedia media)
        {
            ArchiveMedia m = (ArchiveMedia)media;
            m.MediaStatus = TMediaStatus.Deleted;
            m.Verified = false;
            m.Save();
            base.MediaRemove(media);
        }

        protected override void OnMediaRenamed(IMedia media, string newName)
        {
            base.OnMediaRenamed(media, newName);
            ((ArchiveMedia)media).Save();
        }

        public IArchiveMedia GetArchiveMedia(IMedia media, bool searchExisting = true)
        {
            IArchiveMedia result = null;
            if (searchExisting)
                result = this.DbMediaFind(media);
            if (result == null)
                result = new ArchiveMedia(this, media.MediaGuid)
                {
                    AudioChannelMapping = media.AudioChannelMapping,
                    AudioVolume = media.AudioVolume,
                    AudioLevelIntegrated = media.AudioLevelIntegrated,
                    AudioLevelPeak = media.AudioLevelPeak,
                    Duration = media.Duration,
                    DurationPlay = media.DurationPlay,
                    FileName = (media is IngestMedia) ? Path.GetFileNameWithoutExtension(media.FileName) + DefaultFileExtension(media.MediaType) : media.FileName,
                    FileSize = media.FileSize,
                    Folder = GetCurrentFolder(),
                    LastUpdated = media.LastUpdated,
                    MediaName = media.MediaName,
                    MediaStatus = TMediaStatus.Required,
                    TCStart = media.TCStart,
                    TCPlay = media.TCPlay,
                    VideoFormat = media.VideoFormat,
                    KillDate = (media is PersistentMedia) ? (media as PersistentMedia).KillDate : (media is IngestMedia ? media.LastUpdated + TimeSpan.FromDays(((IngestDirectory)media.Directory).MediaRetnentionDays) : default(DateTime)),
                    IdAux = (media is PersistentMedia) ? (media as PersistentMedia).IdAux : string.Empty,
                    idProgramme = (media is PersistentMedia) ? (media as PersistentMedia).idProgramme : 0L,
                    MediaType = (media.MediaType == TMediaType.Unknown) ? (StillFileTypes.Any(ve => ve == Path.GetExtension(media.FullPath).ToLowerInvariant()) ? TMediaType.Still : TMediaType.Movie) : media.MediaType,
                    MediaCategory = media.MediaCategory,
                    Parental = media.Parental,
                    OriginalMedia = media,
                };
            return result;
        }

        public void ArchiveSave(IMedia media, TVideoFormat outputFormat, bool deleteAfterSuccess)
        {
            IArchiveMedia toMedia = GetArchiveMedia(media);
            if (media is ServerMedia)
            {
                _archiveCopy(media, toMedia, deleteAfterSuccess, false);
            }
            if (media is IngestMedia)
            {
                FileManager.Queue(new ConvertOperation { SourceMedia = media, DestMedia = toMedia, SuccessCallback = GetVolumeInfo, OutputFormat = outputFormat});
            }
        }

        public void ArchiveRestore(IArchiveMedia media, IServerMedia mediaPGM, bool toTop)
        {
            if (mediaPGM != null)
                _archiveCopy(media, mediaPGM, false, toTop);
        }

        internal string GetCurrentFolder()
        {
            return DateTime.UtcNow.ToString("yyyyMM"); 
        }

        private void _archiveCopy(IMedia fromMedia, IMedia toMedia, bool deleteAfterSuccess, bool toTop)
        {
            if (fromMedia.MediaGuid == toMedia.MediaGuid && fromMedia.FilePropertiesEqual(toMedia))
            {
                if (deleteAfterSuccess)
                    FileManager.Queue(new FileOperation { Kind = TFileOperationKind.Delete, SourceMedia = fromMedia, SuccessCallback = GetVolumeInfo}, toTop);
            }
            else
            {
                if (!Directory.Exists(Path.GetDirectoryName(toMedia.FullPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(toMedia.FullPath));
                FileManager.Queue(new FileOperation { Kind = deleteAfterSuccess ? TFileOperationKind.Move : TFileOperationKind.Copy, SourceMedia = fromMedia, DestMedia = toMedia, SuccessCallback = GetVolumeInfo }, toTop);
            }
        }    

    }
}
