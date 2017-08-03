﻿using System;

namespace TAS.Server.Common.Interfaces
{
    /// <summary>
    /// Access control list item
    /// </summary>
    public interface IAclItem
    {
        /// <summary>
        /// Object to which rights are controlled against
        /// </summary>
        IPersistent Owner { get; set; }

        /// <summary>
        /// Object that HAVE rights to Owner
        /// </summary>
        ISecurityObject SecurityObject { get; set; }
        
        /// <summary>
        /// bit map of rights assigned for SecurityObject to Owner
        /// </summary>
        ulong Acl { get; set ;}
    }
}
