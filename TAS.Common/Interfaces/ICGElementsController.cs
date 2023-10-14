﻿using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace TAS.Common.Interfaces
{
    public interface ICGElementsController: ICGElementsState, INotifyPropertyChanged, IGpi
    {
        IEnumerable<ICGElement> Crawls { get; }
        IEnumerable<ICGElement> Logos { get; }
        IEnumerable<ICGElement> Parentals { get; }
        void SetState(ICGElementsState state);
        byte DefaultCrawl { get; }
        byte DefaultLogo { get; }
        bool IsMaster { get; }
        bool IsConnected { get; }
        void Clear();
    }
}
