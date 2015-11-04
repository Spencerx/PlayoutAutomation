﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TAS.Common;
using TAS.FFMpegUtils;
using TAS.Server.Interfaces;

namespace TAS.Server
{
    public static class MediaChecker
    {
        internal static void Check(Media media)
        {

            if (media.MediaType == TMediaType.Movie || media.MediaType == TMediaType.Unknown)
            {
                TimeSpan videoDuration;
                TimeSpan audioDuration;
                int startTickCunt = Environment.TickCount;
                using (FFMpegWrapper ffmpeg = new FFMpegWrapper(media.FullPath))
                {
                    Rational frameRate = ffmpeg.GetFrameRate();
                    videoDuration = ffmpeg.GetFrameCount().SMPTEFramesToTimeSpan(new RationalNumber(frameRate.Num, frameRate.Den));
                    audioDuration = (TimeSpan)ffmpeg.GetAudioDuration();
                    if (videoDuration == TimeSpan.Zero)
                    {
                        MediaInfoLib.MediaInfo mi = new MediaInfoLib.MediaInfo();
                        try
                        {
                            mi.Open(media.FullPath);
                            long frameCount;
                            if (long.TryParse(mi.Get(MediaInfoLib.StreamKind.Video, 0, "FrameCount"), out frameCount))
                                videoDuration = frameCount.SMPTEFramesToTimeSpan(media.VideoFormatDescription.FrameRate);
                            long audioMilliseconds;
                            if (long.TryParse(mi.Get(MediaInfoLib.StreamKind.Audio, 0, "Duration"), out audioMilliseconds))
                                audioDuration = TimeSpan.FromMilliseconds(audioMilliseconds);
                            //mi.Option("Complete");
                            //Debug.WriteLine(mi.Inform());
                        }
                        finally
                        {
                            mi.Close();
                        }
                    }

                    media.Duration = videoDuration;
                    if (media.DurationPlay == TimeSpan.Zero || media.DurationPlay > videoDuration)
                        media.DurationPlay = videoDuration;
                    int w = ffmpeg.GetWidth();
                    int h = ffmpeg.GetHeight();
                    FieldOrder order = ffmpeg.GetFieldOrder();
                    Rational sar = ffmpeg.GetSAR();
                    if (h == 608 && w == 720)
                    {
                        media._hasExtraLines = true;
                        h = 576;
                    }
                    else
                        media._hasExtraLines = false;

                    RationalNumber sAR = (h == 576 && ((sar.Num == 608 && sar.Den == 405) || (sar.Num == 1 && sar.Den == 1))) ? VideoFormatDescription.Descriptions[TVideoFormat.PAL_FHA].SAR
                        : (sar.Num == 152 && sar.Den == 135) ? VideoFormatDescription.Descriptions[TVideoFormat.PAL].SAR
                        : new RationalNumber(sar.Num, sar.Den);
                    
                    var vfd = VideoFormatDescription.Match(new System.Drawing.Size(w, h), new RationalNumber(frameRate.Num, frameRate.Den), sAR, order != FieldOrder.PROGRESSIVE);
                    media.VideoFormat = vfd.Format;
                    media.VideoFormatDescription = vfd;

                    if (videoDuration > TimeSpan.Zero)
                    {
                        media.MediaType = TMediaType.Movie;
                        if (Math.Abs(videoDuration.Ticks - audioDuration.Ticks) >= TimeSpan.TicksPerSecond / 2
                            && audioDuration != TimeSpan.Zero)
                            // when more than 0.5 sec difference
                            media.MediaStatus = TMediaStatus.ValidationError;
                        else
                            media.MediaStatus = TMediaStatus.Available;
                    }
                    else
                        media.MediaStatus = TMediaStatus.ValidationError;
                }
                Debug.WriteLine("Check of {0} finished with status {1}. It took {2} milliseconds", media.FullPath, media.MediaStatus, Environment.TickCount - startTickCunt);
            }
            else
                media.MediaStatus = TMediaStatus.Available;
        }

        public static void GetLoudness(this IMedia media, TimeSpan startTime, TimeSpan duration, EventHandler<AudioVolumeMeasuredEventArgs> audioVolumeMeasuredCallback, Action finishCallback)
        {
            FileManager.Queue(new LoudnessOperation() { SourceMedia = media, AudioVolumeMeasured = audioVolumeMeasuredCallback, MeasureStart = startTime, MeasureDuration = duration, FailureCallback = finishCallback, SuccessCallback = finishCallback }, true);
        }

        public static void GetLoudness(this IMedia media)
        {
            FileManager.Queue(new LoudnessOperation() { SourceMedia = media, MeasureStart = media.TCPlay - media.TCStart, MeasureDuration = media.DurationPlay });
        }
    }
}

