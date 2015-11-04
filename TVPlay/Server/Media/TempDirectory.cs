﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Configuration;
using System.Diagnostics;
using TAS.Server.Interfaces;

namespace TAS.Server
{
    public class TempDirectory: MediaDirectory
    {
        public TempDirectory()
        {
            _folder = ConfigurationManager.AppSettings["TempDirectory"];
        }

        protected override IMedia CreateMedia(string fileNameOnly)
        {
            throw new InvalidOperationException("Temp media must have OriginalMedia property. Use Get() to acquire one.");
        }

        protected override IMedia CreateMedia(string fileNameOnly, Guid guid)
        {
            throw new InvalidOperationException("Temp media must have OriginalMedia property. Use Get() to acquire one.");
        }

        public override void MediaAdd(IMedia media)
        {
            // do not add to _files
        }

        public override void Refresh()
        {
            
        }
        
        public TempMedia CreateMedia(IMedia media, string fileExtension = null)
        {
            return new TempMedia(this, media, fileExtension);
        }

        public override void SweepStaleMedia()
        {
            foreach (string fileName in Directory.GetFiles(_folder))
                try
                {
                    File.Delete(fileName);
                }
                catch { }
        }
    }
}
