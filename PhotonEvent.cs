﻿public enum PhotonEvent : byte
{
    RPC = 200,
    OSR1 = 201,
    OSR = 206,
    Instantiate = 202,
    CloseConnection = 203,
    RemoveInstantiatedObject = 204,
    DestroyPlayerObjects = 207,
    SetMasterClient = 208,
    UpdatePlayersCounters = 226,
    UpdateQueuePosition = 228,
    RoomListUpdate = 229,
    RoomListCreated = 230,
    PlayerJoined = 253,
    PlayerLeft = 254,
    JoinedRoom = 255,
}