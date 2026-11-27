using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using UnityEngine;

public sealed partial class ChamberLan : IDisposable
{
    public const int Port=47737,Capacity=4,TickRate=120;
    const float Step=1f/TickRate;
    const int HistorySize=4096,Window=72;
    enum Mode { Offline,Menu,Allocating,Connecting,Room,Play }
    const byte ProtocolVersion=15;
    enum Kind : byte { Hello=1,Lobby,Start,Profile,Input,Frames,Ping,Pong,Leave,SetReady,Bots }
    sealed class Peer
    {
        public ChamberConnection Connection;
        public int Slot,ReceivedInput,Ack,Applied;
        public bool HasProfile,MatchReady;
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
    IChamberWire wire;
    Mode mode;
    bool host,previousBackground,editing,online;
    string address="",invite="",message="",code="";
    string steamCode="";
    int transportTab;
    int count=1,sequence,received,sent,epoch;
    ChamberWardrobe wardrobe;
    float accumulator,lastSend,lastPing,connectingAt,playClock;
    const int MinReserve=8,MaxReserve=24;
    float lastHistoryAt,reserveTicks;
    public ChamberProfile Local {get;private set;}
    public ChamberProfile Profile(int index)=>roster[index];
    public int Slot {get;private set;}
    public int PingMs {get;private set;}
    public int BufferedTicks=>received-sequence;
    public bool Busy=>mode!=Mode.Offline;
    public bool Playing=>mode==Mode.Play;
    public bool ResultsReady {get;private set;}
    public bool InRoom=>mode==Mode.Room;
    public int OccupiedSeats=>count;
    int readySeats;
    bool requestedReady;
    public bool IsSeatReady(int seat)=>seat>0&&seat<count&&(readySeats & (1<<seat))!=0;
    public bool CanStart=>host&&InRoom&&count>=2&&peers.All(p=>p.HasProfile&&p.MatchReady);
    public bool Guest=>Playing&&!host;
    public bool Editing=>editing;
    public ChamberLan(ChamberGame owner){
        game=owner;Local=ChamberProfile.Load();
        for(int i=0;i<Capacity;i++)inputs[i]=new Queue<byte>();
    }
    public void OpenFromCommandLine(){
        var args=Environment.GetCommandLineArgs();int join=Array.IndexOf(args,"-chamber-join");
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        int steamJoin=Array.IndexOf(args,"-chamber-steam-code");
        if(Array.IndexOf(args,"-chamber-steam-host")>=0){SteamRoom(true);return;}
        if(steamJoin>=0&&steamJoin+1<args.Length){steamCode=args[steamJoin+1];SteamRoom(false);return;}
#endif
        int relay=Array.IndexOf(args,"-chamber-code");
        if(Array.IndexOf(args,"-chamber-online-host")>=0)OnlineRoom(true);
        else if(relay>=0&&relay+1<args.Length){code=args[relay+1];OnlineRoom(false);}
        else if(join>=0&&join+1<args.Length)JoinRoom(args[join+1]);
        else if(Array.IndexOf(args,"-chamber-host")>=0)HostRoom();
    }
    public void OpenMenu(){
        if(Busy)return;
        ResultsReady=false;
        previousBackground=Application.runInBackground;Application.runInBackground=true;mode=Mode.Menu;
    }
    public void OpenProfile(){
        if(Busy || editing)return;
        wardrobe ??= new ChamberWardrobe(game.transform,game.MenuUI);
        wardrobe.Open(Local);
        editing=true;
    }
    public void HostRoom(){
        OpenMenu();
        try{
            host=true;online=false;Slot=0;roster[0]=Local;count=1;message="";
            OpenUdp(true,Port);mode=Mode.Room;
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
            host=false;online=false;
            peers.Add(new Peer{Connection=OpenUdp(false,0).Connect(endpoint)});connectingAt=Time.realtimeSinceStartup;mode=Mode.Connecting;message="";
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
            var udp=OpenUdp(create,0,data);
            if(create){mode=Mode.Room;Debug.Log("Chamber Relay: room code "+invite);}
            else{peers.Add(new Peer{Connection=udp.Connect(data.Endpoint)});mode=Mode.Connecting;}
            connectingAt=Time.realtimeSinceStartup;message="";
        }catch(Exception e){if(epoch==generation)Fail(e.Message);}
    }
    ChamberDatagrams OpenUdp(bool server,ushort port,RelayServerData? data=null){
        var udp=new ChamberDatagrams();wire=udp;udp.Open(server,port,data);return udp;
    }
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    void SteamRoom(bool create){
        OpenMenu();
        if(mode!=Mode.Menu)return;
        try{
            var steam=new ChamberSteam();wire=steam;steam.Open();
            host=create;online=true;Slot=0;count=1;roster[0]=Local;message="";
            if(create){
                invite=steam.Host();mode=Mode.Room;
                Debug.Log("Chamber Steam: room open");
            }else{
                peers.Add(new Peer{Connection=steam.Join(steamCode)});
                connectingAt=Time.realtimeSinceStartup;mode=Mode.Connecting;
            }
        }catch(Exception e){Fail(e.Message);}
    }
#endif
    static byte Encode(Vector2Int d)=>d==Vector2Int.up?(byte)1:d==Vector2Int.right?(byte)2:d==Vector2Int.down?(byte)3:d==Vector2Int.left?(byte)4:(byte)0;
    static Vector2Int Decode(int d)=>d==1?Vector2Int.up:d==2?Vector2Int.right:d==3?Vector2Int.down:d==4?Vector2Int.left:Vector2Int.zero;
    static int Read(byte[] b,int at)=>BitConverter.ToInt32(b,at);
    static void Put(byte[] b,int at,int value){for(int n=0;n<4;n++)b[at+n]=(byte)(value>>(8*n));}
    static byte[] Packet(Kind kind,int size=16,int a=0,int b=0,int c=0){
        var bytes=new byte[size];bytes[0]=83;bytes[1]=67;bytes[2]=ProtocolVersion;bytes[3]=(byte)kind;
        Put(bytes,4,a);Put(bytes,8,b);Put(bytes,12,c);return bytes;
    }
    static byte[] Identity(Kind kind,int slot,ChamberProfile profile){
        var bytes=Packet(kind,60,slot,profile.Swatch);
        Put(bytes,56,profile.Animal);
        Put(bytes,12,System.Text.Encoding.UTF8.GetBytes(profile.Name??"",0,(profile.Name??"").Length,bytes,16));return bytes;
    }
    static ChamberProfile ReadIdentity(byte[] bytes){
        int length=Read(bytes,12);
        if(bytes.Length!=60||length<0||length>40)throw new InvalidOperationException("프로필 메시지 오류");
        return ChamberProfile.Create(System.Text.Encoding.UTF8.GetString(bytes,16,length),Read(bytes,8),Read(bytes,56));
    }
    void UpdateLobby(bool rosterChanged=true){
        count=1;
        readySeats=0;
        foreach(var p in peers)if(p.HasProfile){
            p.Slot=count++;
            roster[p.Slot]=p.Profile;
            if(p.MatchReady)readySeats |= 1<<p.Slot;
        }
        AppendBotSeats();
        foreach(var p in peers)if(p.HasProfile){
            p.Control.Enqueue(Packet(Kind.Lobby,a:p.Slot,b:count,c:readySeats));
            p.Control.Enqueue(Packet(Kind.Bots,a:botMask,b:botCodes));
            if(rosterChanged)for(int i=0;i<count;i++)p.Control.Enqueue(Identity(Kind.Profile,i,roster[i]));
        }
    }
    public void SetReady(bool ready){
        if(!InRoom||host||requestedReady==ready)return;
        requestedReady=ready;
        peers[0].Control.Enqueue(Packet(Kind.SetReady,a:ready?1:0));
    }
    public void StartMatch(){
        if(!CanStart)return;
        int seed=Guid.NewGuid().GetHashCode();
        foreach(var p in peers)p.Control.Enqueue(Packet(Kind.Start,a:seed,b:count));
        Begin(seed);
    }
    void Begin(int seed){
        sequence=received=sent=0;accumulator=playClock=0;pending.Clear();commands.Clear();
        lastHistoryAt=0;reserveTicks=MinReserve;
        foreach(var q in inputs)q.Clear();
        mode=Mode.Play;game.BeginDuel(seed,count);
        Debug.Log($"Chamber UDP: {count} players ready, slot {Slot+1}, {TickRate} Hz");
    }
    public void Poll(float dt,Vector2Int input){
        try{
            if(practice){
                if(Playing){
                    QueueHostInput(input);
                    AdvanceHost(dt);
                    if(game.MatchFinished)CompleteMatch();
                }
                return;
            }
            if(mode==Mode.Allocating){
                if(Time.realtimeSinceStartup-connectingAt>25)Fail("온라인 방 연결 시간이 초과되었습니다.");return;
            }
            if(wire==null)return;
            wire.Update();
            ChamberConnection accepted;
            while(host&&(accepted=wire.Accept()).IsCreated){
                if(mode==Mode.Room&&peers.Count<Capacity-1)peers.Add(new Peer{Connection=accepted});
                else wire.Disconnect(accepted);
            }
            for(int i=peers.Count-1;i>=0;i--)ReadPeer(peers[i]);
            if(wire==null)return;
            if(mode==Mode.Connecting&&Time.realtimeSinceStartup-connectingAt>25)throw new InvalidOperationException("방 접속 시간이 초과되었습니다. 방 코드와 방장 연결을 확인해 주세요.");
            if(Playing){
                byte key=Encode(input);
                if(key!=0&&!game.RoundFinished&&!game.CountingDown){
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
            if(Playing && game.MatchFinished && (!host || peers.All(p=>p.Applied>=sequence))){
                if(!host){SendCommands();wire.Flush();}
                foreach(var peer in peers)wire.Disconnect(peer.Connection);
                wire.Update();
                CompleteMatch();
            }
        }catch(Exception e){Fail(e.Message);}
    }
    void CompleteMatch(){
        Dispose();
        ResultsReady=true;
    }
    void AdvanceHost(float dt){
        accumulator+=Mathf.Min(dt,.1f);
        while(accumulator>=Step&&!game.MatchFinished){
            accumulator-=Step;int packed=0;
            for(int i=0;i<count;i++){
                byte k=IsBot(i)?Encode(game.BotInput(i,Step)):inputs[i].Count>0?inputs[i].Dequeue():(byte)0;
                packed|=k<<(i*3);keys[i]=Decode(k);
            }
            int hash=game.DuelTick(keys,Step);sequence++;
            history[sequence%HistorySize]=new Frame{Number=sequence,Keys=packed,Hash=hash};
            if(game.RoundFinished)foreach(var q in inputs)q.Clear();
        }
    }
    void AdvanceGuest(float dt){
        if(sequence==0&&received<reserveTicks)return;
        // Spend the reserve during a late delivery. Refill it gradually without
        // skipping hazard transitions or running unconfirmed collision checks.
        float rate=Mathf.Clamp(1f+(BufferedTicks-reserveTicks)*.015f,.90f,1.15f);
        playClock=Mathf.Min(playClock+Mathf.Min(dt,.1f)*rate,.1f);
        int steps=0;
        while(playClock>=Step&&pending.TryGetValue(sequence+1,out var frame)&&steps++<16){
            for(int i=0;i<count;i++)keys[i]=Decode((frame.Keys>>(i*3))&7);
            int digest=game.DuelTick(keys,Step);
            if(digest!=frame.Hash)throw new InvalidOperationException($"게임 판정이 달라졌습니다. 틱 {frame.Number} · 방에 다시 참가하세요.");
            pending.Remove(++sequence);playClock-=Step;
        }
        if(BufferedTicks==0)playClock=Mathf.Min(playClock,Step);
    }
    void SendCommands(){
        var bytes=Packet(Kind.Input,16+commands.Count*8,a:received,b:commands.Count,c:sequence);
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
                if(host&&Playing&&game.MatchFinished)return;
                if(Playing||!host)throw new InvalidOperationException("참가자와의 연결이 종료되었습니다.");
                UpdateLobby();return;
            }
            if(type!=NetworkEvent.Type.Data)continue;
            if(bytes.Length<16||bytes[0]!=83||bytes[1]!=67||bytes[2]!=ProtocolVersion){
                if(host&&InRoom){wire.Disconnect(p.Connection);peers.Remove(p);UpdateLobby();return;}
                throw new InvalidOperationException("같은 버전의 UDP 게임을 사용하세요.");
            }
            var kind=(Kind)bytes[3];int a=Read(bytes,4),b=Read(bytes,8),c=Read(bytes,12);
            if(kind==Kind.Ping){if(host)wire.Send(p.Connection,Packet(Kind.Pong,a:a),false);continue;}
            if(kind==Kind.Pong&&!host){PingMs=Mathf.Clamp(unchecked(Environment.TickCount-a),0,9999);continue;}
            if(host&&InRoom&&!p.HasProfile&&kind==Kind.Hello){p.Profile=ReadIdentity(bytes);p.HasProfile=true;UpdateLobby();continue;}
            if(host&&InRoom&&p.HasProfile&&kind==Kind.SetReady&&(a==0||a==1)){
                p.MatchReady=a==1;UpdateLobby(false);continue;
            }
            if(!host&&(mode==Mode.Connecting||InRoom)&&kind==Kind.Lobby&&a>=1&&a<b&&b<=Capacity){
                Slot=a;count=b;readySeats=c;mode=Mode.Room;continue;
            }
            if(!host&&mode==Mode.Room&&kind==Kind.Profile&&a>=0&&a<count){roster[a]=ReadIdentity(bytes);continue;}
            if(!host&&InRoom&&kind==Kind.Bots&&(a&1)==0&&(a>>count)==0){botMask=a;botCodes=b;continue;}
            if(!host&&mode==Mode.Room&&kind==Kind.Start&&b==count){Begin(a);continue;}
            if(host&&Playing&&kind==Kind.Input){
                if(a<0||a>sequence||c<0||c>a||b<0||b>8||bytes.Length!=16+b*8)continue;
                p.Ack=Mathf.Max(p.Ack,a);
                p.Applied=Mathf.Max(p.Applied,c);
                for(int i=0;i<b;i++){
                    int n=Read(bytes,16+i*8),key=Read(bytes,20+i*8);
                    if(n>p.ReceivedInput&&n<=p.ReceivedInput+8&&key>=1&&key<=4)p.Inputs[n]=(byte)key;
                }
                while(p.Inputs.TryGetValue(p.ReceivedInput+1,out byte key)){
                    p.Inputs.Remove(++p.ReceivedInput);
                    if(!game.RoundFinished&&!game.CountingDown&&inputs[p.Slot].Count<8)inputs[p.Slot].Enqueue(key);
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
                int before=received;
                while(pending.ContainsKey(received+1))received++;
                if(received>before){
                    float now=Time.realtimeSinceStartup;
                    if(lastHistoryAt>0){
                        float gap=now-lastHistoryAt;
                        float needed=Mathf.Clamp(gap*TickRate+3,MinReserve,MaxReserve);
                        reserveTicks=Mathf.Max(needed,reserveTicks-gap*.65f);
                    }
                    lastHistoryAt=now;
                }
                continue;
            }
        }
    }
    void Close(){
        ClearBots();
        epoch++;wire?.Dispose();wire=null;peers.Clear();commands.Clear();pending.Clear();
        foreach(var q in inputs)q.Clear();count=1;Slot=0;host=false;PingMs=0;readySeats=0;requestedReady=false;
    }
    void Fail(string text){
        bool played=Playing;Close();mode=Mode.Menu;message=text;Debug.LogWarning("Chamber UDP: "+text);
        if(played)game.EndDuel();
    }
    public void Leave(){
        bool played=Playing;
        if(wire!=null&&wire.Created){
            foreach(var peer in peers)wire.Disconnect(peer.Connection);
            wire.Update();
        }
        Dispose();
        if(played)game.EndDuel();
    }
    public void Dispose(){wardrobe?.Dispose();wardrobe=null;editing=false;if(Busy)Application.runInBackground=previousBackground;Close();mode=Mode.Offline;message="";}
    public void Draw(float width,float height){
        if(!Busy){
            if(!editing)return;
            var action=wardrobe.Draw();
            if(action==ChamberWardrobe.Action.Save){Local=wardrobe.Profile;Local.Save();game.ApplyProfiles();}
            if(action!=ChamberWardrobe.Action.None){editing=false;wardrobe.Close();}
            return;
        }
        if(Playing){if(game.MenuUI.Button(new Rect(width-100,64,86,30),"방 나가기",PartyMenuUI.Mint,PartyMenuUI.Navy,14,MenuTone.Back))Leave();return;}
        if(InRoom){DrawRoom(width,height);return;}
        DrawConnection(width,height);
    }

    PartyMenuUI UI=>game.MenuUI;
    void DrawConnection(float width,float height){
        var ink=PartyMenuUI.Navy;var paper=PartyMenuUI.Paper;var mint=PartyMenuUI.Mint;
        PartyMenuUI.Fill(new Rect(0,0,width,height),new Color(.025f,.105f,.14f,.46f));
        const float w=402;
        bool menu=mode==Mode.Menu;
        bool relay=transportTab==1;
        bool direct=transportTab==2;
        string note=message;
        if(menu && relay && string.IsNullOrEmpty(note) && string.IsNullOrEmpty(Application.cloudProjectId))
            note="Relay를 사용하려면 Unity Cloud 프로젝트를 연결하세요.";
        float noteHeight=string.IsNullOrEmpty(note)?0:62;
        float h=menu?368+noteHeight:201;
        float x=(width-w)*.5f,y=(height-h)*.5f;
        var panel=new Rect(x,y,w,h);
        PartyMenuUI.Fill(new Rect(x+3,y+6,w,h),ink,21);
        PartyMenuUI.Fill(panel,paper,20);
        PartyMenuUI.Fill(new Rect(x,y,w,76),ink,20);
        PartyMenuUI.Fill(new Rect(x,y+53,w,23),ink);
        PartyMenuUI.ModeIcon(new Rect(x+23,y+22,32,32),1,PartyMenuUI.Gold);
        UI.Text(new Rect(x+66,y+10,210,56),"친구와 대전",28,paper,true,TextAnchor.MiddleLeft);
        PartyMenuUI.Fill(new Rect(x+w-89,y+24,66,29),PartyMenuUI.Teal,13);
        UI.Text(new Rect(x+w-89,y+24,66,29),"2–4인",16,paper,true);
        float left=x+22,inner=w-44;
        if(menu){
            float tabWidth=inner/3;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            int firstTab=0;
#else
            int firstTab=1;
            if(transportTab==0)transportTab=1;
            tabWidth=inner/2;relay=transportTab==1;
#endif
            for(int i=firstTab;i<3;i++){
                var r=new Rect(left+(i-firstTab)*tabWidth,y+91,tabWidth-4,33);
                bool selected=transportTab==i;
                if(UI.Button(r,i==0?"Steam":i==1?"Relay":"직접 연결",selected?PartyMenuUI.Teal:mint,selected?paper:ink,15,MenuTone.Focus)){
                    transportTab=i;message="";GUI.FocusControl(null);return;
                }
            }
            if(UI.Button(new Rect(left,y+143,inner,45),"방 만들기",PartyMenuUI.Gold,ink,19)){
                ConnectRoom(true);return;
            }
            PartyMenuUI.Fill(new Rect(left,y+208,inner,1),new Color32(176,199,185,255));
            UI.Text(new Rect(left,y+220,inner,25),direct?"방장의 IP 주소":relay?"Relay 초대 코드":"Steam 방 코드",16,ink,true,TextAnchor.MiddleLeft);
            var field=new Rect(left,y+250,inner-95,40);
            string value=direct?address:relay?code:steamCode;
            value=UI.Input(field,value,direct?64:relay?12:40);
            if(direct)address=value;else if(relay)code=value;else steamCode=value;
            if(UI.Button(new Rect(field.xMax+9,field.y,86,40),"참가",PartyMenuUI.Teal,paper,17,MenuTone.Confirm,!string.IsNullOrWhiteSpace(value))){
                ConnectRoom(false);return;
            }
            if(noteHeight>0){
                var style=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=14,alignment=TextAnchor.MiddleLeft};
                style.normal.textColor=ink;
                GUI.Label(new Rect(left,y+300,inner,noteHeight),note,style);
            }
        }else UI.Text(new Rect(left,y+88,inner,45),"방에 연결하는 중…",20,ink,true);
        if(UI.Button(new Rect(left,y+h-51,110,32),"돌아가기",mint,ink,15,MenuTone.Back))Leave();
    }

    void ConnectRoom(bool create){
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if(transportTab==0){SteamRoom(create);return;}
#endif
        if(transportTab==1){OnlineRoom(create);return;}
        if(create)HostRoom();else JoinRoom(address);
    }

    void DrawRoom(float width,float height){
        var ink=PartyMenuUI.Navy;var paper=PartyMenuUI.Paper;var mint=PartyMenuUI.Mint;
        float middle=width*.5f;
        float headerWidth=Mathf.Min(530,width-30);
        var header=new Rect(middle-headerWidth/2,18,headerWidth,57);
        PartyMenuUI.Fill(header,ink,17);
        UI.Text(new Rect(header.x+20,header.y,headerWidth-155,57),practice?"봇 연습 대기실":"친구 대전 대기실",26,paper,true,TextAnchor.MiddleLeft);
        var countBox=new Rect(header.xMax-114,header.y+13,98,31);
        PartyMenuUI.Fill(countBox,PartyMenuUI.Teal,12);
        UI.Text(countBox,$"참가  {count} / {Capacity}",15,paper,true);
        if(host&&!practice){
            var strip=new Rect(middle-headerWidth/2,84,headerWidth,35);
            PartyMenuUI.Fill(strip,paper,9);
            UI.Text(new Rect(strip.x+6,strip.y,92,35),online?"초대 코드":"방장 IP",14,ink,true);
            UI.Input(new Rect(strip.x+99,strip.y+2,strip.width-172,31),invite,Mathf.Max(128,invite.Length));
            if(UI.Button(new Rect(strip.xMax-64,strip.y,64,32),"복사",mint,ink,14))GUIUtility.systemCopyBuffer=invite;
        }
        float span=Mathf.Min(195,Mathf.Abs(game.LobbySeatX(1)-game.LobbySeatX(0))*width-9);
        float y=height*.59f;
        float footer=height-52;
        float cardHeight=Mathf.Min(169,footer-y-12);
        for(int i=0;i<Capacity;i++){
            float x=game.LobbySeatX(i)*width;
            bool occupied=i<count, bot=IsBot(i);
            var card=new Rect(x-span/2,y,span,cardHeight);
            PartyMenuUI.Fill(card,occupied?paper:mint,13);
            var ribbon=new Rect(card.x+9,y+9,span-18,25);
            PartyMenuUI.Fill(ribbon,occupied?(i==0?PartyMenuUI.Gold:PartyMenuUI.Teal):new Color32(174,209,198,255),8);
            var role=i==0?"방장":bot?"봇 · 준비 완료":occupied?(IsSeatReady(i)?"준비 완료":"준비 전"):"빈자리";
            UI.Text(ribbon,$"{i+1}P  ·  {role}",13,occupied&&i>0?paper:ink,true);
            string name=occupied?roster[i].Label(i):(practice?"봇 대기석":"참가 대기 중");
            UI.Text(new Rect(card.x+5,y+36,span-10,30),name,18,ink,true);
            if(occupied && !bot)
                UI.Text(new Rect(card.x+5,y+69,span-10,23),i==Slot?"내 캐릭터":"친구",14,PartyMenuUI.Muted);
            DrawBotSeat(i,x,y+68,span);
        }
        bool canAct=!host||CanStart;
        string action=host?(CanStart?"게임 시작":count<2?(practice?"봇을 추가하세요":"친구 대기 중"):"준비 대기 중"):(requestedReady?"준비 취소":"준비");
        if(UI.Button(new Rect(middle-151,footer,184,36),action,PartyMenuUI.Gold,ink,18,MenuTone.Confirm,canAct)){
            if(host)StartMatch();else SetReady(!requestedReady);
        }
        if(UI.Button(new Rect(middle+45,footer,106,36),"방 나가기",ink,paper,15,MenuTone.Back))Leave();
    }
}
