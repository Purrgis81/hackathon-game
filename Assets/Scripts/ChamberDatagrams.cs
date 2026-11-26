using System;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;

public readonly struct ChamberConnection
{
    public readonly NetworkConnection Udp;
    public readonly uint Steam;
    public ChamberConnection(NetworkConnection udp){Udp=udp;Steam=0;}
    public ChamberConnection(uint steam){Udp=default;Steam=steam;}
    public bool IsCreated=>Udp.IsCreated||Steam!=0;
}

public interface IChamberWire : IDisposable
{
    bool Created {get;}
    ChamberConnection Accept();
    void Update();
    NetworkEvent.Type Receive(ChamberConnection connection,out byte[] bytes);
    bool Send(ChamberConnection connection,byte[] bytes,bool ordered);
    void Flush();
    void Disconnect(ChamberConnection connection);
}

// A reliable channel is reserved for room membership. Motion uses independent UDP datagrams.
public sealed class ChamberDatagrams : IChamberWire
{
    NetworkDriver driver;
    NetworkPipeline reliable;
    public bool Created=>driver.IsCreated;
    public void Open(bool server,ushort port,RelayServerData? relay=null){
        var settings=new NetworkSettings();
        settings.WithNetworkConfigParameters(connectTimeoutMS:500,maxConnectAttempts:12,disconnectTimeoutMS:12000,
            heartbeatTimeoutMS:500,sendQueueCapacity:256,receiveQueueCapacity:256);
        if(relay.HasValue){var data=relay.Value;settings.WithRelayParameters(ref data);}
        driver=NetworkDriver.Create(settings);settings.Dispose();
        reliable=driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
        if(driver.Bind(NetworkEndpoint.AnyIpv4.WithPort(port))!=0)throw new InvalidOperationException("UDP 포트를 열지 못했습니다.");
        if(server && driver.Listen()!=0)throw new InvalidOperationException("UDP 방을 열지 못했습니다.");
    }
    public ChamberConnection Connect(NetworkEndpoint endpoint)=>new ChamberConnection(driver.Connect(endpoint));
    public void Update(){driver.ScheduleUpdate().Complete();}
    public ChamberConnection Accept()=>new ChamberConnection(driver.Accept());
    public NetworkEvent.Type Receive(ChamberConnection connection,out byte[] bytes){
        var kind=driver.PopEventForConnection(connection.Udp,out var reader);
        bytes=null;
        if(kind==NetworkEvent.Type.Data){
            bytes=new byte[reader.Length];
            for(int i=0;i<bytes.Length;i++)bytes[i]=reader.ReadByte();
        }
        return kind;
    }
    public bool Send(ChamberConnection connection,byte[] bytes,bool ordered){
        var pipeline=ordered?reliable:NetworkPipeline.Null;
        int result=driver.BeginSend(pipeline,connection.Udp,out var writer);
        if(result!=0)return false;
        for(int i=0;i<bytes.Length;i++)writer.WriteByte(bytes[i]);
        return driver.EndSend(writer)>=0;
    }
    public void Flush(){driver.ScheduleFlushSend(default).Complete();}
    public void Disconnect(ChamberConnection connection){if(driver.IsCreated)driver.Disconnect(connection.Udp);}
    public void Dispose(){if(driver.IsCreated)driver.Dispose();}
}
