using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using UnityEngine;

public sealed class ChamberLan : IDisposable
{
    public const int Port=47737,Capacity=5,TickRate=120;
    const float Step=1f/TickRate;
    const int HistorySize=4096,Window=72;
    enum Mode { Offline,Menu,Allocating,Connecting,Room,Play }
    enum Kind : byte { Hello=1,Lobby,Start,Profile,Input,Frames,Ping,Pong,Leave }
    sealed class Peer
    {
        public NetworkConnection Connection;
        public int Slot,ReceivedInput,Ack;
        public bool Ready;
        public ChamberProfile Profile;
        public readonly Queue<byte[]> Control=new Queue<byte[]>();
        public readonly SortedDictionary<int,byte> Inputs=new SortedDictionary<int,byte>();
    }
    struct Frame { public int Number,Keys,Hash; }
    readonly ChamberGame game;
    readonly ChamberProfile[] roster=new ChamberProfile[Capacity];
    readonly List<Peer> peers=new List<Peer>();
    readonly Queue<byte>[] inputs=new Queue<byte>[Capacity];
    readonly Vector2Int[] keys=new Vector2Int[Capacity];
    readonly Frame[] history=new Frame[HistorySize];
    readonly SortedDictionary<int,Frame> pending=new SortedDictionary<int,Frame>();
    readonly SortedDictionary<int,byte> commands=new SortedDictionary<int,byte>();
    ChamberDatagrams wire;
    Mode mode;
    bool host,previousBackground,editing,online;
    string address="",invite="",message="",code="",draftName;
    int draftColor,count=1,sequence,received,sent,epoch;
    float accumulator,lastSend,lastPing,connectingAt,playClock;
    public ChamberProfile Local {get;private set;}
    public ChamberProfile Profile(int index)=>roster[index];
    public int Slot {get;private set;}
    public int PingMs {get;private set;}
    public int BufferedTicks=>received-sequence;
    public bool Busy=>mode!=Mode.Offline;
    public bool Playing=>mode==Mode.Play;
    public bool Guest=>Playing&&!host;
    public bool Editing=>editing;
    public ChamberLan(ChamberGame owner){
        game=owner;Local=ChamberProfile.Load();
        for(int i=0;i<Capacity;i++)inputs[i]=new Queue<byte>();
    }
    public void OpenFromCommandLine(){
        var args=Environment.GetCommandLineArgs();int join=Array.IndexOf(args,"-chamber-join");
        int relay=Array.IndexOf(args,"-chamber-code");
        if(Array.IndexOf(args,"-chamber-online-host")>=0)OnlineRoom(true);
        else if(relay>=0&&relay+1<args.Length){code=args[relay+1];OnlineRoom(false);}
        else if(join>=0&&join+1<args.Length)JoinRoom(args[join+1]);
        else if(Array.IndexOf(args,"-chamber-host")>=0)HostRoom();
    }
    public void OpenMenu(){
        if(Busy)return;
        previousBackground=Application.runInBackground;Application.runInBackground=true;mode=Mode.Menu;
    }
    public void HostRoom(){
        OpenMenu();
        try{
            host=true;online=false;Slot=0;roster[0]=Local;count=1;message="";
            wire=new ChamberDatagrams();wire.Open(true,Port);mode=Mode.Room;
            invite=string.Join(" / ",NetworkInterface.GetAllNetworkInterfaces()
                .Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback)
                .OrderByDescending(n=>n.NetworkInterfaceType==NetworkInterfaceType.Wireless80211)
                .SelectMany(n=>n.GetIPProperties().UnicastAddresses)
                .Where(a=>a.Address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(a=>a.Address.ToString()).Distinct());
        }catch(Exception e){Fail(e.Message);}
    }
    public void JoinRoom(string ip){
        OpenMenu();
        if(!NetworkEndpoint.TryParse(ip.Trim(),Port,out var endpoint)){message="방장의 IPv4 주소를 입력하세요.";return;}
        try{
            host=false;online=false;wire=new ChamberDatagrams();wire.Open(false,0);
            peers.Add(new Peer{Connection=wire.Connect(endpoint)});connectingAt=Time.realtimeSinceStartup;mode=Mode.Connecting;message="";
        }catch(Exception e){Fail(e.Message);}
    }
    public async void OnlineRoom(bool create){
        OpenMenu();
        if(mode!=Mode.Menu)return;
        mode=Mode.Allocating;online=true;connectingAt=Time.realtimeSinceStartup;int generation=++epoch;
        try{
            RelayServerData data;
            if(create){var allocation=await ChamberRelay.Host();data=allocation.Data;invite=allocation.Code;}
            else data=await ChamberRelay.Join(code);
            if(epoch!=generation)return;
            host=create;Slot=0;count=1;roster[0]=Local;
            wire=new ChamberDatagrams();wire.Open(create,0,data);
            if(create){mode=Mode.Room;Debug.Log("Chamber Relay: room code "+invite);}
            else{peers.Add(new Peer{Connection=wire.Connect(data.Endpoint)});mode=Mode.Connecting;}
            connectingAt=Time.realtimeSinceStartup;message="";
        }catch(Exception e){if(epoch==generation)Fail(e.Message);}
    }
    static byte Encode(Vector2Int d)=>d==Vector2Int.up?(byte)1:d==Vector2Int.right?(byte)2:d==Vector2Int.down?(byte)3:d==Vector2Int.left?(byte)4:(byte)0;
    static Vector2Int Decode(int d)=>d==1?Vector2Int.up:d==2?Vector2Int.right:d==3?Vector2Int.down:d==4?Vector2Int.left:Vector2Int.zero;
    static int Read(byte[] b,int at)=>BitConverter.ToInt32(b,at);
    static void Put(byte[] b,int at,int value){for(int n=0;n<4;n++)b[at+n]=(byte)(value>>(8*n));}
    static byte[] Packet(Kind kind,int size=16,int a=0,int b=0,int c=0){
        var bytes=new byte[size];bytes[0]=83;bytes[1]=67;bytes[2]=6;bytes[3]=(byte)kind;
        Put(bytes,4,a);Put(bytes,8,b);Put(bytes,12,c);return bytes;
    }
    static byte[] Identity(Kind kind,int slot,ChamberProfile profile){
        var bytes=Packet(kind,56,slot,profile.Swatch);
        Put(bytes,12,System.Text.Encoding.UTF8.GetBytes(profile.Name??"",0,(profile.Name??"").Length,bytes,16));return bytes;
    }
    static ChamberProfile ReadIdentity(byte[] bytes){
        int length=Read(bytes,12);
        if(bytes.Length!=56||length<0||length>40)throw new InvalidOperationException("프로필 메시지 오류");
        return ChamberProfile.Create(System.Text.Encoding.UTF8.GetString(bytes,16,length),Read(bytes,8));
    }
    void UpdateLobby(){
        count=1;foreach(var p in peers)if(p.Ready){p.Slot=count++;roster[p.Slot]=p.Profile;}
        foreach(var p in peers)if(p.Ready){
            p.Control.Enqueue(Packet(Kind.Lobby,a:p.Slot,b:count));
            for(int i=0;i<count;i++)p.Control.Enqueue(Identity(Kind.Profile,i,roster[i]));
        }
    }
    public void StartMatch(){
        if(!host||mode!=Mode.Room||count<2||peers.Any(p=>!p.Ready))return;
        int seed=Guid.NewGuid().GetHashCode();
        foreach(var p in peers)p.Control.Enqueue(Packet(Kind.Start,a:seed,b:count));
        Begin(seed);
    }
    void Begin(int seed){
        sequence=received=sent=0;accumulator=playClock=0;pending.Clear();commands.Clear();
        foreach(var q in inputs)q.Clear();
        mode=Mode.Play;game.BeginDuel(seed,count);
        Debug.Log($"Chamber UDP: {count} players ready, slot {Slot+1}, {TickRate} Hz");
    }
    public void Poll(float dt,Vector2Int input){
        try{
            if(mode==Mode.Allocating){
                if(Time.realtimeSinceStartup-connectingAt>25)Fail("온라인 방 연결 시간이 초과되었습니다.");return;
            }
            if(wire==null)return;
            wire.Update();
            NetworkConnection accepted;
            while(host&&(accepted=wire.Accept()).IsCreated){
                if(mode==Mode.Room&&peers.Count<Capacity-1)peers.Add(new Peer{Connection=accepted});
                else wire.Disconnect(accepted);
            }
            for(int i=peers.Count-1;i>=0;i--)ReadPeer(peers[i]);
            if(wire==null)return;
            if(mode==Mode.Connecting&&Time.realtimeSinceStartup-connectingAt>12)throw new InvalidOperationException("UDP 접속 시간이 초과되었습니다.");
            if(host&&mode==Mode.Room&&count==Capacity)StartMatch();
            if(Playing){
                byte key=Encode(input);
                if(key!=0&&!game.RoundFinished){
                    if(host){if(inputs[0].Count<8)inputs[0].Enqueue(key);}
                    else if(commands.Count<8){commands.Add(++sent,key);game.Anticipate(Slot,input);}
                }
                if(host)AdvanceHost(dt);
                else AdvanceGuest(dt);
                if(Time.realtimeSinceStartup-lastSend>=1f/60||key!=0){
                    lastSend=Time.realtimeSinceStartup;
                    if(host)foreach(var p in peers)SendHistory(p);
                    else SendCommands();
                }
            }
            if(!host&&peers.Count>0&&Time.realtimeSinceStartup-lastPing>1){
                lastPing=Time.realtimeSinceStartup;wire.Send(peers[0].Connection,Packet(Kind.Ping,a:Environment.TickCount),false);
            }
            foreach(var p in peers){
                while(p.Control.Count>0){
                    if(!wire.Send(p.Connection,p.Control.Peek(),true))break;
                    p.Control.Dequeue();
                }
            }
            wire.Flush();
        }catch(Exception e){Fail(e.Message);}
    }
    void AdvanceHost(float dt){
        accumulator+=Mathf.Min(dt,.1f);
        while(accumulator>=Step){
            accumulator-=Step;int packed=0;
            for(int i=0;i<count;i++){
                byte k=inputs[i].Count>0?inputs[i].Dequeue():(byte)0;packed|=k<<(i*3);keys[i]=Decode(k);
            }
            int hash=game.DuelTick(keys,Step);sequence++;
            history[sequence%HistorySize]=new Frame{Number=sequence,Keys=packed,Hash=hash};
            if(game.RoundFinished)foreach(var q in inputs)q.Clear();
        }
    }
    void AdvanceGuest(float dt){
        if(sequence==0&&received<3)return;
        // A small buffer absorbs arrival jitter. Catch up gradually after a brief outage.
        float rate=BufferedTicks>12?1.15f:BufferedTicks>5?1.04f:1;
        playClock=Mathf.Min(playClock+Mathf.Min(dt,.1f)*rate,.1f);
        int steps=0;
        while(playClock>=Step&&pending.TryGetValue(sequence+1,out var frame)&&steps++<16){
            for(int i=0;i<count;i++)keys[i]=Decode((frame.Keys>>(i*3))&7);
            if(keys[Slot]!=Vector2Int.zero)game.ConfirmLocalInput(Slot);
            int digest=game.DuelTick(keys,Step);
            if(digest!=frame.Hash)throw new InvalidOperationException($"게임 판정이 달라졌습니다. 틱 {frame.Number} · 방에 다시 참가하세요.");
            pending.Remove(++sequence);playClock-=Step;
        }
    }
    void SendCommands(){
        var bytes=Packet(Kind.Input,16+commands.Count*8,a:received,b:commands.Count);
        int at=16;foreach(var command in commands){Put(bytes,at,command.Key);Put(bytes,at+4,command.Value);at+=8;}
        wire.Send(peers[0].Connection,bytes,false);
    }
    void SendHistory(Peer p){
        int start=p.Ack+1;
        if(sequence-start>=HistorySize)throw new InvalidOperationException("참가자의 연결이 너무 오래 끊겼습니다.");
        int end=Mathf.Min(sequence,start+Window-1),length=Mathf.Max(0,end-start+1);
        var bytes=Packet(Kind.Frames,16+length*12,a:start,b:length,c:p.ReceivedInput);
        for(int i=0;i<length;i++){
            var frame=history[(start+i)%HistorySize];Put(bytes,16+i*12,frame.Number);Put(bytes,20+i*12,frame.Keys);Put(bytes,24+i*12,frame.Hash);
        }
        wire.Send(p.Connection,bytes,false);
    }
    void ReadPeer(Peer p){
        NetworkEvent.Type type;
        while((type=wire.Receive(p.Connection,out var bytes))!=NetworkEvent.Type.Empty){
            if(type==NetworkEvent.Type.Connect){
                if(!host)p.Control.Enqueue(Identity(Kind.Hello,0,Local));continue;
            }
            if(type==NetworkEvent.Type.Disconnect){
                peers.Remove(p);
                if(Playing||!host)throw new InvalidOperationException("참가자와의 연결이 종료되었습니다.");
                UpdateLobby();return;
            }
            if(type!=NetworkEvent.Type.Data)continue;
            if(bytes.Length<16||bytes[0]!=83||bytes[1]!=67||bytes[2]!=6)throw new InvalidOperationException("같은 버전의 UDP 게임을 사용하세요.");
            var kind=(Kind)bytes[3];int a=Read(bytes,4),b=Read(bytes,8),c=Read(bytes,12);
            if(kind==Kind.Ping){if(host)wire.Send(p.Connection,Packet(Kind.Pong,a:a),false);continue;}
            if(kind==Kind.Pong&&!host){PingMs=Mathf.Clamp(unchecked(Environment.TickCount-a),0,9999);continue;}
            if(host&&mode==Mode.Room&&!p.Ready&&kind==Kind.Hello){p.Profile=ReadIdentity(bytes);p.Ready=true;UpdateLobby();continue;}
            if(!host&&mode==Mode.Connecting&&kind==Kind.Lobby&&a>=1&&a<b&&b<=Capacity){Slot=a;count=b;mode=Mode.Room;continue;}
            if(!host&&mode==Mode.Room&&kind==Kind.Lobby&&a>=1&&a<b&&b<=Capacity){Slot=a;count=b;continue;}
            if(!host&&mode==Mode.Room&&kind==Kind.Profile&&a>=0&&a<count){roster[a]=ReadIdentity(bytes);continue;}
            if(!host&&mode==Mode.Room&&kind==Kind.Start&&b==count){Begin(a);continue;}
            if(host&&Playing&&kind==Kind.Input){
                if(a<0||a>sequence||b<0||b>8||bytes.Length!=16+b*8)continue;
                p.Ack=Mathf.Max(p.Ack,a);
                for(int i=0;i<b;i++){
                    int n=Read(bytes,16+i*8),key=Read(bytes,20+i*8);
                    if(n>p.ReceivedInput&&n<=p.ReceivedInput+8&&key>=1&&key<=4)p.Inputs[n]=(byte)key;
                }
                while(p.Inputs.TryGetValue(p.ReceivedInput+1,out byte key)){
                    p.Inputs.Remove(++p.ReceivedInput);
                    if(!game.RoundFinished&&inputs[p.Slot].Count<8)inputs[p.Slot].Enqueue(key);
                }
                continue;
            }
            if(!host&&Playing&&kind==Kind.Frames){
                if(a<1||b<0||b>Window||bytes.Length!=16+b*12)continue;
                foreach(int n in commands.Keys.Where(n=>n<=c).ToArray())commands.Remove(n);
                for(int i=0;i<b;i++){
                    int n=Read(bytes,16+i*12);
                    if(n!=a+i||n<=sequence||n>sequence+HistorySize)continue;
                    pending[n]=new Frame{Number=n,Keys=Read(bytes,20+i*12),Hash=Read(bytes,24+i*12)};
                }
                while(pending.ContainsKey(received+1))received++;
                continue;
            }
        }
    }
    void Close(){
        epoch++;wire?.Dispose();wire=null;peers.Clear();commands.Clear();pending.Clear();
        foreach(var q in inputs)q.Clear();count=1;Slot=0;host=false;PingMs=0;
    }
    void Fail(string text){
        bool played=Playing;Close();mode=Mode.Menu;message=text;Debug.LogWarning("Chamber UDP: "+text);
        if(played)game.EndDuel();
    }
    public void Leave(){bool played=Playing;Dispose();if(played)game.EndDuel();}
    public void Dispose(){if(Busy)Application.runInBackground=previousBackground;Close();mode=Mode.Offline;message="";}
    public void Draw(float width,float height,bool canOpen){
        if(!Busy){
            if(!canOpen)return;
            if(editing){
                float pw=Mathf.Min(width-24,320);
                GUILayout.BeginArea(new Rect((width-pw)/2,(height-220)/2,pw,220),GUI.skin.box);
                GUILayout.Label("캐릭터 설정");GUILayout.Label("이름 (최대 12자)");
                draftName=GUILayout.TextField(draftName,12);GUILayout.Label("캐릭터 색상");
                draftColor=ChamberProfile.ChooseColor(draftColor);
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("저장")){Local=ChamberProfile.Create(draftName,draftColor);Local.Save();game.ApplyProfiles();editing=false;}
                if(GUILayout.Button("취소"))editing=false;
                GUILayout.EndHorizontal();GUILayout.EndArea();
            }else{
                if(GUI.Button(new Rect((width-200)/2,height-160,200,34),"캐릭터 설정")){draftName=Local.Name;draftColor=Local.Swatch;editing=true;}
                if(GUI.Button(new Rect((width-200)/2,height-119,200,34),"친구와 UDP 대전"))OpenMenu();
            }
            return;
        }
        if(Playing){if(GUI.Button(new Rect(width-86,64,70,27),"방 나가기"))Leave();return;}
        float w=Mathf.Min(420,width-20);
        GUILayout.BeginArea(new Rect((width-w)/2,(height-390)/2,w,390),GUI.skin.box);
        GUILayout.Label("친구와 UDP 대전 · 2~5명");
        if(mode==Mode.Menu){
            if(GUILayout.Button("온라인 방 만들기 · 초대 코드",GUILayout.Height(33)))OnlineRoom(true);
            code=GUILayout.TextField(code,12);
            if(GUILayout.Button("초대 코드로 참가"))OnlineRoom(false);
            if(string.IsNullOrEmpty(Application.cloudProjectId))
                GUILayout.Label("온라인 기능은 Unity Cloud 프로젝트 연결 후 사용할 수 있습니다.");
            GUILayout.Space(12);
            if(GUILayout.Button("직접 연결 방 만들기"))HostRoom();
            GUILayout.Label("방장 IP");address=GUILayout.TextField(address,45);
            if(GUILayout.Button("IP로 직접 참가"))JoinRoom(address);
            GUILayout.Label(message);
        }else if(mode==Mode.Room){
            GUILayout.Label($"참가 {count}/5 · 내 캐릭터 {Slot+1}P");
            if(host){
                GUILayout.Label(online?"친구에게 알려줄 초대 코드":"직접 연결 주소");
                GUILayout.TextField(invite);
                if(count>=2&&GUILayout.Button("현재 인원으로 시작"))StartMatch();
            }
            GUILayout.Label("5명이 모이면 자동으로 시작합니다.");
        }else GUILayout.Label("방장에게 연결하는 중…");
        if(GUILayout.Button("돌아가기"))Leave();
        GUILayout.EndArea();
    }
}
