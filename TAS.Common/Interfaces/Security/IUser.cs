﻿using System.Collections.ObjectModel;
using System.Security.Principal;

namespace TAS.Common.Interfaces.Security
{
    public interface IUser: ISecurityObject, IIdentity
    {
        void GroupAdd(IGroup group);
        bool GroupRemove(IGroup group);
        bool IsAdmin { get; set; }
        AuthenticationSource AuthenticationSource { get; set; }
        string AuthenticationObject { get; set; }
        ReadOnlyCollection<IGroup> GetGroups();
    }
}
