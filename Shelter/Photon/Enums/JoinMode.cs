﻿namespace Photon.Enums
{
    /// <summary>Defines possible values for OpJoinRoom and OpJoinOrCreate. It tells the server if the room can be only be joined normally, created implicitly or found on a web-service for Turnbased games.</summary>
    /// <remarks>These values are not directly used by a game but implicitly set.</remarks>
    public enum JoinMode : byte
    {
        /// <summary>Regular join. The room must exist.</summary>
        Default = 0,

        /// <summary>Join or create the room if it's not existing. Used for OpJoinOrCreate for example.</summary>
        CreateIfNotExists = 1,

        /// <summary>The room might be out of memory and should be loaded (if possible) from a Turnbased web-service.</summary>
        JoinOrRejoin = 2,

        /// <summary>Only re-join will be allowed. If the user is not yet in the room, this will fail.</summary>
        RejoinOnly = 3,
    }
}