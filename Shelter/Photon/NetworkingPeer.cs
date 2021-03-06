using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ExitGames.Client.Photon;
using ExitGames.Client.Photon.Lite;
using Mod;
using Mod.Exceptions;
using Photon.Enums;
using RC;
using UnityEngine;
using Component = UnityEngine.Component;
using Debug = UnityEngine.Debug;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using LogType = Mod.Logging.LogType;

namespace Photon
{
    internal class NetworkingPeer : LoadbalancingPeer, IPhotonPeerListener
    {
        private HashSet<int> allowedReceivingGroups;
        private HashSet<int> blockSendingGroups;
        protected internal short currentLevelPrefix;
        protected internal const string CurrentSceneProperty = "curScn";
        private bool didAuthenticate;
        private IPhotonPeerListener externalListener;
        private string[] friendListRequested;
        private int friendListTimestamp;
        public bool hasSwitchedMC;
        public bool insideLobby;
        public Dictionary<int, GameObject> instantiatedObjects;
        private bool isFetchingFriends;
        public bool IsInitialConnect;
        protected internal bool loadingLevelAndPausedNetwork;
        public Dictionary<int, Player> mActors;
        protected internal string mAppId;
        protected internal string mAppVersion;
        public Dictionary<string, Room> mGameList;
        private JoinType mLastJoinType;
        public Player mMasterClient;
        private Dictionary<Type, List<MethodInfo>> monoRPCMethodsCache;
        public Player[] mOtherPlayerListCopy;
        public Player[] mPlayerListCopy;
        private bool mPlayernameHasToBeUpdated;
        public string NameServerAddress;
        protected internal Dictionary<int, PhotonView> photonViewList;
        private string playername;
        public static Dictionary<string, GameObject> PrefabCache = new Dictionary<string, GameObject>();
        private static readonly Dictionary<ConnectionProtocol, int> ProtocolToNameServerPort;
        public bool requestSecurity;
        private readonly Dictionary<string, int> rpcShortcuts;
        private Dictionary<int, object[]> tempInstantiationData;
        public static bool UsePrefabCache = true;

        static NetworkingPeer()
        {
            Dictionary<ConnectionProtocol, int> dictionary = new Dictionary<ConnectionProtocol, int>
            {
                { ConnectionProtocol.Udp, 5058 }, //5058
                { ConnectionProtocol.Tcp, 4533 } // 4533
            };
            ProtocolToNameServerPort = dictionary;
        }

        public NetworkingPeer(IPhotonPeerListener listener, string playername, ConnectionProtocol connectionProtocol) : base(listener, connectionProtocol)
        {
            this.playername = string.Empty;
            this.mActors = new Dictionary<int, Player>();
            this.mOtherPlayerListCopy = new Player[0];
            this.mPlayerListCopy = new Player[0];
            this.requestSecurity = true;
            this.monoRPCMethodsCache = new Dictionary<Type, List<MethodInfo>>();
            mGameList = new Dictionary<string, Room>();
            Room.List = new List<Room>();
            this.instantiatedObjects = new Dictionary<int, GameObject>();
            this.allowedReceivingGroups = new HashSet<int>();
            this.blockSendingGroups = new HashSet<int>();
            this.photonViewList = new Dictionary<int, PhotonView>();
            this.NameServerAddress = "ns.exitgamescloud.com";
            this.tempInstantiationData = new Dictionary<int, object[]>();
            if (PhotonHandler.PingImplementation == null)
            {
                PhotonHandler.PingImplementation = typeof(PingMono);
            }
            Listener = this;
            this.lobby = TypedLobby.Default;
            LimitOfUnreliableCommands = 40;
            this.externalListener = listener;
            this.PlayerName = playername;
            this.mLocalActor = new Player(true, -1, this.playername);
            this.AddNewPlayer(this.mLocalActor.ID, this.mLocalActor);
            this.rpcShortcuts = new Dictionary<string, int>(PhotonNetwork.PhotonServerSettings.RpcList.Count);
            for (int i = 0; i < PhotonNetwork.PhotonServerSettings.RpcList.Count; i++)
            {
                string str = PhotonNetwork.PhotonServerSettings.RpcList[i];
                this.rpcShortcuts[str] = i;
            }
            this.states = ClientState.PeerCreated;
        }

        private void AddNewPlayer(int ID, Player player)
        {
            if (!this.mActors.ContainsKey(ID))
            {
                this.mActors[ID] = player;
                this.RebuildPlayerListCopies();
            }
            else
            {
                Debug.LogError("Adding player twice: " + ID);
            }
        }

        private bool AlmostEquals(object[] lastData, object[] currentContent)
        {
            if (lastData != null || currentContent != null)
            {
                if (lastData == null || currentContent == null || lastData.Length != currentContent.Length)
                {
                    return false;
                }
                for (int i = 0; i < currentContent.Length; i++)
                {
                    object one = currentContent[i];
                    object two = lastData[i];
                    if (!ObjectIsSameWithInprecision(one, two))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public void ChangeLocalID(int newID)
        {
            if (this.mLocalActor == null)
            {
                Debug.LogWarning(string.Format("Local actor is null or not in mActors! mLocalActor: {0} mActors==null: {1} newID: {2}", this.mLocalActor, this.mActors == null, newID));
            }
            if (this.mActors.ContainsKey(this.mLocalActor.ID))
            {
                this.mActors.Remove(this.mLocalActor.ID);
            }
            this.mLocalActor.InternalChangeLocalID(newID);
            this.mActors[this.mLocalActor.ID] = this.mLocalActor;
            this.RebuildPlayerListCopies();
        }

        public void checkLAN()
        {
            if (GameManager.OnPrivateServer && this.MasterServerAddress != null && this.MasterServerAddress != string.Empty && this.mGameserver != null && this.mGameserver != string.Empty && this.MasterServerAddress.Contains(":") && this.mGameserver.Contains(":"))
            {
                this.mGameserver = this.MasterServerAddress.Split(':')[0] + ":" + this.mGameserver.Split(':')[1];
            }
        }

        private void CheckMasterClient(int leavingPlayerId)
        {
            bool flag2;
            bool flag = this.mMasterClient != null && this.mMasterClient.ID == leavingPlayerId;
            if (!(flag2 = leavingPlayerId > 0) || flag)
            {
                if (this.mActors.Count <= 1)
                {
                    this.mMasterClient = this.mLocalActor;
                }
                else
                {
                    int num = 2147483647;
                    foreach (int num2 in this.mActors.Keys)
                    {
                        if (num2 < num && num2 != leavingPlayerId)
                        {
                            num = num2;
                        }
                    }
                    this.mMasterClient = this.mActors[num];
                }
                if (flag2)
                {
                    SendMonoMessage(PhotonNetworkingMessage.OnMasterClientSwitched, this.mMasterClient);
                }
            }
        }

        private bool CheckTypeMatch(ParameterInfo[] methodParameters, Type[] callParameterTypes)
        {
            if (methodParameters.Length < callParameterTypes.Length)
            {
                return false;
            }
            for (int i = 0; i < callParameterTypes.Length; i++)
            {
                Type parameterType = methodParameters[i].ParameterType;
                if (callParameterTypes[i] != null && parameterType != callParameterTypes[i])
                {
                    return false;
                }
            }
            return true;
        }

        public void CleanRpcBufferIfMine(PhotonView view)
        {
            if (view.ownerId != this.mLocalActor.ID && !this.mLocalActor.IsMasterClient)
            {
                Debug.LogError(string.Concat("Cannot remove cached RPCs on a PhotonView thats not ours! ", view.owner, " scene: ", view.isSceneView));
            }
            else
            {
                this.OpCleanRpcBuffer(view);
            }
        }

        public bool Connect(string serverAddress, ServerConnection type)
        {
            bool flag;
            if (PhotonHandler.AppQuits)
            {
                Debug.LogWarning("Ignoring Connect() because app gets closed. If this is an error, check PhotonHandler.AppQuits.");
                return false;
            }
            if (PhotonNetwork.connectionStatesDetailed == ClientState.Disconnecting)
            {
                Debug.LogError("Connect() failed. Can't connect while disconnecting (still). Current state: " + PhotonNetwork.connectionStatesDetailed);
                return false;
            }
            if (flag = base.Connect(serverAddress, string.Empty))
            {
                switch (type)
                {
                    case ServerConnection.MasterServer:
                        this.states = ClientState.ConnectingToMasterserver;
                        return flag;

                    case ServerConnection.GameServer:
                        this.states = ClientState.ConnectingToGameserver;
                        return flag;

                    case ServerConnection.NameServer:
                        this.states = ClientState.ConnectingToNameServer;
                        return flag;
                }
            }
            return flag;
        }

        public override bool Connect(string serverAddress, string applicationName)
        {
            Debug.LogError("Avoid using this directly. Thanks.");
            return false;
        }

        public bool ConnectToNameServer()
        {
            if (PhotonHandler.AppQuits)
            {
                Debug.LogWarning("Ignoring Connect() because app gets closed. If this is an error, check PhotonHandler.AppQuits.");
                return false;
            }
            this.IsUsingNameServer = true;
            this.CloudRegion = CloudRegionCode.None;
            if (this.states != ClientState.ConnectedToNameServer)
            {
                string nameServerAddress = this.NameServerAddress;
                if (!nameServerAddress.Contains(":"))
                {
                    int num = 0;
                    ProtocolToNameServerPort.TryGetValue(UsedProtocol, out num);
                    nameServerAddress = string.Format("{0}:{1}", nameServerAddress, num);
                    Debug.Log(string.Concat("Server to connect to: ", nameServerAddress, " settings protocol: ", PhotonNetwork.PhotonServerSettings.Protocol));
                }
                if (!base.Connect(nameServerAddress, "ns"))
                {
                    return false;
                }
                this.states = ClientState.ConnectingToNameServer;
            }
            return true;
        }

        public bool ConnectToRegionMaster(CloudRegionCode region)
        {
            if (PhotonHandler.AppQuits)
            {
                Debug.LogWarning("Ignoring Connect() because app gets closed. If this is an error, check PhotonHandler.AppQuits.");
                return false;
            }
            this.IsUsingNameServer = true;
            this.CloudRegion = region;
            if (this.states == ClientState.ConnectedToNameServer)
            {
                return this.OpAuthenticate(this.mAppId, this.mAppVersionPun, this.PlayerName, this.CustomAuthenticationValues, region.ToString());
            }
            string nameServerAddress = this.NameServerAddress;
            if (!nameServerAddress.Contains(":"))
            {
                int num = 0;
                ProtocolToNameServerPort.TryGetValue(UsedProtocol, out num);
                nameServerAddress = string.Format("{0}:{1}", nameServerAddress, num);
            }
            if (!base.Connect(nameServerAddress, "ns"))
            {
                return false;
            }
            this.states = ClientState.ConnectingToNameServer;
            return true;
        }

        public void DebugReturn(DebugLevel level, string message)
        {
            this.externalListener.DebugReturn(level, message);
        }

        private bool DeltaCompressionRead(PhotonView view, Hashtable data)
        {
            if (!data.ContainsKey((byte) 1))
            {
                if (view.lastOnSerializeDataReceived == null)
                {
                    return false;
                }
                object[] objArray = data[(byte) 2] as object[];
                if (objArray == null)
                {
                    return false;
                }
                int[] target = data[(byte) 3] as int[];
                if (target == null)
                {
                    target = new int[0];
                }
                object[] lastOnSerializeDataReceived = view.lastOnSerializeDataReceived;
                for (int i = 0; i < objArray.Length; i++)
                {
                    if (objArray[i] == null && !target.Contains(i))
                    {
                        objArray[i] = lastOnSerializeDataReceived[i];
                    }
                }
                data[(byte) 1] = objArray;
            }
            return true;
        }

        private bool DeltaCompressionWrite(PhotonView view, Hashtable data)
        {
            if (view.lastOnSerializeDataSent != null)
            {
                object[] lastOnSerializeDataSent = view.lastOnSerializeDataSent;
                object[] objArray2 = data[(byte) 1] as object[];
                if (objArray2 == null)
                {
                    return false;
                }
                if (lastOnSerializeDataSent.Length != objArray2.Length)
                {
                    return true;
                }
                object[] objArray3 = new object[objArray2.Length];
                int num = 0;
                List<int> list = new List<int>();
                for (int i = 0; i < objArray3.Length; i++)
                {
                    object one = objArray2[i];
                    object two = lastOnSerializeDataSent[i];
                    if (ObjectIsSameWithInprecision(one, two))
                    {
                        num++;
                    }
                    else
                    {
                        objArray3[i] = objArray2[i];
                        if (one == null)
                        {
                            list.Add(i);
                        }
                    }
                }
                if (num > 0)
                {
                    data.Remove((byte) 1);
                    if (num == objArray2.Length)
                    {
                        return false;
                    }
                    data[(byte) 2] = objArray3;
                    if (list.Count > 0)
                    {
                        data[(byte) 3] = list.ToArray();
                    }
                }
            }
            return true;
        }

        public void DestroyAll(bool localOnly)
        {
            if (!localOnly)
            {
                this.OpRemoveCompleteCache();
                this.SendDestroyOfAll();
            }
            this.LocalCleanupAnythingInstantiated(true);
        }

        public void DestroyPlayerObjects(int playerId, bool localOnly)
        {
            if (playerId <= 0)
            {
                Debug.LogError("Failed to Destroy objects of playerId: " + playerId);
            }
            else
            {
                if (!localOnly)
                {
                    this.OpRemoveFromServerInstantiationsOfPlayer(playerId);
                    this.OpCleanRpcBuffer(playerId);
                    this.SendDestroyOfPlayer(playerId);
                }
                Queue<GameObject> queue = new Queue<GameObject>();
                int num = playerId * PhotonNetwork.MaxViewIds;
                int num2 = num + PhotonNetwork.MaxViewIds;
                foreach (KeyValuePair<int, GameObject> pair in this.instantiatedObjects)
                {
                    if (pair.Key > num && pair.Key < num2)
                    {
                        queue.Enqueue(pair.Value);
                    }
                }
                foreach (GameObject obj2 in queue)
                {
                    this.RemoveInstantiatedGO(obj2, true);
                }
            }
        }

        public override void Disconnect()
        {
            if (PeerState == PeerStateValue.Disconnected)
            {
                if (!PhotonHandler.AppQuits)
                {
                    Debug.LogWarning(string.Format("Can't execute Disconnect() while not connected. Nothing changed. State: {0}", this.states));
                }
            }
            else
            {
                this.states = ClientState.Disconnecting;
                base.Disconnect();
            }
        }

        private void DisconnectToReconnect()
        {
            switch (this.server)
            {
                case ServerConnection.MasterServer:
                    this.states = ClientState.DisconnectingFromMasterserver;
                    base.Disconnect();
                    break;

                case ServerConnection.GameServer:
                    this.states = ClientState.DisconnectingFromGameserver;
                    base.Disconnect();
                    break;

                case ServerConnection.NameServer:
                    this.states = ClientState.DisconnectingFromNameServer;
                    base.Disconnect();
                    break;
            }
        }

        private void DisconnectToReconnect2()
        {
            this.checkLAN();
            switch (this.server)
            {
                case ServerConnection.MasterServer:
                    this.states = GameManager.returnPeerState(2);
                    base.Disconnect();
                    break;

                case ServerConnection.GameServer:
                    this.states = GameManager.returnPeerState(3);
                    base.Disconnect();
                    break;

                case ServerConnection.NameServer:
                    this.states = GameManager.returnPeerState(4);
                    base.Disconnect();
                    break;
            }
        }

        internal GameObject DoInstantiate(Hashtable evData, Player player, GameObject resourceGameObject)
        {
            Vector3 zero;
            int[] numArray;
            object[] objArray;
            string key = (string) evData[(byte) 0];
            int timestamp = (int) evData[(byte) 6];
            int instantiationId = (int) evData[(byte) 7];
            if (evData.ContainsKey((byte) 1))
            {
                zero = (Vector3) evData[(byte) 1];
            }
            else
            {
                zero = Vector3.zero;
            }
            Quaternion identity = Quaternion.identity;
            if (evData.ContainsKey((byte) 2))
            {
                identity = (Quaternion) evData[(byte) 2];
            }
            int item = 0;
            if (evData.ContainsKey((byte) 3))
            {
                item = (int) evData[(byte) 3];
            }
            short num4 = 0;
            if (evData.ContainsKey((byte) 8))
            {
                num4 = (short) evData[(byte) 8];
            }
            if (evData.ContainsKey((byte) 4))
            {
                numArray = (int[]) evData[(byte) 4];
            }
            else
            {
                numArray = new int[] { instantiationId };
            }
            if (!InstantiateTracker.instance.checkObj(key, player, numArray))
            {
                return null;
            }
            if (evData.ContainsKey((byte) 5))
            {
                objArray = (object[]) evData[(byte) 5];
            }
            else
            {
                objArray = null;
            }
            if (!(item == 0 || this.allowedReceivingGroups.Contains(item)))
            {
                return null;
            }
            if (resourceGameObject == null)
            {
                if (!UsePrefabCache || !PrefabCache.TryGetValue(key, out resourceGameObject))
                {
                    if (key.StartsWith("RCAsset/"))
                    {
                        resourceGameObject = GameManager.InstantiateCustomAsset(key);
                    }
                    else
                    {
                        resourceGameObject = (GameObject) Resources.Load(key, typeof(GameObject));
                    }
                    if (UsePrefabCache)
                    {
                        PrefabCache.Add(key, resourceGameObject);
                    }
                }
                if (resourceGameObject == null)
                {
                    Debug.LogError("PhotonNetwork error: Could not Instantiate the prefab [" + key + "]. Please verify you have this gameobject in a Resources folder.");
                    return null;
                }
            }
            PhotonView[] photonViewsInChildren = resourceGameObject.GetPhotonViewsInChildren();
            if (photonViewsInChildren.Length != numArray.Length)
            {
                throw new Exception("Error in Instantiation! The resource's PhotonView count is not the same as in incoming data.");
            }
            for (int i = 0; i < numArray.Length; i++)
            {
                photonViewsInChildren[i].viewID = numArray[i];
                photonViewsInChildren[i].prefix = num4;
                photonViewsInChildren[i].instantiationId = instantiationId;
            }
            this.StoreInstantiationData(instantiationId, objArray);
            GameObject obj3 = (GameObject) UnityEngine.Object.Instantiate(resourceGameObject, zero, identity);
            for (int j = 0; j < numArray.Length; j++)
            {
                photonViewsInChildren[j].viewID = 0;
                photonViewsInChildren[j].prefix = -1;
                photonViewsInChildren[j].prefixBackup = -1;
                photonViewsInChildren[j].instantiationId = -1;
            }
            this.RemoveInstantiationData(instantiationId);
            if (this.instantiatedObjects.ContainsKey(instantiationId))
            {
                GameObject go = this.instantiatedObjects[instantiationId];
                string str2 = string.Empty;
                if (go != null)
                {
                    foreach (PhotonView view in go.GetPhotonViewsInChildren())
                    {
                        if (view != null)
                        {
                            str2 = str2 + view + ", ";
                        }
                    }
                }
                object[] args = new object[] { obj3, instantiationId, this.instantiatedObjects.Count, go, str2, PhotonNetwork.lastUsedViewSubId, PhotonNetwork.lastUsedViewSubIdStatic, this.photonViewList.Count };
                Debug.LogError(string.Format("DoInstantiate re-defines a GameObject. Destroying old entry! New: '{0}' (instantiationID: {1}) Old: {3}. PhotonViews on old: {4}. instantiatedObjects.Count: {2}. PhotonNetwork.lastUsedViewSubId: {5} PhotonNetwork.lastUsedViewSubIdStatic: {6} this.photonViewList.Count {7}.)", args));
                this.RemoveInstantiatedGO(go, true);
            }
            this.instantiatedObjects.Add(instantiationId, obj3);
            obj3.SendMessage(PhotonNetworkingMessage.OnPhotonInstantiate.ToString(), new PhotonMessageInfo(player, timestamp, null), SendMessageOptions.DontRequireReceiver);
            return obj3;
        }

        public void ExecuteRPC(Hashtable rpcData, Player sender)
        {
            if (rpcData != null && rpcData.ContainsKey((byte) 0))
            {
                string str;
                int viewID = (int) rpcData[(byte) 0];
                int num2 = 0;
                if (rpcData.ContainsKey((byte) 1))
                {
                    num2 = (short) rpcData[(byte) 1];
                }
                if (rpcData.ContainsKey((byte) 5))
                {
                    int num3 = (byte) rpcData[(byte) 5];
                    if (num3 > PhotonNetwork.PhotonServerSettings.RpcList.Count - 1)
                    {
                        Debug.LogError("Could not find RPC with index: " + num3 + ". Going to ignore! Check PhotonServerSettings.RpcList");
                        return;
                    }
                    str = PhotonNetwork.PhotonServerSettings.RpcList[num3];
                }
                else
                {
                    str = (string) rpcData[(byte) 3];
                }
                object[] parameters = null;
                if (rpcData.ContainsKey((byte) 4))
                {
                    parameters = (object[]) rpcData[(byte) 4];
                }
                if (parameters == null)
                {
                    parameters = new object[0];
                }
                PhotonView photonView = this.GetPhotonView(viewID);
                if (photonView == null)
                {
                    int num4 = viewID / PhotonNetwork.MaxViewIds;
                    bool flag = num4 == this.mLocalActor.ID;
                    bool flag2 = num4 == sender.ID;
                    if (flag)
                    {
                        Debug.LogWarning(string.Concat("Received RPC \"", str, "\" for viewID ", viewID, " but this PhotonView does not exist! View was/is ours.", !flag2 ? " Remote called." : " Owner called."));
                    }
                    else
                    {
                        Debug.LogError(string.Concat("Received RPC \"", str, "\" for viewID ", viewID, " but this PhotonView does not exist! Was remote PV.", !flag2 ? " Remote called." : " Owner called."));
                    }
                }
                else if (photonView.prefix != num2)
                {
                    Debug.LogError(string.Concat("Received RPC \"", str, "\" on viewID ", viewID, " with a prefix of ", num2, ", our prefix is ", photonView.prefix, ". The RPC has been ignored."));
                }
                else if (str == string.Empty)
                {
                    Debug.LogError("Malformed RPC; this should never occur. Content: " + SupportClass.DictionaryToString(rpcData));
                }
                else
                {
                    if (PhotonNetwork.LogLevel >= PhotonLogLevel.Full)
                    {
                        Debug.Log("Received RPC: " + str);
                    }
                    if (photonView.group == 0 || this.allowedReceivingGroups.Contains(photonView.group))
                    {
                        Type[] callParameterTypes = new Type[0];
                        if (parameters.Length > 0)
                        {
                            callParameterTypes = new Type[parameters.Length];
                            int index = 0;
                            foreach (object obj2 in parameters)
                            {
                                if (obj2 == null)
                                {
                                    callParameterTypes[index] = null;
                                }
                                else
                                {
                                    callParameterTypes[index] = obj2.GetType();
                                }
                                index++;
                            }
                        }
                        int num7 = 0;
                        int num8 = 0;
                        foreach (UnityEngine.MonoBehaviour behaviour in photonView.GetComponents<UnityEngine.MonoBehaviour>())
                        {
                            if (behaviour == null)
                            {
                                Debug.LogError("ERROR You have missing MonoBehaviours on your gameobjects!");
                            }
                            else
                            {
                                Type key = behaviour.GetType();
                                List<MethodInfo> list = null;
                                if (this.monoRPCMethodsCache.ContainsKey(key))
                                {
                                    list = this.monoRPCMethodsCache[key];
                                }
                                if (list == null)
                                {
                                    List<MethodInfo> methods = SupportClass.GetMethods(key, typeof(RPC));
                                    this.monoRPCMethodsCache[key] = methods;
                                    list = methods;
                                }
                                if (list != null)
                                {
                                    foreach (MethodInfo info in list)
                                    {
                                        StringBuilder builder = null;
                                        if (str != null)
                                        {
                                            builder = new StringBuilder(str);
                                            if (builder.Length > 0)
                                            {
                                                builder.Insert(0, builder[0].ToString().ToUpper());
                                                builder.Remove(1, 1);
                                            }
                                        }


                                        if (info.Name == builder?.ToString())
                                        {
                                            num8++;
                                            ParameterInfo[] methodParameters = info.GetParameters();
                                            if (methodParameters.Length == callParameterTypes.Length)
                                            {
                                                if (this.CheckTypeMatch(methodParameters, callParameterTypes))
                                                {
                                                    num7++;
                                                    object obj3 = info.Invoke(behaviour, parameters);
                                                    if (info.ReturnType == typeof(IEnumerator))
                                                    {
                                                        behaviour.StartCoroutine((IEnumerator) obj3);
                                                    }
                                                }
                                            }
                                            else if (methodParameters.Length - 1 == callParameterTypes.Length)
                                            {
                                                if (this.CheckTypeMatch(methodParameters, callParameterTypes) && methodParameters[methodParameters.Length - 1].ParameterType == typeof(PhotonMessageInfo))
                                                {
                                                    num7++;
                                                    int timestamp = (int) rpcData[(byte) 2];
                                                    object[] array = new object[parameters.Length + 1];
                                                    parameters.CopyTo(array, 0);
                                                    array[array.Length - 1] = new PhotonMessageInfo(sender, timestamp, photonView);
                                                    object obj4 = info.Invoke(behaviour, array);
                                                    if (info.ReturnType == typeof(IEnumerator))
                                                    {
                                                        behaviour.StartCoroutine((IEnumerator) obj4);
                                                    }
                                                }
                                            }
                                            else if (methodParameters.Length == 1 && methodParameters[0].ParameterType.IsArray)
                                            {
                                                num7++;
                                                object[] objArray5 = new object[] { parameters };
                                                object obj5 = info.Invoke(behaviour, objArray5);
                                                if (info.ReturnType == typeof(IEnumerator))
                                                {
                                                    behaviour.StartCoroutine((IEnumerator) obj5);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        if (num7 != 1)
                        {
                            string str2 = string.Empty;
                            for (int k = 0; k < callParameterTypes.Length; k++)
                            {
                                Type type2 = callParameterTypes[k];
                                if (str2 != string.Empty)
                                {
                                    str2 = str2 + ", ";
                                }
                                if (type2 == null)
                                {
                                    str2 = str2 + "null";
                                }
                                else
                                {
                                    str2 = str2 + type2.Name;
                                }
                            }
                            if (num7 == 0)
                            {
                                if (num8 == 0)
                                {
                                    Debug.LogError(string.Concat("PhotonView with ID ", viewID, " has no method \"", str, "\" marked with the [RPC](C#) or @RPC(JS) property! Args: ", str2));
                                }
                                else
                                {
                                    Debug.LogError(string.Concat("PhotonView with ID ", viewID, " has no method \"", str, "\" that takes ", callParameterTypes.Length, " argument(s): ", str2));
                                }
                            }
                            else
                            {
                                Debug.LogError(string.Concat("PhotonView with ID ", viewID, " has ", num7, " methods \"", str, "\" that takes ", callParameterTypes.Length, " argument(s): ", str2, ". Should be just one?"));
                            }
                        }
                    }
                }
            }
            else
            {
                Debug.LogError("Malformed RPC; this should never occur. Content: " + SupportClass.DictionaryToString(rpcData));
            }
        }

        public object[] FetchInstantiationData(int instantiationId)
        {
            object[] objArray = null;
            if (instantiationId == 0)
            {
                return null;
            }
            this.tempInstantiationData.TryGetValue(instantiationId, out objArray);
            return objArray;
        }

        private void GameEnteredOnGameServer(OperationResponse operationResponse)
        {
            if (operationResponse.ReturnCode == ErrorCode.Ok)
            {
                this.states = ClientState.Joined;
                this.mRoomToGetInto.IsLocalClientInside = true;
                Hashtable pActorProperties = (Hashtable) operationResponse[249];
                Hashtable gameProperties = (Hashtable) operationResponse[248];
                this.ReadoutProperties(gameProperties, pActorProperties, 0);
                int newID = (int) operationResponse[254];
                this.ChangeLocalID(newID);
                this.CheckMasterClient(-1);
                if (this.mPlayernameHasToBeUpdated)
                    this.SendPlayerName();
            
                if (operationResponse.OperationCode == OperationCode.CreateGame) 
                    SendMonoMessage(PhotonNetworkingMessage.OnCreatedRoom);
            }
            else
            {
                switch (operationResponse.OperationCode)
                {
                    case OperationCode.JoinRandomGame:
                    {
                        if (PhotonNetwork.LogLevel >= PhotonLogLevel.Informational)
                        {
                            Debug.Log("Join failed on GameServer. Changing back to MasterServer. Msg: " + operationResponse.DebugMessage);
                            if (operationResponse.ReturnCode == ErrorCode.GameDoesNotExist)
                                Debug.Log("Most likely the game became empty during the switch to GameServer.");
                        }
                        SendMonoMessage(PhotonNetworkingMessage.OnPhotonRandomJoinFailed, operationResponse.ReturnCode, operationResponse.DebugMessage);
                        break;
                    }
                    case OperationCode.JoinGame:
                    {
                        if (PhotonNetwork.LogLevel >= PhotonLogLevel.Informational)
                        {
                            Debug.Log("Join failed on GameServer. Changing back to MasterServer. Msg: " + operationResponse.DebugMessage);
                            if (operationResponse.ReturnCode == ErrorCode.GameDoesNotExist)
                                Debug.Log("Most likely the game became empty during the switch to GameServer.");
                        }
                        SendMonoMessage(PhotonNetworkingMessage.OnPhotonJoinRoomFailed, operationResponse.ReturnCode, operationResponse.DebugMessage);
                        break;
                    }
                    case OperationCode.CreateGame:
                    {
                        if (PhotonNetwork.LogLevel >= PhotonLogLevel.Informational)
                            Debug.Log("Create failed on GameServer. Changing back to MasterServer. Msg: " + operationResponse.DebugMessage);
                        SendMonoMessage(PhotonNetworkingMessage.OnPhotonCreateRoomFailed, operationResponse.ReturnCode, operationResponse.DebugMessage);
                        break;
                    }
                }
                this.DisconnectToReconnect2();
            }
        }

        private Hashtable GetActorPropertiesForActorNr(Hashtable actorProperties, int actorNr)
        {
            if (actorProperties.ContainsKey(actorNr))
                return (Hashtable) actorProperties[actorNr];
        
            return actorProperties;
        }

        public int GetInstantiatedObjectsId(GameObject go)
        {
            int num = -1;
            if (go == null)
            {
                Debug.LogError("GetInstantiatedObjectsId() for GO == null.");
                return num;
            }
            PhotonView[] photonViewsInChildren = go.GetPhotonViewsInChildren();
            if (photonViewsInChildren != null && photonViewsInChildren.Length > 0 && photonViewsInChildren[0] != null)
            {
                return photonViewsInChildren[0].instantiationId;
            }
            if (PhotonNetwork.LogLevel >= PhotonLogLevel.Informational)
            {
                Debug.Log("GetInstantiatedObjectsId failed for GO: " + go);
            }
            return num;
        }

        private Hashtable GetLocalActorProperties()
        {
            if (Player.Self != null)
                return Player.Self.AllProperties;
        
            return new Hashtable
            {
                [ActorProperties.PlayerName] = this.PlayerName
            };
        }

        protected internal static bool GetMethod(UnityEngine.MonoBehaviour monob, string methodType, out MethodInfo mi)
        {
            mi = null;
            if (monob != null && !string.IsNullOrEmpty(methodType))
            {
                List<MethodInfo> methods = SupportClass.GetMethods(monob.GetType(), null);
                for (int i = 0; i < methods.Count; i++)
                {
                    MethodInfo info = methods[i];
                    if (info.Name.Equals(methodType))
                    {
                        mi = info;
                        return true;
                    }
                }
            }
            return false;
        }

        public PhotonView GetPhotonView(int viewID)
        {
            PhotonView view = null;
            this.photonViewList.TryGetValue(viewID, out view);
            if (view == null)
            {
                PhotonView[] viewArray = UnityEngine.Object.FindObjectsOfType(typeof(PhotonView)) as PhotonView[];
                foreach (PhotonView view2 in viewArray)
                {
                    if (view2.viewID == viewID)
                    {
                        if (view2.didAwake)
                        {
                            Debug.LogWarning("Had to lookup view that wasn't in dict: " + view2);
                        }
                        return view2;
                    }
                }
            }
            return view;
        }

        private Player GetPlayerWithID(int number)
        {
            if (this.mActors != null && this.mActors.ContainsKey(number))
            {
                return this.mActors[number];
            }
            return null;
        }

        public bool GetRegions()
        {
            bool flag;
            if (this.server != ServerConnection.NameServer)
            {
                return false;
            }
            if (flag = this.OpGetRegions(this.mAppId))
            {
                this.AvailableRegions = null;
            }
            return flag;
        }

        private void HandleEventLeave(int actorID)
        {
            if (PhotonNetwork.LogLevel >= PhotonLogLevel.Informational)
            {
                Debug.Log("HandleEventLeave for player ID: " + actorID);
            }
            if (actorID >= 0 && this.mActors.ContainsKey(actorID))
            {
                Player playerWithID = this.GetPlayerWithID(actorID);
                if (playerWithID == null)
                {
                    Debug.LogError("HandleEventLeave for player ID: " + actorID + " has no PhotonPlayer!");
                }
                this.CheckMasterClient(actorID);
                if (this.mCurrentGame != null && this.mCurrentGame.DoAutoCleanup)
                    this.DestroyPlayerObjects(actorID, true);
            
                this.RemovePlayer(actorID, playerWithID);
                SendMonoMessage(PhotonNetworkingMessage.OnPhotonPlayerDisconnected, playerWithID);
            }
            else
            {
                Debug.LogError($"Received event Leave for unknown player ID: {actorID}");
            }
        }

        private void LeftLobbyCleanup()
        {
            mGameList = new Dictionary<string, Room>();
            Room.List = new List<Room>();
            if (this.insideLobby)
            {
                this.insideLobby = false;
                SendMonoMessage(PhotonNetworkingMessage.OnLeftLobby);
            }
        }

        private void LeftRoomCleanup()
        {
            bool flag = this.mRoomToGetInto != null;
            bool flag2 = mRoomToGetInto?.DoAutoCleanup ?? PhotonNetwork.CleanupPlayerObjects;
            this.hasSwitchedMC = false;
            this.mRoomToGetInto = null;
            this.mActors = new Dictionary<int, Player>();
            this.mPlayerListCopy = new Player[0];
            this.mOtherPlayerListCopy = new Player[0];
            this.mMasterClient = null;
            this.allowedReceivingGroups = new HashSet<int>();
            this.blockSendingGroups = new HashSet<int>();
            mGameList = new Dictionary<string, Room>();
            Room.List = new List<Room>();
            this.isFetchingFriends = false;
            this.ChangeLocalID(-1);
            if (flag2)
            {
                this.LocalCleanupAnythingInstantiated(true);
                PhotonNetwork.manuallyAllocatedViewIds = new List<int>();
            }
            if (flag)
            {
                SendMonoMessage(PhotonNetworkingMessage.OnLeftRoom);
            }
        }

        protected internal void LoadLevelIfSynced()
        {
            if (PhotonNetwork.automaticallySyncScene && !PhotonNetwork.isMasterClient && PhotonNetwork.Room != null && PhotonNetwork.Room.Properties.ContainsKey("curScn"))
            {
                object currentLevel = PhotonNetwork.Room.Properties["curScn"];
                switch (currentLevel)
                {
                    case int lvl when Application.loadedLevel != lvl:
                        PhotonNetwork.LoadLevel(lvl);
                        break;
                    case string lvl when Application.loadedLevelName != lvl:
                        PhotonNetwork.LoadLevel(lvl);
                        break;
                }
            }
        }

        public void LocalCleanPhotonView(PhotonView view)
        {
            view.destroyedByPhotonNetworkOrQuit = true;
            this.photonViewList.Remove(view.viewID);
        }    

        protected internal void LocalCleanupAnythingInstantiated(bool destroyInstantiatedGameObjects)
        {
            if (this.tempInstantiationData.Count > 0)
            {
                Debug.LogWarning("It seems some instantiation is not completed, as instantiation data is used. You should make sure instantiations are paused when calling this method. Cleaning now, despite this.");
            }
            if (destroyInstantiatedGameObjects)
            {
                HashSet<GameObject> set = new HashSet<GameObject>(this.instantiatedObjects.Values);
                foreach (GameObject obj2 in set)
                {
                    this.RemoveInstantiatedGO(obj2, true);
                }
            }
            this.tempInstantiationData.Clear();
            this.instantiatedObjects = new Dictionary<int, GameObject>();
            PhotonNetwork.lastUsedViewSubId = 0;
            PhotonNetwork.lastUsedViewSubIdStatic = 0;
        }

        public void NewSceneLoaded()
        {
            if (this.loadingLevelAndPausedNetwork)
            {
                this.loadingLevelAndPausedNetwork = false;
                PhotonNetwork.isMessageQueueRunning = true;
            }

            var counter = 0;
            foreach (var entry in photonViewList.ToList())
            {
                if (entry.Value == null) // Can't we clear non-nulls too?
                {
                    photonViewList.Remove(entry.Key);
                    counter++;
                }
            }
            if (counter > 0 && PhotonNetwork.LogLevel >= PhotonLogLevel.Informational)
            {
                Debug.Log("New level loaded. Removed " + counter + " scene view IDs from last level.");
            }
        }

        private static bool ObjectIsSameWithInprecision(object one, object two)
        {
            if (one == null || two == null)
                return one == null && two == null;
            if (one.Equals(two))
                return true;
        
            switch (one)
            {
                case Vector3 target:
                    if (target.AlmostEquals((Vector3) two, PhotonNetwork.precisionForVectorSynchronization))
                        return true;
                    break;
            
                case Vector2 vector3:
                    if (vector3.AlmostEquals((Vector2) two, PhotonNetwork.precisionForVectorSynchronization))
                        return true;
                    break;
            
                case Quaternion quaternion:
                    if (quaternion.AlmostEquals((Quaternion) two, PhotonNetwork.precisionForQuaternionSynchronization))
                        return true;
                    break;
            
                case float num:
                    if (num.AlmostEquals((float) two, PhotonNetwork.precisionForFloatSynchronization))
                        return true;
                    break;
            
            }
            return false;
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Parameters.Count == 0) // Can this even happen?
                return;
        
            Player sender = Player.Self;
            if (photonEvent[ParameterCode.ActorNr] is int key)
                if (mActors.TryGetValue(key, out Player player)) // Ensure it's not null
                    sender = player;

            if (photonEvent.Code == EventCode.Leave)
                HandleEventLeave(sender.ID);
        
            if (sender.IsIgnored)
                return;

            try
            {
                switch ((PhotonEvent) photonEvent.Code)
                {
                    case PhotonEvent.ModIdentify:
                        if (sender.Mod > CustomMod.RC)
                            Shelter.LogConsole("{0} mod is identifyed as {1} but sent Cyan mod identificator.",
                                LogType.Warning, sender, sender.Mod);

                        sender.Mod = CustomMod.Cyan;
                        break;

                    case PhotonEvent.RPC:
                        this.ExecuteRPC(photonEvent[ParameterCode.Data] as Hashtable, sender);
                        break;

                    case PhotonEvent.SendSerialize:
                    case PhotonEvent.SendSerializeReliable:
                    {
                        if (photonEvent[ParameterCode.Data] is Hashtable hashtable)
                        {
                            if (!(hashtable[(byte) 0] is int networkTime))
                                return;

                            short correctPrefix = -1;
                            short hashtableIndex = 1;
                            if (hashtable.ContainsKey((byte) 1))
                            {
                                if (!(hashtable[(byte) 1] is short prefix))
                                    return;

                                correctPrefix = prefix;
                                hashtableIndex = 2;
                            }

                            for (short i = hashtableIndex; i < hashtable.Count; i = (short) (i + 1))
                                this.OnSerializeRead(hashtable[i] as Hashtable, sender, networkTime, correctPrefix);
                        }

                        break;
                    }


                    case PhotonEvent.Instantiation:
                    {
                        if (photonEvent[ParameterCode.Data] is Hashtable data)
                            if (data[(byte) 0] is string)
                                this.DoInstantiate(data, sender, null);
                        break;
                    }

                    case PhotonEvent.CloseConnection: // TODO: Add reconnect
                        if (!sender.IsMasterClient)
                            throw new NotAllowedException(photonEvent.Code, sender);
                        break;

                    case PhotonEvent.Destroy:
                    {
                        if (photonEvent[ParameterCode.Data] is Hashtable hashtable)
                            if (hashtable[(byte) 0] is int objId)
                                if (instantiatedObjects.TryGetValue(objId, out var obj) && obj != null)
                                    RemoveInstantiatedGO(obj, true);
                        break;
                    }

                    case PhotonEvent.DestroyPlayer:
                    {
                        if (photonEvent[ParameterCode.Data] is Hashtable hashtable)
                        {
                            if (!(hashtable[(byte) 0] is int playerId))
                                return;

                            if (playerId < 0 && sender.IsMasterClient)
                                this.DestroyAll(true);
                            else if (sender.IsMasterClient || playerId != Player.Self.ID) //TODO: Can probably be abused
                                this.DestroyPlayerObjects(playerId, true);
                        }

                        break;
                    }

                    case PhotonEvent.SetMasterClient:
                    {
                        if (!(photonEvent[245] is Hashtable hashtable))
                            return;
                        if (!(hashtable[(byte) 1] is int id))
                            return;

                        if (!sender.IsMasterClient && !sender.IsLocal)
                        {
                            if (PhotonNetwork.isMasterClient)
                            {
                                GameManager.noRestart = true;
                                PhotonNetwork.SetMasterClient(Player.Self);
                                GameManager.instance.KickPlayerRC(sender, true, "stealing MC.");
                                throw new NotAllowedException(photonEvent.Code, sender);
                            }
                        }

                        SetMasterClient(id, false);
                        break;
                    }
                    case PhotonEvent.AppStats:
                    {
                        if (photonEvent[ParameterCode.PeerCount] is int players &&
                            photonEvent[ParameterCode.MasterPeerCount] is int playersMC &&
                            photonEvent[ParameterCode.GameCount] is int gameCount)
                        {
                            this.mPlayersInRoomsCount = players;
                            this.mPlayersOnMasterCount = playersMC;
                            this.mGameCount = gameCount;
                        }

                        break;
                    }

                    case PhotonEvent.QueueState:
                        if (!sender.IsLocal)
                            throw new NotAllowedException(photonEvent.Code, sender);

                        if (photonEvent.Parameters.ContainsKey(ParameterCode.MatchMakingType))
                            if (photonEvent[ParameterCode.MatchMakingType] is int type)
                                this.mQueuePosition = type;
                        return;

                    case PhotonEvent.GameList:
                    {
                        if (!(photonEvent[ParameterCode.GameList] is Hashtable hashtable))
                            return;

                        mGameList = new Dictionary<string, Room>();
                        foreach (DictionaryEntry entry in hashtable)
                        {
                            string roomName = (string) entry.Key;

                            if (!Room.TryParse(roomName, (Hashtable) entry.Value, out Room room))
                                continue;

                            mGameList[roomName] = room;
                        }

                        Room.List = new List<Room>(mGameList.Values);
                        Room.List.Sort();
                        SendMonoMessage(PhotonNetworkingMessage.OnReceivedRoomListUpdate);
                        break;
                    }
                    case PhotonEvent.GameListUpdate:
                    {
                        if (!(photonEvent[ParameterCode.GameList] is Hashtable hashtable))
                            return;

                        foreach (DictionaryEntry entry in hashtable)
                        {
                            string roomName = (string) entry.Key;

                            if (!Room.TryParse(roomName, (Hashtable) entry.Value, out Room room))
                                continue;

                            if (room.RemovedFromList)
                                mGameList.Remove(roomName);
                            else
                                mGameList[roomName] = room;
                        }

                        Room.List = new List<Room>(mGameList.Values);
                        Room.List.Sort();
                        SendMonoMessage(PhotonNetworkingMessage.OnReceivedRoomListUpdate);
                        break;
                    }

                    case PhotonEvent.PropertiesChanged:
                    {
                        if (!(photonEvent[ParameterCode.TargetActorNr] is int id))
                            return;

                        if (id == 0)
                        {
                            if (photonEvent[ParameterCode.Properties] is Hashtable hash)
                                ReadoutProperties(hash, null, id);
                            return;
                        }

                        if (photonEvent[ParameterCode.Properties] is Hashtable hashtable)
                        {
                            if (!sender.IsLocal)
                            {
                                PlayerProperties props = new PlayerProperties(hashtable)
                                {
                                    ["sender"] = sender
                                };

                                if (id == sender.ID)
                                {
                                    if (props.Acceleration > 150 || props.Blade > 125 || props.Gas > 150 ||
                                        props.Speed > 140)
                                    {
                                        if (PhotonNetwork.isMasterClient)
                                            GameManager.instance.KickPlayerRC(sender, true, "Illegal stats.");
                                        throw new NotAllowedException(photonEvent.Code, sender);
                                    }
                                }

                                if (props.ContainsKey("thisName")) //TODO: Add ignore and remove this if possible
                                {
                                    if (id != sender.ID)
                                        InstantiateTracker.instance.resetPropertyTracking(id);
                                    else if (!InstantiateTracker.instance.PropertiesChanged(sender))
                                        return;
                                }
                            }

                            this.ReadoutProperties(null, hashtable, id);
                        }

                        break;
                    }

                    case PhotonEvent.Leave: // Moved above (here only to prevent the default case)
                        break;

                    case PhotonEvent.Join:
                    {
                        if (!(photonEvent[ParameterCode.ActorNr] is int senderId))
                            return;

                        if (photonEvent[ParameterCode.PlayerProperties] is Hashtable properties)
                        {
                            if (!mActors.ContainsKey(senderId))
                                AddNewPlayer(senderId, new Player(mLocalActor.ID == senderId, senderId, properties));
                            ResetPhotonViewsOnSerialize();

                            // We joined room
                            if (senderId == this.mLocalActor.ID)
                            {
                                // We created it
                                if (this.mLastJoinType == JoinType.JoinOrCreateRoom && this.mLocalActor.ID == 1)
                                    SendMonoMessage(PhotonNetworkingMessage.OnCreatedRoom);

                                // Create Player instances
                                if (photonEvent[ParameterCode.ActorList] is int[] ids)
                                {
                                    foreach (int id in ids)
                                        if (mLocalActor.ID != id && !mActors.ContainsKey(id))
                                            AddNewPlayer(id, new Player(false, id, string.Empty));

                                    SendMonoMessage(PhotonNetworkingMessage.OnJoinedRoom);
                                }

                                return;
                            }

                            // Player joined room
                            SendMonoMessage(PhotonNetworkingMessage.OnPhotonPlayerConnected, mActors[senderId]);
                        }

                        break;
                    }

                    default:
                        if (!sender.IsLocal) // Disallow everything that's not expected.
                            throw new NotAllowedException(photonEvent.Code, sender);
                        break;
                }
            }
            catch (Exception e)
            {
                Shelter.LogBoth("Exception on Event({0})", LogType.Error, (PhotonEvent) photonEvent.Code);
                Shelter.LogException(e);
            }
        }

        public void OnOperationResponse(OperationResponse operationResponse)
        {
            if (PhotonNetwork.networkingPeer.states == ClientState.Disconnecting)
            {
                if (PhotonNetwork.LogLevel >= PhotonLogLevel.Informational)
                {
                    Debug.Log("OperationResponse ignored while disconnecting. Code: " + operationResponse.OperationCode);
                }
                return;
            }
            if (operationResponse.ReturnCode == ErrorCode.Ok)
            {
                if (PhotonNetwork.LogLevel >= PhotonLogLevel.Informational)
                {
                    Debug.Log(operationResponse.ToString());
                }
            }
            else if (operationResponse.ReturnCode == ErrorCode.OperationNotAllowedInCurrentState)
            {
                Debug.LogError("Operation " + operationResponse.OperationCode + " could not be executed (yet). Wait for state JoinedLobby or ConnectedToMaster and their callbacks before calling operations. WebRPCs need a server-side configuration. Enum OperationCode helps identify the operation.");
            }
            else if (operationResponse.ReturnCode == ErrorCode.PluginReportedError)
            {
                Debug.LogError(string.Concat("Operation ", operationResponse.OperationCode, " failed in a server-side plugin. Check the configuration in the Dashboard. Message from server-plugin: ", operationResponse.DebugMessage));
            }
            else if (PhotonNetwork.LogLevel >= PhotonLogLevel.Informational)
            {
                Debug.LogError(string.Concat("Operation failed: ", operationResponse.ToStringFull(), " Server: ", this.server));
            }
            if (operationResponse.Parameters.ContainsKey(221))
            {
                if (this.CustomAuthenticationValues == null)
                {
                    this.CustomAuthenticationValues = new AuthenticationValues();
                }
                this.CustomAuthenticationValues.Secret = operationResponse[221] as string;
            }
            switch (operationResponse.OperationCode)
            {
                case OperationCode.WebRpc:
                {
                    SendMonoMessage(PhotonNetworkingMessage.OnWebRpcResponse, operationResponse);
                    this.externalListener.OnOperationResponse(operationResponse);
                    return;
                }
                case OperationCode.GetRegions:
                {
                    if (operationResponse.ReturnCode != ErrorCode.InvalidAuthentication)
                    {
                        string[] strArray = operationResponse[210] as string[];
                        if (strArray == null || !(operationResponse[230] is string[] strArray2) || strArray.Length != strArray2.Length)
                        {
                            Debug.LogError("The region arrays from Name Server are not ok. Must be non-null and same length.");
                        }
                        else
                        {
                            this.AvailableRegions = new List<Region>(strArray.Length);
                            for (int i = 0; i < strArray.Length; i++)
                            {
                                string str = strArray[i];
                                if (!string.IsNullOrEmpty(str))
                                {
                                    CloudRegionCode code = Region.Parse(str.ToLower());
                                    Region item = new Region
                                    {
                                        Code = code,
                                        HostAndPort = strArray2[i]
                                    };
                                    this.AvailableRegions.Add(item);
                                }
                            }
                            if (PhotonNetwork.PhotonServerSettings.HostType == ServerSettings.HostingOption.BestRegion)
                            {
                                PhotonHandler.PingAvailableRegionsAndConnectToBest();
                            }
                        }
                        this.externalListener.OnOperationResponse(operationResponse);
                        return;
                    }
                    Debug.LogError(string.Format("The appId this client sent is unknown on the server (Cloud). Check settings. If using the Cloud, check account."));
                    object[] objArray8 = new object[] { DisconnectCause.InvalidAuthentication };
                    SendMonoMessage(PhotonNetworkingMessage.OnFailedToConnectToPhoton, objArray8);
                    this.states = ClientState.Disconnecting;
                    this.Disconnect();
                    return;
                }
                case OperationCode.FindFriends:
                {
                    bool[] flagArray = operationResponse[1] as bool[];
                    string[] strArray3 = operationResponse[2] as string[];
                    if (flagArray == null || strArray3 == null || this.friendListRequested == null || flagArray.Length != this.friendListRequested.Length)
                    {
                        Debug.LogError("FindFriends failed to apply the result, as a required value wasn't provided or the friend list length differed from result.");
                    }
                    else
                    {
                        List<FriendInfo> list = new List<FriendInfo>(this.friendListRequested.Length);
                        for (int j = 0; j < this.friendListRequested.Length; j++)
                        {
                            FriendInfo info = new FriendInfo
                            {
                                Name = this.friendListRequested[j],
                                Room = strArray3[j],
                                IsOnline = flagArray[j]
                            };
                            list.Insert(j, info);
                        }
                        PhotonNetwork.Friends = list;
                    }
                    this.friendListRequested = null;
                    this.isFetchingFriends = false;
                    this.friendListTimestamp = Environment.TickCount;
                    if (this.friendListTimestamp == 0)
                    {
                        this.friendListTimestamp = 1;
                    }
                    SendMonoMessage(PhotonNetworkingMessage.OnUpdatedFriendList);
                    this.externalListener.OnOperationResponse(operationResponse);
                    return;
                }
                case OperationCode.JoinRandomGame:
                    if (operationResponse.ReturnCode == ErrorCode.Ok)
                    {
                        string str3 = (string)operationResponse[255];
                        this.mRoomToGetInto.FullName = str3;
                        this.mGameserver = (string)operationResponse[230];
                        this.DisconnectToReconnect2();
                    }
                    else
                    {
                        if (operationResponse.ReturnCode != ErrorCode.NoRandomMatchFound)
                        {
                            if (PhotonNetwork.LogLevel >= PhotonLogLevel.Informational)
                            {
                                Debug.LogWarning(string.Format("JoinRandom failed: {0}.", operationResponse.ToStringFull()));
                            }
                        }
                        else if (PhotonNetwork.LogLevel >= PhotonLogLevel.Full)
                        {
                            Debug.Log("JoinRandom failed: No open game. Calling: OnPhotonRandomJoinFailed() and staying on master server.");
                        }
                        SendMonoMessage(PhotonNetworkingMessage.OnPhotonRandomJoinFailed);
                    }
                    this.externalListener.OnOperationResponse(operationResponse);
                    return;

                case OperationCode.JoinGame:
                    if (this.server == ServerConnection.GameServer)
                    {
                        this.GameEnteredOnGameServer(operationResponse);
                    }
                    else if (operationResponse.ReturnCode == ErrorCode.Ok)
                    {
                        this.mGameserver = (string)operationResponse[230];
                        this.DisconnectToReconnect2();
                    }
                    else
                    {
                        if (PhotonNetwork.LogLevel >= PhotonLogLevel.Informational)
                        {
                            Debug.Log(string.Format("JoinRoom failed (room maybe closed by now). Client stays on masterserver: {0}. states: {1}", operationResponse.ToStringFull(), this.states));
                        }
                        SendMonoMessage(PhotonNetworkingMessage.OnPhotonJoinRoomFailed);
                    }
                    this.externalListener.OnOperationResponse(operationResponse);
                    return;

                case OperationCode.CreateGame:
                    if (this.server != ServerConnection.GameServer)
                    {
                        if (operationResponse.ReturnCode != ErrorCode.Ok)
                        {
                            if (PhotonNetwork.LogLevel >= PhotonLogLevel.Informational)
                            {
                                Debug.LogWarning(string.Format("CreateRoom failed, client stays on masterserver: {0}.", operationResponse.ToStringFull()));
                            }
                            SendMonoMessage(PhotonNetworkingMessage.OnPhotonCreateRoomFailed);
                        }
                        else
                        {
                            string str2 = (string)operationResponse[255];
                            if (!string.IsNullOrEmpty(str2))
                            {
                                this.mRoomToGetInto.FullName = str2;
                            }
                            this.mGameserver = (string)operationResponse[230];
                            this.DisconnectToReconnect2();
                        }
                    }
                    else
                    {
                        this.GameEnteredOnGameServer(operationResponse);
                    }
                    this.externalListener.OnOperationResponse(operationResponse);
                    return;

                case OperationCode.LeaveLobby:
                    this.states = ClientState.Authenticated;
                    this.LeftLobbyCleanup();
                    this.externalListener.OnOperationResponse(operationResponse);
                    return;

                case OperationCode.JoinLobby:
                    this.states = ClientState.JoinedLobby;
                    this.insideLobby = true;
                    SendMonoMessage(PhotonNetworkingMessage.OnJoinedLobby);
                    this.externalListener.OnOperationResponse(operationResponse);
                    return;

                case OperationCode.Authenticate:
                    if (operationResponse.ReturnCode == ErrorCode.Ok)
                    {
                        if (this.server == ServerConnection.NameServer)
                        {
                            this.MasterServerAddress = operationResponse[230] as string;
                            this.DisconnectToReconnect2();
                        }
                        else switch (this.server)
                        {
                            case ServerConnection.MasterServer when PhotonNetwork.autoJoinLobby:
                                this.states = ClientState.Authenticated;
                                this.OpJoinLobby(this.lobby);
                                break;
                            case ServerConnection.MasterServer:
                                this.states = ClientState.ConnectedToMaster;
                                SendMonoMessage(PhotonNetworkingMessage.OnConnectedToMaster);
                                break;
                            case ServerConnection.GameServer:
                                this.states = ClientState.Joining;
                                if (this.mLastJoinType == JoinType.JoinRoom || this.mLastJoinType == JoinType.JoinRandomRoom || this.mLastJoinType == JoinType.JoinOrCreateRoom)
                                    this.OpJoinRoom(this.mRoomToGetInto.FullName, this.mRoomOptionsForCreate, this.mRoomToEnterLobby, this.mLastJoinType == JoinType.JoinOrCreateRoom);
                                else if (this.mLastJoinType == JoinType.CreateRoom)
                                    this.OpCreateGame(this.mRoomToGetInto.FullName, this.mRoomOptionsForCreate, this.mRoomToEnterLobby);
                                break;
                        }
                        this.externalListener.OnOperationResponse(operationResponse);
                        return;
                    }
                    if (operationResponse.ReturnCode != ErrorCode.InvalidOperation)
                    {
                        if (operationResponse.ReturnCode == ErrorCode.InvalidAuthentication)
                        {
                            Debug.LogError(string.Format("The appId this client sent is unknown on the server (Cloud). Check settings. If using the Cloud, check account."));
                            object[] objArray3 = new object[] { DisconnectCause.InvalidAuthentication };
                            SendMonoMessage(PhotonNetworkingMessage.OnFailedToConnectToPhoton, objArray3);
                        }
                        else if (operationResponse.ReturnCode == ErrorCode.CustomAuthenticationFailed)
                        {
                            Debug.LogError(string.Format("Custom Authentication failed (either due to user-input or configuration or AuthParameter string format). Calling: OnCustomAuthenticationFailed()"));
                            object[] objArray4 = new object[] { operationResponse.DebugMessage };
                            SendMonoMessage(PhotonNetworkingMessage.OnCustomAuthenticationFailed, objArray4);
                        }
                        else
                        {
                            Debug.LogError(string.Format("Authentication failed: '{0}' Code: {1}", operationResponse.DebugMessage, operationResponse.ReturnCode));
                        }
                    }
                    else
                    {
                        Debug.LogError(string.Format("If you host Photon yourself, make sure to start the 'Instance LoadBalancing' " + ServerAddress));
                    }
                    break;

                case OperationCode.GetProperties:
                {
                    Hashtable pActorProperties = (Hashtable) operationResponse[249];
                    Hashtable gameProperties = (Hashtable)operationResponse[248];
                    this.ReadoutProperties(gameProperties, pActorProperties, 0);
                    this.externalListener.OnOperationResponse(operationResponse);
                    return;
                }
                case OperationCode.SetProperties:
                case OperationCode.RaiseEvent:
                    this.externalListener.OnOperationResponse(operationResponse);
                    return;
        
                case OperationCode.Leave:
                    this.DisconnectToReconnect2();
                    this.externalListener.OnOperationResponse(operationResponse);
                    return;
        
                default:
                    Debug.LogWarning(string.Format("OperationResponse unhandled: {0}", operationResponse));
                    this.externalListener.OnOperationResponse(operationResponse);
                    return;
            }
            this.states = ClientState.Disconnecting;
            this.Disconnect();
            if (operationResponse.ReturnCode == ErrorCode.MaxCcuReached)
            {
                Shelter.LogBoth("Currently, the limit of users is reached for this title. Try again later. Disconnecting", LogType.Warning);
                SendMonoMessage(PhotonNetworkingMessage.OnPhotonMaxCccuReached);
                SendMonoMessage(PhotonNetworkingMessage.OnConnectionFail, DisconnectCause.MaxCcuReached);
            }
            else if (operationResponse.ReturnCode == ErrorCode.InvalidRegion)
            {
                Shelter.LogBoth("The used master server address is not available with the subscription currently used. Got to Photon Cloud Dashboard or change URL. Disconnecting.", LogType.Warning);
                SendMonoMessage(PhotonNetworkingMessage.OnConnectionFail, DisconnectCause.InvalidRegion);
            }
            else if (operationResponse.ReturnCode == ErrorCode.AuthenticationTicketExpired)
            {
                Shelter.LogBoth("The authentication ticket expired. You need to connect (and authenticate) again. Disconnecting.", LogType.Warning);
                SendMonoMessage(PhotonNetworkingMessage.OnConnectionFail, DisconnectCause.AuthenticationTicketExpired);
            }
        }

        private void OnSerializeRead(Hashtable data, Player sender, int networkTime, short correctPrefix)
        {
            int viewID = (int) data[(byte) 0];
            PhotonView photonView = this.GetPhotonView(viewID);
            if (photonView == null)
            {
                Debug.LogWarning(string.Concat("Received OnSerialization for view ID ", viewID, ". We have no such PhotonView! Ignored this if you're leaving a room. State: ", this.states));
            }
            else if (photonView.prefix > 0 && correctPrefix != photonView.prefix)
            {
                Debug.LogError(string.Concat("Received OnSerialization for view ID ", viewID, " with prefix ", correctPrefix, ". Our prefix is ", photonView.prefix));
            }
            else if (photonView.@group == 0 || this.allowedReceivingGroups.Contains(photonView.group))
            {
                if (photonView.synchronization == ViewSynchronization.ReliableDeltaCompressed)
                {
                    if (!this.DeltaCompressionRead(photonView, data))
                    {
                        if (PhotonNetwork.LogLevel >= PhotonLogLevel.Informational)
                        {
                            Debug.Log(string.Concat("Skipping packet for ", photonView.name, " [", photonView.viewID, "] as we haven't received a full packet for delta compression yet. This is OK if it happens for the first few frames after joining a game."));
                        }
                        return;
                    }
                    photonView.lastOnSerializeDataReceived = data[(byte) 1] as object[];
                }
                if (photonView.observed is UnityEngine.MonoBehaviour)
                {
                    object[] incomingData = data[(byte) 1] as object[];
                    PhotonStream pStream = new PhotonStream(false, incomingData);
                    PhotonMessageInfo info = new PhotonMessageInfo(sender, networkTime, photonView);
                    photonView.ExecuteOnSerialize(pStream, info);
                }
                else if (photonView.observed is Transform observed)
                {
                    object[] objArray2 = data[(byte) 1] as object[];
                    if (objArray2.Length >= 1 && objArray2[0] != null)
                    {
                        observed.localPosition = (Vector3) objArray2[0];
                    }
                    if (objArray2.Length >= 2 && objArray2[1] != null)
                    {
                        observed.localRotation = (Quaternion) objArray2[1];
                    }
                    if (objArray2.Length >= 3 && objArray2[2] != null)
                    {
                        observed.localScale = (Vector3) objArray2[2];
                    }
                }
                else if (photonView.observed is Rigidbody rigidbody)
                {
                    object[] objArray3 = data[(byte) 1] as object[];
                    if (objArray3.Length >= 1 && objArray3[0] != null)
                    {
                        rigidbody.velocity = (Vector3) objArray3[0];
                    }
                    if (objArray3.Length >= 2 && objArray3[1] != null)
                    {
                        rigidbody.angularVelocity = (Vector3) objArray3[1];
                    }
                }
                else
                {
                    Debug.LogError("Type of observed is unknown when receiving.");
                }
            }
        }

        private Hashtable OnSerializeWrite(PhotonView view)
        {
            List<object> list = new List<object>();
            if (view.observed is UnityEngine.MonoBehaviour)
            {
                PhotonStream pStream = new PhotonStream(true, null);
                PhotonMessageInfo info = new PhotonMessageInfo(this.mLocalActor, ServerTimeInMilliSeconds, view);
                view.ExecuteOnSerialize(pStream, info);
                if (pStream.Count == 0)
                {
                    return null;
                }
                list = pStream.data;
            }
            else if (view.observed is Transform)
            {
                Transform observed = (Transform) view.observed;
                if (view.onSerializeTransformOption != OnSerializeTransform.OnlyPosition && view.onSerializeTransformOption != OnSerializeTransform.PositionAndRotation && view.onSerializeTransformOption != OnSerializeTransform.All)
                {
                    list.Add(null);
                }
                else
                {
                    list.Add(observed.localPosition);
                }
                if (view.onSerializeTransformOption != OnSerializeTransform.OnlyRotation && view.onSerializeTransformOption != OnSerializeTransform.PositionAndRotation && view.onSerializeTransformOption != OnSerializeTransform.All)
                {
                    list.Add(null);
                }
                else
                {
                    list.Add(observed.localRotation);
                }
                if (view.onSerializeTransformOption == OnSerializeTransform.OnlyScale || view.onSerializeTransformOption == OnSerializeTransform.All)
                {
                    list.Add(observed.localScale);
                }
            }
            else
            {
                if (!(view.observed is Rigidbody))
                {
                    Debug.LogError("Observed type is not serializable: " + view.observed.GetType());
                    return null;
                }
                Rigidbody rigidbody = (Rigidbody) view.observed;
                if (view.onSerializeRigidBodyOption != OnSerializeRigidBody.OnlyAngularVelocity)
                {
                    list.Add(rigidbody.velocity);
                }
                else
                {
                    list.Add(null);
                }
                if (view.onSerializeRigidBodyOption != OnSerializeRigidBody.OnlyVelocity)
                {
                    list.Add(rigidbody.angularVelocity);
                }
            }
            object[] lastData = list.ToArray();
            if (view.synchronization == ViewSynchronization.UnreliableOnChange)
            {
                if (this.AlmostEquals(lastData, view.lastOnSerializeDataSent))
                {
                    if (view.mixedModeIsReliable)
                    {
                        return null;
                    }
                    view.mixedModeIsReliable = true;
                    view.lastOnSerializeDataSent = lastData;
                }
                else
                {
                    view.mixedModeIsReliable = false;
                    view.lastOnSerializeDataSent = lastData;
                }
            }
            Hashtable data = new Hashtable
            {
                [(byte)0] = view.viewID,
                [(byte)1] = lastData
            };
            if (view.synchronization == ViewSynchronization.ReliableDeltaCompressed)
            {
                bool flag = this.DeltaCompressionWrite(view, data);
                view.lastOnSerializeDataSent = lastData;
                if (!flag)
                {
                    return null;
                }
            }
            return data;
        }

        public void OnStatusChanged(StatusCode statusCode)
        {
            DisconnectCause cause;
            switch (statusCode)
            {
                case StatusCode.SecurityExceptionOnConnect:
                case StatusCode.ExceptionOnConnect:
                {
                    this.states = ClientState.PeerCreated;
                    if (this.CustomAuthenticationValues != null)
                    {
                        this.CustomAuthenticationValues.Secret = null;
                    }
                    cause = (DisconnectCause) statusCode;
                    SendMonoMessage(PhotonNetworkingMessage.OnFailedToConnectToPhoton, cause);
                    break;
                }
                case StatusCode.Connect:
                    switch (this.states)
                    {
                        case ClientState.ConnectingToNameServer:
                            if (PhotonNetwork.LogLevel >= PhotonLogLevel.Full)
                                Debug.Log("Connected to NameServer.");
                        
                            this.server = ServerConnection.NameServer;
                            if (this.CustomAuthenticationValues != null)
                                this.CustomAuthenticationValues.Secret = null;

                            break;
                        case ClientState.ConnectingToGameserver:
                            if (PhotonNetwork.LogLevel >= PhotonLogLevel.Full)
                                Debug.Log("Connected to gameserver.");
                        
                            this.server = ServerConnection.GameServer;
                            this.states = ClientState.ConnectedToGameserver;
                            break;
                    }

                    if (this.states == ClientState.ConnectingToMasterserver)
                    {
                        if (PhotonNetwork.LogLevel >= PhotonLogLevel.Full)
                            Debug.Log("Connected to masterserver.");
                    
                        this.server = ServerConnection.MasterServer;
                        this.states = ClientState.ConnectedToMaster;
                        if (this.IsInitialConnect)
                        {
                            this.IsInitialConnect = false;
                            SendMonoMessage(PhotonNetworkingMessage.OnConnectedToPhoton);
                        }
                    }
                    EstablishEncryption();
                    if (this.IsAuthorizeSecretAvailable)
                    {
                        this.didAuthenticate = this.OpAuthenticate(this.mAppId, this.mAppVersionPun, this.PlayerName, this.CustomAuthenticationValues, this.CloudRegion.ToString());
                        if (this.didAuthenticate)
                        {
                            this.states = ClientState.Authenticating;
                        }
                    }
                    break;

                case StatusCode.Disconnect:
                    this.didAuthenticate = false;
                    this.isFetchingFriends = false;
                    if (this.server == ServerConnection.GameServer)
                    {
                        this.LeftRoomCleanup();
                    }
                    if (this.server == ServerConnection.MasterServer)
                    {
                        this.LeftLobbyCleanup();
                    }
                    if (this.states == ClientState.DisconnectingFromMasterserver)
                    {
                        if (this.Connect(this.mGameserver, ServerConnection.GameServer))
                        {
                            this.states = ClientState.ConnectingToGameserver;
                        }
                    }
                    else if (this.states == ClientState.DisconnectingFromGameserver || this.states == ClientState.DisconnectingFromNameServer)
                    {
                        if (this.Connect(this.MasterServerAddress, ServerConnection.MasterServer))
                        {
                            this.states = ClientState.ConnectingToMasterserver;
                        }
                    }
                    else
                    {
                        if (this.CustomAuthenticationValues != null)
                        {
                            this.CustomAuthenticationValues.Secret = null;
                        }
                        this.states = ClientState.PeerCreated;
                        SendMonoMessage(PhotonNetworkingMessage.OnDisconnectedFromPhoton);
                    }
                    break;

                case StatusCode.Exception:
                {
                    if (!this.IsInitialConnect)
                    {
                        this.states = ClientState.PeerCreated;
                        cause = (DisconnectCause) statusCode;
                        object[] objArray3 = new object[] { cause };
                        SendMonoMessage(PhotonNetworkingMessage.OnConnectionFail, objArray3);
                        break;
                    }
                    Debug.LogError("Exception while connecting to: " + ServerAddress + ". Check if the server is available.");
                    if (ServerAddress == null || ServerAddress.StartsWith("127.0.0.1"))
                    {
                        Debug.LogWarning("The server address is 127.0.0.1 (localhost): Make sure the server is running on this machine. Android and iOS emulators have their own localhost.");
                        if (ServerAddress == this.mGameserver)
                        {
                            Debug.LogWarning("This might be a misconfiguration in the game server config. You need to edit it to a (public) address.");
                        }
                    }
                    this.states = ClientState.PeerCreated;
                    cause = (DisconnectCause) statusCode;
                    object[] objArray2 = new object[] { cause };
                    SendMonoMessage(PhotonNetworkingMessage.OnFailedToConnectToPhoton, objArray2);
                    Disconnect();
                    return;
                }
                case StatusCode.QueueOutgoingReliableWarning:
                case StatusCode.QueueOutgoingUnreliableWarning:
                case StatusCode.SendError:
                case StatusCode.QueueOutgoingAcksWarning:
                case StatusCode.QueueSentWarning:
                    break;

                case StatusCode.QueueIncomingReliableWarning:
                case StatusCode.QueueIncomingUnreliableWarning:
                    Debug.Log(statusCode + ". This client buffers many incoming messages. This is OK temporarily. With lots of these warnings, check if you send too much or execute messages too slow. " + (!PhotonNetwork.isMessageQueueRunning ? "Your isMessageQueueRunning is false. This can cause the issue temporarily." : string.Empty));
                    break;

                case StatusCode.ExceptionOnReceive:
                case StatusCode.TimeoutDisconnect:
                case StatusCode.DisconnectByServer:
                case StatusCode.DisconnectByServerUserLimit:
                case StatusCode.DisconnectByServerLogic:
                    if (!this.IsInitialConnect)
                    {
                        cause = (DisconnectCause) statusCode;
                        object[] objArray6 = new object[] { cause };
                        SendMonoMessage(PhotonNetworkingMessage.OnConnectionFail, objArray6);
                    }
                    else
                    {
                        Debug.LogWarning(string.Concat(statusCode, " while connecting to: ", ServerAddress, ". Check if the server is available."));
                        cause = (DisconnectCause) statusCode;
                        object[] objArray5 = new object[] { cause };
                        SendMonoMessage(PhotonNetworkingMessage.OnFailedToConnectToPhoton, objArray5);
                    }
                    if (this.CustomAuthenticationValues != null)
                    {
                        this.CustomAuthenticationValues.Secret = null;
                    }
                    this.Disconnect();
                    break;

                case StatusCode.EncryptionEstablished:
                    if (this.server == ServerConnection.NameServer)
                    {
                        this.states = ClientState.ConnectedToNameServer;
                        if (!this.didAuthenticate && this.CloudRegion == CloudRegionCode.None)
                        {
                            this.OpGetRegions(this.mAppId);
                        }
                    }
                    if (!this.didAuthenticate && (!this.IsUsingNameServer || this.CloudRegion != CloudRegionCode.None))
                    {
                        this.didAuthenticate = this.OpAuthenticate(this.mAppId, this.mAppVersionPun, this.PlayerName, this.CustomAuthenticationValues, this.CloudRegion.ToString());
                        if (this.didAuthenticate)
                        {
                            this.states = ClientState.Authenticating;
                        }
                    }
                    break;

                case StatusCode.EncryptionFailedToEstablish:
                    Debug.LogError("Encryption wasn't established: " + statusCode + ". Going to authenticate anyways.");
                    this.OpAuthenticate(this.mAppId, this.mAppVersionPun, this.PlayerName, this.CustomAuthenticationValues, this.CloudRegion.ToString());
                    break;

                default:
                    Debug.LogError("Received unknown status code: " + statusCode);
                    break;
            }
            this.externalListener.OnStatusChanged(statusCode);
        }

        public void OpCleanRpcBuffer(PhotonView view)
        {
            Hashtable customEventContent = new Hashtable
            {
                [(byte)0] = view.viewID
            };
            RaiseEventOptions raiseEventOptions = new RaiseEventOptions {
                CachingOption = EventCaching.RemoveFromRoomCache
            };
            this.OpRaiseEvent(200, customEventContent, true, raiseEventOptions);
        }

        public void OpCleanRpcBuffer(int actorNumber)
        {
            RaiseEventOptions options = new RaiseEventOptions {
                CachingOption = EventCaching.RemoveFromRoomCache
            };
            options.TargetActors = new int[] { actorNumber };
            RaiseEventOptions raiseEventOptions = options;
            this.OpRaiseEvent(200, null, true, raiseEventOptions);
        }

        public bool OpCreateGame(string roomName, RoomOptions roomOptions, TypedLobby typedLobby)
        {
            bool flag;
            if (!(flag = this.server == ServerConnection.GameServer))
            {
                this.mRoomOptionsForCreate = roomOptions;
                if (!Room.TryParse(roomName, roomOptions, out Room room))
                    return false;
                this.mRoomToGetInto = room;
                this.mRoomToEnterLobby = typedLobby ?? (!this.insideLobby ? null : this.lobby);
            }
            this.mLastJoinType = JoinType.CreateRoom;
            return OpCreateRoom(roomName, roomOptions, this.mRoomToEnterLobby, this.GetLocalActorProperties(), flag);
        }

        public override bool OpFindFriends(string[] friendsToFind)
        {
            if (this.isFetchingFriends)
            {
                return false;
            }
            this.friendListRequested = friendsToFind;
            this.isFetchingFriends = true;
            return base.OpFindFriends(friendsToFind);
        }

        public override bool OpJoinRandomRoom(Hashtable expectedCustomRoomProperties, byte expectedMaxPlayers, Hashtable playerProperties, MatchmakingMode matchingType, TypedLobby typedLobby, string sqlLobbyFilter)
        {
            this.mRoomToGetInto = new Room("", (Hashtable)null);
            this.mRoomToEnterLobby = null;
            this.mLastJoinType = JoinType.JoinRandomRoom;
            return base.OpJoinRandomRoom(expectedCustomRoomProperties, expectedMaxPlayers, playerProperties, matchingType, typedLobby, sqlLobbyFilter);
        }

        public bool OpJoinRoom(string roomName, RoomOptions roomOptions, TypedLobby typedLobby, bool createIfNotExists)
        {
            bool flag;
            if (!(flag = this.server == ServerConnection.GameServer))
            {
                this.mRoomOptionsForCreate = roomOptions;
                this.mRoomToGetInto = new Room(roomName, roomOptions);
                this.mRoomToEnterLobby = null;
                if (createIfNotExists)
                {
                    this.mRoomToEnterLobby = typedLobby ?? (!this.insideLobby ? null : this.lobby);
                }
            }
            this.mLastJoinType = !createIfNotExists ? JoinType.JoinRoom : JoinType.JoinOrCreateRoom;
            return base.OpJoinRoom(roomName, roomOptions, this.mRoomToEnterLobby, createIfNotExists, this.GetLocalActorProperties(), flag);
        }

        public virtual bool OpLeave()
        {
            if (this.states != ClientState.Joined)
            {
                Debug.LogWarning("Not sending leave operation. State is not 'Joined': " + this.states);
                return false;
            }
            return this.OpCustom(254, null, true, 0);
        }

        public override bool OpRaiseEvent(byte eventCode, object customEventContent, bool sendReliable, RaiseEventOptions raiseEventOptions)
        {
            if (PhotonNetwork.offlineMode)
            {
                return false;
            }
            return base.OpRaiseEvent(eventCode, customEventContent, sendReliable, raiseEventOptions);
        }

        public void OpRemoveCompleteCache()
        {
            RaiseEventOptions raiseEventOptions = new RaiseEventOptions {
                CachingOption = EventCaching.RemoveFromRoomCache,
                Receivers = ReceiverGroup.MasterClient
            };
            this.OpRaiseEvent(0, null, true, raiseEventOptions);
        }

        public void OpRemoveCompleteCacheOfPlayer(int actorNumber)
        {
            RaiseEventOptions options = new RaiseEventOptions {
                CachingOption = EventCaching.RemoveFromRoomCache
            };
            options.TargetActors = new int[] { actorNumber };
            RaiseEventOptions raiseEventOptions = options;
            this.OpRaiseEvent(0, null, true, raiseEventOptions);
        }

        private void OpRemoveFromServerInstantiationsOfPlayer(int actorNr)
        {
            RaiseEventOptions options = new RaiseEventOptions {
                CachingOption = EventCaching.RemoveFromRoomCache
            };
            options.TargetActors = new int[] { actorNr };
            RaiseEventOptions raiseEventOptions = options;
            this.OpRaiseEvent(202, null, true, raiseEventOptions);
        }

        private void ReadoutProperties(Hashtable gameProperties, Hashtable pActorProperties, int targetActorNr)
        {
            if (this.mCurrentGame != null && gameProperties != null)
            {
                this.mCurrentGame.InternalCacheProperties(gameProperties);
                SendMonoMessage(PhotonNetworkingMessage.OnPhotonCustomRoomPropertiesChanged, gameProperties);
                if (PhotonNetwork.automaticallySyncScene)
                {
                    this.LoadLevelIfSynced();
                }
            }
            if (pActorProperties != null && pActorProperties.Count > 0)
            {
                if (targetActorNr > 0)
                {
                    Player playerWithID = this.GetPlayerWithID(targetActorNr);
                    if (playerWithID != null)
                    {
                        Hashtable actorPropertiesForActorNr = this.GetActorPropertiesForActorNr(pActorProperties, targetActorNr);
                        playerWithID.InternalCacheProperties(actorPropertiesForActorNr);
                        SendMonoMessage(PhotonNetworkingMessage.OnPhotonPlayerPropertiesChanged, playerWithID, actorPropertiesForActorNr);
                    }
                }
                else
                {
                    foreach (object obj2 in pActorProperties.Keys)
                    {
                        int number = (int) obj2;
                        Hashtable properties = (Hashtable) pActorProperties[obj2];
                        string name = string.Empty;
                        if (properties.ContainsKey(byte.MaxValue))
                            name = properties[byte.MaxValue] as string;
                        Player player = this.GetPlayerWithID(number);
                        if (player == null)
                        {
                            player = new Player(false, number, name);
                            this.AddNewPlayer(number, player);
                        }
                        player.InternalCacheProperties(properties);
                        object[] objArray3 = new object[] { player, properties };
                        SendMonoMessage(PhotonNetworkingMessage.OnPhotonPlayerPropertiesChanged, objArray3);
                    }
                }
            }
        }

        private void RebuildPlayerListCopies()
        {
            this.mPlayerListCopy = new Player[this.mActors.Count];
            this.mActors.Values.CopyTo(this.mPlayerListCopy, 0);
            this.mOtherPlayerListCopy = this.mPlayerListCopy.Where(player => !player.IsLocal).ToArray();
        }

        public void RegisterPhotonView(PhotonView netView)
        {
            if (!Application.isPlaying)
                this.photonViewList = new Dictionary<int, PhotonView>();
        
            else if (netView.subId != 0)
            {
                if (this.photonViewList.ContainsKey(netView.viewID))
                {
                    if (netView != this.photonViewList[netView.viewID])
                    {
                        Debug.LogError(string.Format("PhotonView ID duplicate found: {0}. New: {1} old: {2}. Maybe one wasn't destroyed on scene load?! Check for 'DontDestroyOnLoad'. Destroying old entry, adding new.", netView.viewID, netView, this.photonViewList[netView.viewID]));
                    }
                    this.RemoveInstantiatedGO(this.photonViewList[netView.viewID].gameObject, true);
                }
                this.photonViewList.Add(netView.viewID, netView);
                if (PhotonNetwork.LogLevel >= PhotonLogLevel.Full)
                {
                    Debug.Log("Registered PhotonView: " + netView.viewID);
                }
            }
        }

        public void RemoveAllInstantiatedObjects()
        {
            GameObject[] array = new GameObject[this.instantiatedObjects.Count];
            this.instantiatedObjects.Values.CopyTo(array, 0);
            for (int i = 0; i < array.Length; i++)
            {
                GameObject go = array[i];
                if (go != null)
                {
                    this.RemoveInstantiatedGO(go, false);
                }
            }
            if (this.instantiatedObjects.Count > 0)
            {
                Debug.LogError("RemoveAllInstantiatedObjects() this.instantiatedObjects.Count should be 0 by now.");
            }
            this.instantiatedObjects = new Dictionary<int, GameObject>();
        }

        private void RemoveCacheOfLeftPlayers()
        {
            Dictionary<byte, object> customOpParameters = new Dictionary<byte, object>
            {
                [244] = (byte)0,
                [247] = (byte)7
            };
            this.OpCustom(253, customOpParameters, true, 0);
        }

        public void RemoveInstantiatedGO(GameObject go, bool localOnly)
        {
            if (go == null)
            {
                Debug.LogError("Failed to 'network-remove' GameObject because it's null.");
            }
            else
            {
                PhotonView[] componentsInChildren = go.GetComponentsInChildren<PhotonView>();
                if (componentsInChildren != null && componentsInChildren.Length > 0)
                {
                    PhotonView view = componentsInChildren[0];
                    int ownerActorNr = view.OwnerActorNr;
                    int instantiationId = view.instantiationId;
                    if (!localOnly)
                    {
                        if (!view.isMine && (!this.mLocalActor.IsMasterClient || this.mActors.ContainsKey(ownerActorNr)))
                        {
                            Debug.LogError("Failed to 'network-remove' GameObject. Client is neither owner nor masterClient taking over for owner who left: " + view);
                            return;
                        }
                        if (instantiationId < 1)
                        {
                            Debug.LogError("Failed to 'network-remove' GameObject because it is missing a valid InstantiationId on view: " + view + ". Not Destroying GameObject or PhotonViews!");
                            return;
                        }
                    }
                    if (!localOnly)
                    {
                        this.ServerCleanInstantiateAndDestroy(instantiationId, ownerActorNr);
                    }
                    this.instantiatedObjects.Remove(instantiationId);
                    for (int i = componentsInChildren.Length - 1; i >= 0; i--)
                    {
                        PhotonView view2 = componentsInChildren[i];
                        if (view2 != null)
                        {
                            if (view2.instantiationId >= 1)
                            {
                                this.LocalCleanPhotonView(view2);
                            }
                            if (!localOnly)
                            {
                                this.OpCleanRpcBuffer(view2);
                            }
                        }
                    }
                    if (PhotonNetwork.LogLevel >= PhotonLogLevel.Full)
                    {
                        Debug.Log("Network destroy Instantiated GO: " + go.name);
                    }
                    UnityEngine.Object.Destroy(go);
                }
                else
                {
                    Debug.LogError("Failed to 'network-remove' GameObject because has no PhotonView components: " + go);
                }
            }
        }

        private void RemoveInstantiationData(int instantiationId)
        {
            this.tempInstantiationData.Remove(instantiationId);
        }

        private void RemovePlayer(int ID, Player player)
        {
            this.mActors.Remove(ID);
            if (!player.IsLocal)
            {
                this.RebuildPlayerListCopies();
            }
        }

        public void RemoveRPCsInGroup(int group)
        {
            foreach (KeyValuePair<int, PhotonView> pair in this.photonViewList)
            {
                PhotonView view = pair.Value;
                if (view.group == group)
                {
                    this.CleanRpcBufferIfMine(view);
                }
            }
        }

        private void ResetPhotonViewsOnSerialize()
        {
            foreach (PhotonView view in this.photonViewList.Values)
            {
                view.lastOnSerializeDataSent = null;
            }
        }

        private static int ReturnLowestPlayerId(Player[] players, int playerIdToIgnore)
        {
            if (players == null || players.Length == 0)
            {
                return -1;
            }
            int iD = 2147483647;
            for (int i = 0; i < players.Length; i++)
            {
                Player player = players[i];
                if (player.ID != playerIdToIgnore && player.ID < iD)
                {
                    iD = player.ID;
                }
            }
            return iD;
        }

        internal void RPC(PhotonView view, string methodName, Player player, params object[] parameters)
        {
            if (!this.blockSendingGroups.Contains(view.group))
            {
                if (view.viewID < 1)
                {
                    Debug.LogError(string.Concat("Illegal view ID:", view.viewID, " method: ", methodName, " GO:", view.gameObject.name));
                }
                if (PhotonNetwork.LogLevel >= PhotonLogLevel.Full)
                {
                    Debug.Log(string.Concat("Sending RPC \"", methodName, "\" to player[", player, "]"));
                }
                Hashtable rpcData = new Hashtable
                {
                    [(byte)0] = view.viewID
                };
                if (view.prefix > 0)
                {
                    rpcData[(byte) 1] = (short) view.prefix;
                }
                rpcData[(byte) 2] = ServerTimeInMilliSeconds;
                if (this.rpcShortcuts.TryGetValue(methodName, out var num))
                {
                    rpcData[(byte) 5] = (byte) num;
                }
                else
                {
                    rpcData[(byte) 3] = methodName;
                }
                if (parameters != null && parameters.Length > 0)
                {
                    rpcData[(byte) 4] = parameters;
                }
                if (this.mLocalActor == player)
                {
                    this.ExecuteRPC(rpcData, player);
                }
                else
                {
                    RaiseEventOptions options = new RaiseEventOptions();
                    options.TargetActors = new int[] { player.ID };
                    RaiseEventOptions raiseEventOptions = options;
                    this.OpRaiseEvent(200, rpcData, true, raiseEventOptions);
                }
            }
        }

        internal void RPC(PhotonView view, string methodName, PhotonTargets target, params object[] parameters)
        {
            if (!this.blockSendingGroups.Contains(view.group))
            {
                RaiseEventOptions options;
                if (view.viewID < 1)
                {
                    Debug.LogError(string.Concat("Illegal view ID:", view.viewID, " method: ", methodName, " GO:", view.gameObject.name));
                }
                if (PhotonNetwork.LogLevel >= PhotonLogLevel.Full)
                {
                    Debug.Log(string.Concat("Sending RPC \"", methodName, "\" to ", target));
                }
                Hashtable customEventContent = new Hashtable
                {
                    [(byte)0] = view.viewID
                };
                if (view.prefix > 0)
                {
                    customEventContent[(byte) 1] = (short) view.prefix;
                }
                customEventContent[(byte) 2] = ServerTimeInMilliSeconds;
                if (this.rpcShortcuts.TryGetValue(methodName, out var num))
                {
                    customEventContent[(byte) 5] = (byte) num;
                }
                else
                {
                    customEventContent[(byte) 3] = methodName;
                }
                if (parameters != null && parameters.Length > 0)
                {
                    customEventContent[(byte) 4] = parameters;
                }
                switch (target)
                {
                    case PhotonTargets.All:
                        options = new RaiseEventOptions {
//                        TargetActors = PhotonNetwork.playerList.Select(x => x.ID).Where(x => !GameManager.ignoreList.Contains(x)).ToArray()
                            InterestGroup = (byte) view.group
                        };
                        RaiseEventOptions raiseEventOptions = options;
                        this.OpRaiseEvent(200, customEventContent, true, raiseEventOptions);
                        this.ExecuteRPC(customEventContent, this.mLocalActor);
                        break;
                    case PhotonTargets.Others:
                        options = new RaiseEventOptions {
                            InterestGroup = (byte) view.@group
                        };
                        RaiseEventOptions options3 = options;
                        this.OpRaiseEvent(200, customEventContent, true, options3);
                        break;
                    case PhotonTargets.AllBuffered:
                        options = new RaiseEventOptions {
                            CachingOption = EventCaching.AddToRoomCache
                        };
                        RaiseEventOptions options4 = options;
                        this.OpRaiseEvent(200, customEventContent, true, options4);
                        this.ExecuteRPC(customEventContent, this.mLocalActor);
                        break;
                    case PhotonTargets.OthersBuffered:
                        options = new RaiseEventOptions {
                            CachingOption = EventCaching.AddToRoomCache
                        };
                        RaiseEventOptions options5 = options;
                        this.OpRaiseEvent(200, customEventContent, true, options5);
                        break;
                    case PhotonTargets.MasterClient when this.mMasterClient == this.mLocalActor:
                        this.ExecuteRPC(customEventContent, this.mLocalActor);
                        break;
                    case PhotonTargets.MasterClient:
                        options = new RaiseEventOptions {
                            Receivers = ReceiverGroup.MasterClient
                        };
                        RaiseEventOptions options6 = options;
                        this.OpRaiseEvent(200, customEventContent, true, options6);
                        break;
                    case PhotonTargets.AllViaServer:
                        options = new RaiseEventOptions {
                            InterestGroup = (byte) view.@group,
                            Receivers = ReceiverGroup.All
                        };
                        RaiseEventOptions options7 = options;
                        this.OpRaiseEvent(200, customEventContent, true, options7);
                        break;
                    case PhotonTargets.AllBufferedViaServer:
                        options = new RaiseEventOptions {
                            InterestGroup = (byte) view.@group,
                            Receivers = ReceiverGroup.All,
                            CachingOption = EventCaching.AddToRoomCache
                        };
                        RaiseEventOptions options8 = options;
                        this.OpRaiseEvent(200, customEventContent, true, options8);
                        break;
                    default:
                        Debug.LogError("Unsupported target enum: " + target);
                        break;
                }
            }
        }

        public void RunViewUpdate()
        {
            if (PhotonNetwork.connected && !PhotonNetwork.offlineMode && this.mActors != null && this.mActors.Count > 1)
            {
                Dictionary<int, Hashtable> dictionary = new Dictionary<int, Hashtable>();
                Dictionary<int, Hashtable> dictionary2 = new Dictionary<int, Hashtable>();
                foreach (KeyValuePair<int, PhotonView> pair in this.photonViewList)
                {
                    PhotonView view = pair.Value;
                    if (view.observed != null && view.synchronization != ViewSynchronization.Off && (view.ownerId == this.mLocalActor.ID || view.isSceneView && this.mMasterClient == this.mLocalActor) && view.gameObject.activeInHierarchy && !this.blockSendingGroups.Contains(view.@group))
                    {
                        Hashtable hashtable = this.OnSerializeWrite(view);
                        if (hashtable != null)
                        {
                            if (view.synchronization != ViewSynchronization.ReliableDeltaCompressed && !view.mixedModeIsReliable)
                            {
                                if (!dictionary2.ContainsKey(view.group))
                                {
                                    dictionary2[view.group] = new Hashtable
                                    {
                                        [(byte)0] = ServerTimeInMilliSeconds
                                    };
                                    if (this.currentLevelPrefix >= 0)
                                    {
                                        dictionary2[view.group][(byte) 1] = this.currentLevelPrefix;
                                    }
                                }
                                Hashtable hashtable3 = dictionary2[view.group];
                                hashtable3.Add((short) hashtable3.Count, hashtable);
                            }
                            else if (hashtable.ContainsKey((byte) 1) || hashtable.ContainsKey((byte) 2))
                            {
                                if (!dictionary.ContainsKey(view.group))
                                {
                                    dictionary[view.group] = new Hashtable
                                    {
                                        [(byte)0] = ServerTimeInMilliSeconds
                                    };
                                    if (this.currentLevelPrefix >= 0)
                                    {
                                        dictionary[view.group][(byte) 1] = this.currentLevelPrefix;
                                    }
                                }
                                Hashtable hashtable2 = dictionary[view.group];
                                hashtable2.Add((short) hashtable2.Count, hashtable);
                            }
                        }
                    }
                }
                RaiseEventOptions raiseEventOptions = new RaiseEventOptions();
                foreach (KeyValuePair<int, Hashtable> pair2 in dictionary)
                {
                    raiseEventOptions.InterestGroup = (byte) pair2.Key;
                    this.OpRaiseEvent(PunEvent.SendSerializeReliable, pair2.Value, true, raiseEventOptions);
                }
                foreach (KeyValuePair<int, Hashtable> pair3 in dictionary2)
                {
                    raiseEventOptions.InterestGroup = (byte) pair3.Key;
                    this.OpRaiseEvent(PunEvent.SendSerialize, pair3.Value, false, raiseEventOptions);
                }
            }
        }

        private void SendDestroyOfAll()
        {
            Hashtable customEventContent = new Hashtable
            {
                [(byte)0] = -1
            };
            this.OpRaiseEvent(PunEvent.DestroyPlayer, customEventContent, true, null);
        }

        private void SendDestroyOfPlayer(int actorNr)
        {
            Hashtable customEventContent = new Hashtable
            {
                [(byte)0] = actorNr
            };
            this.OpRaiseEvent(PunEvent.DestroyPlayer, customEventContent, true, null);
        }

        internal Hashtable SendInstantiate(string prefabName, Vector3 position, Quaternion rotation, int group, int[] viewIDs, object[] data, bool isGlobalObject)
        {
            int num = viewIDs[0];
            Hashtable customEventContent = new Hashtable
            {
                [(byte)0] = prefabName
            };
            if (position != Vector3.zero)
                customEventContent[(byte) 1] = position;
        
            if (rotation != Quaternion.identity)
                customEventContent[(byte) 2] = rotation;
        
            if (group != 0)
                customEventContent[(byte) 3] = group;
        
            if (viewIDs.Length > 1)
                customEventContent[(byte) 4] = viewIDs;
        
            if (data != null)
                customEventContent[(byte) 5] = data;
        
            if (this.currentLevelPrefix > 0)
                customEventContent[(byte) 8] = this.currentLevelPrefix;
        
            customEventContent[(byte) 6] = ServerTimeInMilliSeconds;
            customEventContent[(byte) 7] = num;
            RaiseEventOptions raiseEventOptions = new RaiseEventOptions {
                CachingOption = !isGlobalObject ? EventCaching.AddToRoomCache : EventCaching.AddToRoomCacheGlobal
            };
            this.OpRaiseEvent(PunEvent.Instantiation, customEventContent, true, raiseEventOptions);
            return customEventContent;
        }

        public static void SendMonoMessage(PhotonNetworkingMessage methodString, params object[] parameters)
        {
            HashSet<GameObject> sendMonoMessageTargets;
            if (PhotonNetwork.SendMonoMessageTargets != null)
            {
                sendMonoMessageTargets = PhotonNetwork.SendMonoMessageTargets;
            }
            else
            {
                sendMonoMessageTargets = new HashSet<GameObject>();
                Component[] componentArray = (Component[]) UnityEngine.Object.FindObjectsOfType(typeof(UnityEngine.MonoBehaviour));
                foreach (var behaviours in componentArray)
                    sendMonoMessageTargets.Add(behaviours.gameObject);
            }
            string methodName = methodString.ToString();
            foreach (GameObject obj2 in sendMonoMessageTargets)
            {
                try
                {
                    if (parameters != null && parameters.Length == 1)
                    {
                        obj2.SendMessage(methodName, parameters[0], SendMessageOptions.DontRequireReceiver);
                    }
                    else
                    {
                        obj2.SendMessage(methodName, parameters, SendMessageOptions.DontRequireReceiver);
                    }
                }
                catch
                {
                }
            }
        }

        private void SendPlayerName()
        {
            if (this.states == ClientState.Joining)
            {
                this.mPlayernameHasToBeUpdated = true;
            }
            else if (this.mLocalActor != null)
            {
                this.mLocalActor.FriendName = this.PlayerName;
                Hashtable actorProperties = new Hashtable
                {
                    [ActorProperties.PlayerName] = this.PlayerName
                };
                if (this.mLocalActor.ID > 0)
                {
                    OpSetPropertiesOfActor(this.mLocalActor.ID, actorProperties, true, 0);
                    this.mPlayernameHasToBeUpdated = false;
                }
            }
        }

        private void ServerCleanInstantiateAndDestroy(int instantiateId, int actorNr)
        {
            Hashtable customEventContent = new Hashtable
            {
                [(byte)7] = instantiateId
            };
            RaiseEventOptions options = new RaiseEventOptions
            {
                CachingOption = EventCaching.RemoveFromRoomCache,
                TargetActors = new[] {actorNr}
            };
            RaiseEventOptions raiseEventOptions = options;
            this.OpRaiseEvent(202, customEventContent, true, raiseEventOptions);
            Hashtable hashtable2 = new Hashtable
            {
                [(byte)0] = instantiateId
            };
            this.OpRaiseEvent(204, hashtable2, true, null);
        }

        public void SetApp(string appId, string gameVersion)
        {
            this.mAppId = appId.Trim();
            if (!string.IsNullOrEmpty(gameVersion))
            {
                this.mAppVersion = gameVersion.Trim();
            }
        }

        protected internal void SetLevelInPropsIfSynced(object levelId)
        {
            if (PhotonNetwork.automaticallySyncScene && PhotonNetwork.isMasterClient && PhotonNetwork.Room != null)
            {
                if (levelId == null)
                {
                    Debug.LogError("Parameter levelId can't be null!");
                }
                else
                {
                    if (PhotonNetwork.Room.Properties.ContainsKey("curScn"))
                    {
                        object obj2 = PhotonNetwork.Room.Properties["curScn"];
                        if (obj2 is int && Application.loadedLevel == (int) obj2)
                        {
                            return;
                        }
                        if (obj2 is string && Application.loadedLevelName.Equals((string) obj2))
                        {
                            return;
                        }
                    }
                    Hashtable propertiesToSet = new Hashtable(1);
                    switch (levelId)
                    {
                        case int i:
                            propertiesToSet["curScn"] = i;
                            break;
                        case string str:
                            propertiesToSet["curScn"] = str;
                            break;
                        default:
                            Debug.LogError("Parameter levelId must be int or string!");
                            break;
                    }
                    PhotonNetwork.Room.SetCustomProperties(propertiesToSet);
                    this.SendOutgoingCommands();
                }
            }
        }

        public void SetLevelPrefix(short prefix)
        {
            this.currentLevelPrefix = prefix;
        }

        protected internal bool SetMasterClient(int playerId, bool sync)
        {
            if (this.mMasterClient == null || this.mMasterClient.ID == -1 || !this.mActors.ContainsKey(playerId))
            {
                return false;
            }
            if (sync)
            {
                Hashtable customEventContent = new Hashtable
                {
                    { (byte)1, playerId }
                };
                if (!this.OpRaiseEvent(208, customEventContent, true, null))
                {
                    return false;
                }
            }
            this.hasSwitchedMC = true;
            this.mMasterClient = this.mActors[playerId];
            SendMonoMessage(PhotonNetworkingMessage.OnMasterClientSwitched, mMasterClient);
            return true;
        }

        public void SetReceivingEnabled(int group, bool enabled)
        {
            if (group <= 0)
            {
                Debug.LogError("Error: PhotonNetwork.SetReceivingEnabled was called with an illegal group number: " + group + ". The group number should be at least 1.");
            }
            else if (enabled)
            {
                if (!this.allowedReceivingGroups.Contains(group))
                {
                    this.allowedReceivingGroups.Add(group);
                    byte[] groupsToAdd = new byte[] { (byte) group };
                    this.OpChangeGroups(null, groupsToAdd);
                }
            }
            else if (this.allowedReceivingGroups.Contains(group))
            {
                this.allowedReceivingGroups.Remove(group);
                byte[] groupsToRemove = new byte[] { (byte) group };
                this.OpChangeGroups(groupsToRemove, null);
            }
        }

        public void SetReceivingEnabled(int[] enableGroups, int[] disableGroups)
        {
            List<byte> list = new List<byte>();
            List<byte> list2 = new List<byte>();
            if (enableGroups != null)
            {
                for (int i = 0; i < enableGroups.Length; i++)
                {
                    int item = enableGroups[i];
                    if (item <= 0)
                    {
                        Debug.LogError("Error: PhotonNetwork.SetReceivingEnabled was called with an illegal group number: " + item + ". The group number should be at least 1.");
                    }
                    else if (!this.allowedReceivingGroups.Contains(item))
                    {
                        this.allowedReceivingGroups.Add(item);
                        list.Add((byte) item);
                    }
                }
            }
            if (disableGroups != null)
            {
                for (int j = 0; j < disableGroups.Length; j++)
                {
                    int num4 = disableGroups[j];
                    if (num4 <= 0)
                    {
                        Debug.LogError("Error: PhotonNetwork.SetReceivingEnabled was called with an illegal group number: " + num4 + ". The group number should be at least 1.");
                    }
                    else if (list.Contains((byte) num4))
                    {
                        Debug.LogError("Error: PhotonNetwork.SetReceivingEnabled disableGroups contains a group that is also in the enableGroups: " + num4 + ".");
                    }
                    else if (this.allowedReceivingGroups.Contains(num4))
                    {
                        this.allowedReceivingGroups.Remove(num4);
                        list2.Add((byte) num4);
                    }
                }
            }
            this.OpChangeGroups(list2.Count <= 0 ? null : list2.ToArray(), list.Count <= 0 ? null : list.ToArray());
        }

        public void SetSendingEnabled(int group, bool enabled)
        {
            if (!enabled)
            {
                this.blockSendingGroups.Add(group);
            }
            else
            {
                this.blockSendingGroups.Remove(group);
            }
        }

        public void SetSendingEnabled(int[] enableGroups, int[] disableGroups)
        {
            if (enableGroups != null)
            {
                foreach (int num2 in enableGroups)
                {
                    if (this.blockSendingGroups.Contains(num2))
                    {
                        this.blockSendingGroups.Remove(num2);
                    }
                }
            }
            if (disableGroups != null)
            {
                foreach (int num4 in disableGroups)
                {
                    if (!this.blockSendingGroups.Contains(num4))
                    {
                        this.blockSendingGroups.Add(num4);
                    }
                }
            }
        }

        private void StoreInstantiationData(int instantiationId, object[] instantiationData)
        {
            this.tempInstantiationData[instantiationId] = instantiationData;
        }

        public bool WebRpc(string uriPath, object parameters)
        {
            Dictionary<byte, object> customOpParameters = new Dictionary<byte, object>
            {
                { 209, uriPath },
                { 208, parameters }
            };
            return this.OpCustom(219, customOpParameters, true);
        }

        public List<Region> AvailableRegions { get; protected internal set; }

        public CloudRegionCode CloudRegion { get; protected internal set; }

        public AuthenticationValues CustomAuthenticationValues { get; set; }

        protected internal int FriendsListAge
        {
            get
            {
                return this.isFetchingFriends || this.friendListTimestamp == 0 ? 0 : Environment.TickCount - this.friendListTimestamp;
            }
        }

        public bool IsAuthorizeSecretAvailable
        {
            get
            {
                return false;
            }
        }

        public bool IsUsingNameServer { get; protected internal set; }

        public TypedLobby lobby { get; set; }

        protected internal string mAppVersionPun
        {
            get
            {
                return string.Format("{0}_{1}", this.mAppVersion, "1.28");
            }
        }

        public string MasterServerAddress { get; protected internal set; }

        public Room mCurrentGame
        {
            get
            {
                if (this.mRoomToGetInto != null && this.mRoomToGetInto.IsLocalClientInside)
                {
                    return this.mRoomToGetInto;
                }
                return null;
            }
        }

        public int mGameCount { get; internal set; }

        public string mGameserver { get; internal set; }

        public Player mLocalActor { get; internal set; }

        public int mPlayersInRoomsCount { get; internal set; }

        public int mPlayersOnMasterCount { get; internal set; }

        public int mQueuePosition { get; internal set; }

        internal RoomOptions mRoomOptionsForCreate { get; set; }

        internal TypedLobby mRoomToEnterLobby { get; set; }

        internal Room mRoomToGetInto { get; set; }

        public string PlayerName
        {
            get
            {
                return this.playername;
            }
            set
            {
                if (!string.IsNullOrEmpty(value) && !value.Equals(this.playername))
                {
                    if (this.mLocalActor != null)
                    {
                        this.mLocalActor.FriendName = value;
                    }
                    this.playername = value;
                    if (this.mCurrentGame != null)
                    {
                        this.SendPlayerName();
                    }
                }
            }
        }

        protected internal ServerConnection server { get; private set; }

        public ClientState states { get; internal set; }
    }
}

