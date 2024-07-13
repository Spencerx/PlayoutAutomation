﻿using System.Collections.Generic;
using TAS.Common.Interfaces.Media;
using TAS.Common.Interfaces.MediaDirectory;

namespace TAS.Server.Media
{
    public class MediaSearchProvider : SearchProvider<IMedia>, IMediaSearchProvider
    {
        public MediaSearchProvider(IEnumerable<IMedia> source) : base(source)
        {
        }
    }
}
