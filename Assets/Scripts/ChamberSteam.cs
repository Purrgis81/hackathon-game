#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using Unity.Networking.Transport;

// The host owns one Steam P2P listen socket per room. Steam authenticates
// both endpoints; the existing game protocol still owns seats and simulation.
public sealed class ChamberSteam : IChamberWire
{
    readonly HashSet<HSteamNetConnection> connections = new HashSet<HSteamNetConnection>();
    readonly HashSet<HSteamNetConnection> connected = new HashSet<HSteamNetConnection>();
    readonly HashSet<HSteamNetConnection> closed = new HashSet<HSteamNetConnection>();
    readonly Queue<ChamberConnection> incoming = new Queue<ChamberConnection>();
    readonly IntPtr[] message = new IntPtr[1];
    Callback<SteamNetConnectionStatusChangedCallback_t> status;
    HSteamListenSocket listener;
    public bool Created { get; private set; }

    public void Open()
    {
        if (!SteamAPI.Init())
            throw new InvalidOperationException("Steam에 로그인하고 게임을 다시 열어 주세요. 테스트 빌드는 steam_appid.txt가 필요합니다.");
        Created = true;
        if (!SteamUser.BLoggedOn()) throw new InvalidOperationException("Steam을 온라인 상태로 전환해 주세요.");
        status = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatus);
        SteamNetworkingUtils.InitRelayNetworkAccess();
    }

    public string Host()
    {
        int port = new Random(Guid.NewGuid().GetHashCode()).Next(1,1000);
        listener = SteamNetworkingSockets.CreateListenSocketP2P(port,0,null);
        if (listener == HSteamListenSocket.Invalid) throw new InvalidOperationException("Steam 방을 열지 못했습니다.");
        return $"SC-{SteamUser.GetSteamID().m_SteamID}-{port}";
    }

    public ChamberConnection Join(string code)
    {
        var parts = code.Trim().Split('-');
        if (parts.Length!=3 || !parts[0].Equals("SC",StringComparison.OrdinalIgnoreCase) ||
            !ulong.TryParse(parts[1],out ulong id) || !new CSteamID(id).IsValid() ||
            !int.TryParse(parts[2],out int port) || port<1 || port>=1000)
            throw new InvalidOperationException("방장이 복사한 SC- 방 코드 전체를 입력해 주세요.");
        if (id==SteamUser.GetSteamID().m_SteamID) throw new InvalidOperationException("서로 다른 Steam 계정으로 참가해 주세요.");
        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID64(id);
        var handle = SteamNetworkingSockets.ConnectP2P(ref identity,port,0,null);
        if (handle==HSteamNetConnection.Invalid) throw new InvalidOperationException("Steam 연결을 시작하지 못했습니다.");
        connections.Add(handle);
        return new ChamberConnection((uint)handle);
    }

    void OnStatus(SteamNetConnectionStatusChangedCallback_t change)
    {
        var handle = change.m_hConn;
        var state = change.m_info.m_eState;
        if (state==ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting &&
            listener!=HSteamListenSocket.Invalid && change.m_info.m_hListenSocket==listener)
        {
            if (connections.Count>=ChamberLan.Capacity-1 || SteamNetworkingSockets.AcceptConnection(handle)!=EResult.k_EResultOK)
            {
                SteamNetworkingSockets.CloseConnection(handle,0,"Room unavailable",false);
                return;
            }
            connections.Add(handle);
            incoming.Enqueue(new ChamberConnection((uint)handle));
        }
        if (!connections.Contains(handle)) return;
        if (state==ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected) connected.Add(handle);
        if (state==ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer ||
            state==ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally) closed.Add(handle);
    }

    public void Update()=>SteamAPI.RunCallbacks();
    public ChamberConnection Accept()=>incoming.Count>0 ? incoming.Dequeue() : default;
    public NetworkEvent.Type Receive(ChamberConnection peer,out byte[] bytes)
    {
        bytes=null;
        var handle=(HSteamNetConnection)peer.Steam;
        if (closed.Contains(handle)){Disconnect(peer);return NetworkEvent.Type.Disconnect;}
        if (connected.Remove(handle)) return NetworkEvent.Type.Connect;
        if (SteamNetworkingSockets.ReceiveMessagesOnConnection(handle,message,1)!=1) return NetworkEvent.Type.Empty;
        try {
            var packet=SteamNetworkingMessage_t.FromIntPtr(message[0]);
            if(packet.m_cbSize<16 || packet.m_cbSize>1200){Disconnect(peer);return NetworkEvent.Type.Disconnect;}
            bytes=new byte[packet.m_cbSize];
            Marshal.Copy(packet.m_pData,bytes,0,bytes.Length);
            return NetworkEvent.Type.Data;
        } finally { SteamNetworkingMessage_t.Release(message[0]); }
    }

    public bool Send(ChamberConnection peer,byte[] bytes,bool ordered)
    {
        int flags=ordered ? Constants.k_nSteamNetworkingSend_ReliableNoNagle : Constants.k_nSteamNetworkingSend_UnreliableNoNagle;
        var pin=GCHandle.Alloc(bytes,GCHandleType.Pinned);
        try {
            return SteamNetworkingSockets.SendMessageToConnection((HSteamNetConnection)peer.Steam,
                pin.AddrOfPinnedObject(),(uint)bytes.Length,flags,out _)==EResult.k_EResultOK;
        } finally {pin.Free();}
    }
    // Both send modes disable Nagle and are handed straight to Steam.
    public void Flush(){}
    public void Disconnect(ChamberConnection peer)
    {
        var handle=(HSteamNetConnection)peer.Steam;
        if(connections.Remove(handle))SteamNetworkingSockets.CloseConnection(handle,0,"Left room",false);
        connected.Remove(handle);closed.Remove(handle);
    }
    public void Dispose()
    {
        if(!Created)return;
        status?.Dispose();
        foreach(var handle in connections) SteamNetworkingSockets.CloseConnection(handle,0,"Room closed",false);
        connections.Clear();
        if(listener!=HSteamListenSocket.Invalid)SteamNetworkingSockets.CloseListenSocket(listener);
        SteamAPI.Shutdown();Created=false;
    }
}
#endif
