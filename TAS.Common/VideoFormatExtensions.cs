﻿namespace TAS.Common
{
    public static class VideoFormatExtensions
    {
        public static bool IsWideScreen(this TVideoFormat videoFormat)
        {
            return videoFormat != TVideoFormat.NTSC
                && videoFormat != TVideoFormat.PAL
                && videoFormat != TVideoFormat.PAL_P;
        }
    }
}
