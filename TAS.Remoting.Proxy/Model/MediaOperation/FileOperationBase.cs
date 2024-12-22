﻿using System;
using System.Collections.Generic;
using jNet.RPC;
using jNet.RPC.Client;
using TAS.Common;
using TAS.Common.Interfaces;

namespace TAS.Remoting.Model.MediaOperation
{
    public class FileOperationBase : ProxyObjectBase, IFileOperationBase
    {
        #pragma warning disable CS0649 

        [DtoMember(nameof(IFileOperationBase.IsAborted))]
        private bool _isAborted;

        [DtoMember(nameof(IFileOperationBase.FinishedTime))]
        private DateTime _finishedTime;

        [DtoMember(nameof(IFileOperationBase.IsIndeterminate))]
        private bool _isIndeterminate;

        [DtoMember(nameof(IFileOperationBase.OperationOutput))]
        private List<string> _operationOutput;

        [DtoMember(nameof(IFileOperationBase.OperationStatus))]
        private FileOperationStatus _operationStatus;

        [DtoMember(nameof(IFileOperationBase.OperationWarning))]
        private List<string> _operationWarning;

        [DtoMember(nameof(IFileOperationBase.Progress))]
        private int _progress;

        [DtoMember(nameof(IFileOperationBase.ScheduledTime))]
        private DateTime _scheduledTime;

        [DtoMember(nameof(IFileOperationBase.StartTime))]
        private DateTime _startTime;

        [DtoMember(nameof(IFileOperationBase.TryCount))]
        private int _tryCount;

        #pragma warning restore

        private event EventHandler _finished;

        public bool IsAborted => _isAborted;


        public DateTime FinishedTime => _finishedTime;

        public bool IsIndeterminate => _isIndeterminate;

        public List<string> OperationOutput => _operationOutput;

        public FileOperationStatus OperationStatus => _operationStatus;

        public List<string> OperationWarning => _operationWarning;

        public int Progress => _progress;

        public DateTime ScheduledTime => _scheduledTime;

        public DateTime StartTime => _startTime;

        public int TryCount => _tryCount;

        public event EventHandler Finished
        {
            add
            {
                EventAdd(_finished);
                _finished += value;
            }
            remove
            {
                _finished -= value;
                EventRemove(_finished);
            }
        }

        public void Abort()
        {
            Invoke();
        }

        protected override void OnEventNotification(string eventName, EventArgs eventArgs)
        {
            if (eventName == nameof(Finished))
            {
                _finished?.Invoke(this, eventArgs);
            }
        }
    }
}
