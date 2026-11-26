// Run in an isolated project: copy this file to Assets/Editor, then use
// Unity -batchmode -projectPath <copy> -executeMethod ChamberBotSmoke.Begin -logFile <log>.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Networking.Transport;

[InitializeOnLoad]
public static class ChamberBotSmoke
{
    const string Key="Codex.BotSmoke";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly List<string> Results=new List<string>();
    static double deadline;
    static ChamberBotSmoke(){if(SessionState.GetBool(Key,false)){deadline=EditorApplication.timeSinceStartup+600;EditorApplication.update+=Update;}}
    static string Report=>Path.Combine(Directory.GetParent(Application.dataPath).FullName,"bot-smoke-results.txt");
    public static void Begin(){
        PlayerSettings.companyName="CodexValidation";PlayerSettings.productName="StoneChamberBotValidation";
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/StoneChamber.unity");
        var roots=scene.GetRootGameObjects();var world=new GameObject("World 0");
        foreach(var root in roots)root.transform.SetParent(world.transform,true);
        UnityEngine.Object.Instantiate(world).name="World 1";
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    static T Get<T>(object owner,string field)=>(T)owner.GetType().GetField(field,Flags).GetValue(owner);
    static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,Flags).SetValue(owner,value);
    static object Call(object owner,string method,params object[] args)=>owner.GetType().GetMethod(method,Flags).Invoke(owner,args);
    static void EnumField(object owner,string field,string name){var f=owner.GetType().GetField(field,Flags);f.SetValue(owner,Enum.Parse(f.FieldType,name));}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Note(string message){Results.Add("PASS: "+message);File.WriteAllLines(Report,Results);Debug.Log("BOT PASS: "+message);}
    static void Update(){
        try{
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Scene startup timed out");
            if(!Application.isPlaying)return;
            var games=UnityEngine.Object.FindObjectsByType<ChamberGame>(FindObjectsSortMode.None).OrderBy(g=>g.transform.root.name).ToArray();
            if(games.Length!=2||games.Any(g=>!g.DebugAvailable))return;
            EditorApplication.update-=Update;
            foreach(var game in games)game.enabled=false;
            Behavior();EasyDifficulty();AdventurerExploration();SafetyAndBlocking();BoundedWork();Practice(games[0]);Network(games);ForecastEquivalence(games[0]);ActualBlocking(games[0]);Hazards(games[0]);NewPatterns(games[0]);Automatic(games[0]);Finish(null);
        }catch(Exception error){Finish(error);}
    }
    static ChamberBot Make(ChamberBotPersonality p=ChamberBotPersonality.Blocker,ChamberBotDifficulty d=ChamberBotDifficulty.Normal,int seed=421)
        =>new ChamberBot(new ChamberBotSettings{Personality=p,Difficulty=d},seed);
    sealed class Arena:IChamberBotArena {
        public int Width=5;public int CellCount=>Width*Width;public int ActorCount=>2;public float Now{get;set;} public float MoveSeconds=>.16f;public float SafetySeconds{get;set;}=1.2f;
        public ChamberBotActor[] actors={new ChamberBotActor{Cell=12,Target=12,Alive=true},new ChamberBotActor{Cell=0,Target=0,Alive=true}};
        public bool danger;public int forbidden=-1;public Func<int,int,float,float,float> risk;
        public Vector2Int Coordinates(int cell)=>new Vector2Int(cell%Width,cell/Width);
        public int Neighbor(int cell,Vector2Int d){var p=Coordinates(cell)+d;return p.x<0||p.x>=Width||p.y<0||p.y>=Width?-1:p.y*Width+p.x;}
        public ChamberBotActor Actor(int slot)=>actors[slot];
        public float TravelRisk(int slot,int from,int to,float delay,float duration)=>risk!=null?risk(from,to,delay,duration):to==forbidden||danger&&from==actors[slot].Cell&&to==from?1:0;
        public void Place(int slot,int cell){actors[slot].Cell=actors[slot].Target=cell;}
        public Vector2Int Step(ChamberBot bot,float seconds=.36f){Now+=seconds;var d=bot.Tick(this,0,seconds);if(d!=Vector2Int.zero)Place(0,Neighbor(actors[0].Cell,d));return d;}
    }
    static void BoundedWork(){
        foreach(int width in new[]{3,5,10})foreach(ChamberBotDifficulty difficulty in Enum.GetValues(typeof(ChamberBotDifficulty)))
        foreach(ChamberBotPersonality personality in Enum.GetValues(typeof(ChamberBotPersonality))){
            var arena=new Arena{Width=width};arena.Place(0,(width/2)*width+width/2);arena.Place(1,0);
            var bot=Make(personality,difficulty);int beam=difficulty==ChamberBotDifficulty.Easy?1:2;
            int expansions=5+(bot.SearchDepth-1)*5*beam*5;
            for(int i=0;i<20;i++){
                arena.Now+=.4f;arena.danger=(i%2)==0;bot.Tick(arena,0,.4f);
                Check(bot.LastExpandedNodes<=expansions&&bot.LastRiskChecks<=2*expansions+40,"Planner exceeded its fixed work budget");
                Check(bot.LastScoredCells<=1+2*bot.SearchRadius*(bot.SearchRadius+1),"Planner scored tiles outside its local neighborhood");
            }
        }
        Note("All personalities/difficulties on 3x3/5x5/10x10 keep fixed expansion/risk budgets and score only their local neighborhood");
    }
    static void Behavior(){
        var a=new Arena();var bot=Make();bot.Tick(a,0,1);
        a.Now=.4f;a.Place(1,1);bot.Tick(a,0,.4f);
        a.Now=.8f;a.Place(1,2);bot.Tick(a,0,.4f);
        Check(bot.RememberedMoves==2&&bot.PredictedCell(a,1)==3,"Opponent movement memory was not used");
        a.Now=5;bot.Tick(a,0,.4f);Check(bot.PredictedCell(a,1)==2,"Opponent memory did not expire");
        Note("Observed opponent heading is remembered and expires; no future input is required");
        var calm=new Arena();calm.Place(1,7);
        var cautious=Make(ChamberBotPersonality.Survivor);var bold=Make();
        cautious.Tick(calm,0,1);bold.Tick(calm,0,1);
        Check(bold.Aggression>cautious.Aggression+.5f,"Personalities have no meaningful aggression difference");
        calm.danger=true;bold.Tick(calm,0,1);Check(bold.Aggression==0,"Bot kept attacking while its own tile was dangerous");
        calm.danger=false;var opportunist=Make(ChamberBotPersonality.Opportunist);opportunist.Tick(calm,0,1);float before=opportunist.Aggression;
        calm.actors[1].Stunned=true;opportunist.Tick(calm,0,1);Check(opportunist.Aggression>before+.5f,"Opportunity did not change aggression");
        Note("Personality changes preferences; danger suppresses aggression; a stunned opponent creates an opportunity");
        foreach(ChamberBotPersonality p in Enum.GetValues(typeof(ChamberBotPersonality)))
            Check(Make(p).DecisionInterval==Make().DecisionInterval,"Personality changed skill timing");
        Check(Make(d:ChamberBotDifficulty.Hard).SearchDepth>Make(d:ChamberBotDifficulty.Easy).SearchDepth,"Skill did not change prediction depth");
        var stunned=new Arena();stunned.actors[0].Stunned=true;var recovery=Make(d:ChamberBotDifficulty.Hard);
        float first=-1,last=0;int presses=0;
        for(int i=0;i<240&&presses<6;i++)if(recovery.Tick(stunned,0,1f/120)!=Vector2Int.zero){if(first<0)first=i/120f;last=i/120f;presses++;}
        Check(presses==6&&last-first>.5f,"Bot bypassed stun mash interval");
        Note("Difficulty is independent of personality and six recovery key presses are rate limited");
        var left=new Arena();var right=new Arena();var different=new Arena();
        left.actors[1].Alive=right.actors[1].Alive=different.actors[1].Alive=false;
        var one=Make(ChamberBotPersonality.Adventurer,seed:32);var two=Make(ChamberBotPersonality.Adventurer,seed:32);var three=Make(ChamberBotPersonality.Adventurer,seed:811);
        var visited=new HashSet<int>();int backtracks=0,lastCell=-1,moves=0;bool varied=false;
        var state=UnityEngine.Random.state;float expected=UnityEngine.Random.value;UnityEngine.Random.state=state;
        for(int i=0;i<90;i++){
            int old=left.actors[0].Cell;var x=left.Step(one);var y=right.Step(two);var z=different.Step(three);
            Check(x==y,"Seeded bot behavior is not repeatable");varied|=x!=z;
            int cell=left.actors[0].Cell;visited.Add(cell);
            if(cell!=old){moves++;if(cell==lastCell)backtracks++;lastCell=old;}
        }
        Check(UnityEngine.Random.value==expected,"Bot changed the game pattern random state");UnityEngine.Random.state=state;
        Check(visited.Count>=8&&backtracks<moves/2&&varied,"Route diversity or loop avoidance failed: "+visited.Count+" cells, "+backtracks+" reversals / "+moves);
        Note("Route diversity: "+visited.Count+" cells, "+backtracks+" immediate reversals / "+moves+" moves; seeds vary behavior without changing Unity RNG");
        var danger=new Arena{danger=true,forbidden=13};var escaping=Make(d:ChamberBotDifficulty.Hard);
        var escape=escaping.Tick(danger,0,1);Check(escape!=Vector2Int.zero&&danger.Neighbor(12,escape)!=13,"Bot chose known danger when a safe escape existed");
        Note("Known immediate danger overrides novelty and aggression");
    }
    static void EasyDifficulty(){
        var a=new Arena();a.Place(1,14);var b=new Arena();b.Place(1,14);
        var one=Make(d:ChamberBotDifficulty.Easy,seed:91);var two=Make(d:ChamberBotDifficulty.Easy,seed:91);
        var unityState=UnityEngine.Random.state;float expected=UnityEngine.Random.value;UnityEngine.Random.state=unityState;
        bool previous=false;int skipped=0,delays=0,wrong=0;
        for(int i=0;i<2400;i++){
            a.Now=b.Now=i;int before=one.Mistakes;
            var x=one.Tick(a,0,1);var y=two.Tick(b,0,1);
            Check(x==y&&one.Mistakes==two.Mistakes,"Easy mistakes are not deterministic for the same seed");
            bool mistake=one.Mistakes>before;
            if(previous){skipped++;Check(!mistake&&x==Vector2Int.right,"Easy bot made consecutive mistakes");}
            Check(mistake==(x!=Vector2Int.right),"Easy mistake did not change the real movement input");
            if(mistake){if(x==Vector2Int.zero)delays++;else wrong++;}
            previous=mistake;
        }
        float rate=(float)one.Mistakes/(2400-skipped);
        Check(rate>.16f&&rate<.24f&&delays>100&&wrong>60,"Easy error distribution is outside the intended range: "+rate);
        Check(UnityEngine.Random.value==expected,"Mistakes changed the game's pattern random state");UnityEngine.Random.state=unityState;
        one.ResetRound();Check(one.Mistakes==0,"Round restart retained the mistake counter");
        Note($"Easy: deterministic actual mistakes at {rate:P1} of eligible decisions; {delays} delays / {wrong} wrong turns; no consecutive mistakes or shared RNG changes");
        foreach(var difficulty in new[]{ChamberBotDifficulty.Normal,ChamberBotDifficulty.Hard}){
            var arena=new Arena();arena.Place(1,14);var bot=Make(d:difficulty,seed:91);
            for(int i=0;i<300;i++){arena.Now=i;Check(bot.Tick(arena,0,1)==Vector2Int.right&&bot.Mistakes==0,"Non-easy bot received an artificial mistake");}
        }
        var late=new Arena();late.Place(1,14);late.risk=(from,to,delay,duration)=>to==7&&delay+duration>=.8f?1:0;
        var novice=Make(d:ChamberBotDifficulty.Easy,seed:62);int missedWarning=0;
        for(int i=0;i<900;i++){late.Now=i;if(novice.Tick(late,0,1)==Vector2Int.up)missedWarning++;}
        Check(missedWarning>5,"Easy errors never missed a future warning");
        Note("Normal/hard keep their inputs; easy bots sometimes choose a warned tile instead of only making harmless errors");
        foreach(int width in new[]{3,5,10}){
            var arena=new Arena{Width=width};arena.Place(0,0);arena.Place(1,1);arena.actors[1].Moving=true;arena.actors[1].Target=width+1;arena.forbidden=width;
            var bot=Make(d:ChamberBotDifficulty.Easy,seed:width);
            for(int i=0;i<250;i++){
                arena.Now=i;var direction=bot.Tick(arena,0,1);int to=direction==Vector2Int.zero?0:arena.Neighbor(0,direction);
                Check(to>=0&&to!=1&&to!=width+1&&to!=width,"Easy mistake ignored a boundary, reservation or immediate hazard");
            }
        }
        var timing=new Arena();timing.actors[1].Alive=false;var slow=Make(d:ChamberBotDifficulty.Easy);int last=0;float previousAt=-1;
        for(int i=0;i<1200;i++){
            timing.Now=i/120f;slow.Tick(timing,0,1f/120);
            if(slow.Decisions==last)continue;
            if(previousAt>=0)Check(timing.Now-previousAt>=.44f,"Easy decision cadence is too fast");
            previousAt=timing.Now;last=slow.Decisions;
        }
        Check(last>=17&&last<=23&&slow.SearchDepth==2,"Easy cadence or search depth did not decrease");
        Note("Easy uses roughly half-second decisions, a two-action horizon, and preserves board/collision legality");
    }
    static void AdventurerExploration(){
        int cases=0;
        foreach(ChamberBotDifficulty difficulty in Enum.GetValues(typeof(ChamberBotDifficulty)))
        for(int seed=0;seed<16;seed++)foreach(bool hazard in new[]{false,true}){
            var arena=new Arena{forbidden=hazard?7:-1};arena.Place(1,14);
            // Begin beside the player or one step away from the same blocking lane.
            arena.Place(0,seed%2==0?12:13);var bot=Make(ChamberBotPersonality.Adventurer,difficulty,seed);
            var visited=new HashSet<int>();int hold=0,longestHold=0;
            for(int step=0;step<90;step++){
                int from=arena.actors[0].Cell;arena.Step(bot,.4f);int cell=arena.actors[0].Cell;
                Check(cell!=14&&cell!=arena.forbidden,"Adventurer entered an occupied or hazardous cell");
                hold=cell==from?hold+1:0;longestHold=Math.Max(longestHold,hold);visited.Add(cell);
            }
            Check(visited.Count>=8&&longestHold<12,"Adventurer camped beside a player: difficulty="+difficulty+", seed="+seed+", hazard="+hazard+", cells="+visited.Count+", longest hold="+longestHold);
            cases++;
        }
        Note("Adventurer explores safe routes near a player instead of camping: "+cases+" difficulty/seed/hazard scenarios");
        var refuge=new Arena();refuge.Place(1,14);refuge.risk=(from,to,delay,duration)=>to==12?0:1;
        var explorer=Make(ChamberBotPersonality.Adventurer);
        for(int step=0;step<20;step++)Check(refuge.Step(explorer)==Vector2Int.zero,"Adventurer left the only safe refuge to explore");
        refuge.risk=(from,to,delay,duration)=>to==11?0:1;
        Check(refuge.Step(explorer)==Vector2Int.left&&refuge.actors[0].Cell==11,"Adventurer failed to flee when its refuge became dangerous");
        Note("Adventurer still holds the only safe tile and escapes when a new safe route opens");
    }
    static void SafetyAndBlocking(){
        foreach(ChamberBotPersonality personality in Enum.GetValues(typeof(ChamberBotPersonality)))
        foreach(ChamberBotDifficulty difficulty in Enum.GetValues(typeof(ChamberBotDifficulty)))
        for(int seed=0;seed<12;seed++){
            var arena=new Arena{forbidden=13};var bot=Make(personality,difficulty,seed);
            for(int step=0;step<20;step++){
                var movement=arena.Step(bot);
                Check(arena.actors[0].Cell!=13,"Bot left safe ground for a hazard");
                if(personality!=ChamberBotPersonality.Adventurer&&difficulty!=ChamberBotDifficulty.Easy)Check(movement==Vector2Int.zero,"Defensive bot abandoned its safe refuge");
            }
            if(personality!=ChamberBotPersonality.Adventurer&&difficulty!=ChamberBotDifficulty.Easy)Check(bot.Intent=="안전구역 유지","Safe holding was not reported");
            arena.Place(0,12);
            arena.risk=(from,to,delay,duration)=>to==12||to==13?1:0;
            for(int attempt=0;attempt<3&&arena.actors[0].Cell==12;attempt++)arena.Step(bot,1);
            Check(arena.actors[0].Cell!=12&&arena.actors[0].Cell!=13,"New danger did not cancel safe holding");
            var afterEscape=arena.Step(bot,1);
            Check(arena.actors[0].Cell!=12&&arena.actors[0].Cell!=13,"Bot returned to danger after escaping");
            if(personality!=ChamberBotPersonality.Adventurer&&difficulty!=ChamberBotDifficulty.Easy)Check(afterEscape==Vector2Int.zero,"Defensive bot did not settle after reaching another refuge");
        }
        Note("All personalities/difficulties across 12 seeds avoid danger and escape new warnings; defensive types settle in safe refuges");
        var delayed=new Arena{SafetySeconds=3.2f};delayed.Place(1,14);
        delayed.risk=(from,to,delay,duration)=>to==13&&delay+duration>=2.5f?1:0;
        Check(delayed.Step(Make())==Vector2Int.zero,"Long visible warning was ignored to block a player");
        var crossing=new Arena();crossing.Place(1,14);
        crossing.risk=(from,to,delay,duration)=>from==12&&to==13?1:0;
        Check(crossing.Step(Make())==Vector2Int.zero,"Safe destination overrode a dangerous movement segment");
        Note("Normal bots keep long-warning and hazardous-crossing protection while blocking");
        for(int seed=0;seed<24;seed++){
            var arena=new Arena();arena.Place(1,14);var bot=Make(seed:seed);
            Check(arena.Step(bot)==Vector2Int.right&&arena.actors[0].Cell==13,"Nearby opponent's lane was not intercepted");
            for(int step=0;step<12;step++)Check(arena.Step(bot)==Vector2Int.zero&&bot.Intent=="블로킹 유지","Bot gave up a safe block or jittered");
            arena.forbidden=13;
            Check(arena.Step(bot)!=Vector2Int.zero&&arena.actors[0].Cell!=14,"Bot kept blocking on a dangerous tile or entered the opponent");
        }
        Note("Nearby lane interception, stable body blocking and immediate hazard retreat pass across 24 seeds");
        var moving=new Arena();moving.Place(0,9);moving.actors[1]=new ChamberBotActor{Cell=12,Target=13,Alive=true,Moving=true};
        var intercept=Make();moving.Step(intercept);
        Check(moving.actors[0].Cell==14,"Moving opponent was not intercepted beyond its reserved destination");
        var defensive=new Arena();defensive.Place(1,13);
        Check(defensive.Step(Make(ChamberBotPersonality.Survivor))==Vector2Int.zero,"Survivor abandoned a safe defensive block");
        defensive.actors[1].Alive=false;var explorer=Make();var cells=new HashSet<int>();
        for(int step=0;step<40;step++){defensive.Step(explorer);cells.Add(defensive.actors[0].Cell);Check(!explorer.Intent.Contains("블로킹"),"Bot blocked a dead opponent");}
        Check(cells.Count>=8,"Bot stayed stuck after its opponent disappeared");
        Note("Movement reservations, defensive holding and release after an opponent disappears are respected");
    }
    static void ActualBlocking(ChamberGame game){
        Setup(game);var board=Get<RisingBoard>(game,"board");var actors=Get<ChamberRunner[]>(game,"players");
        actors[0].Reset(14,board);actors[1].Reset(12,board);Get<ChamberBot[]>(game,"bots")[1]=Make(d:ChamberBotDifficulty.Hard);
        Run(game,.8f);
        Check(actors[1].Cell==13&&!actors[1].Moving&&game.Bot(1).Intent=="블로킹 유지","Actual runner failed to occupy and hold the lane");
        Call(game,"Move",actors[0],Vector2Int.left);
        Check(actors[0].Target==14&&!actors[0].Moving&&actors[0].Blocker==1,"Human input was not blocked by ordinary occupancy");
        Call(game,"AddSpike",13,0f);Warning(game);Set(game,"remaining",3f);Set(game,"warningSeconds",3f);Run(game,.5f);
        Check(actors[1].Cell!=13,"Long visible warning did not immediately cancel the block");Run(game,3f);
        Check(actors[1].Alive&&actors[1].Cell!=13,"Actual blocking runner failed to retreat from a new spike warning");
        Get<ChamberLan>(game,"lan").Leave();
        Note("Actual game: bot occupied the lane, blocked a human movement input, then survived a new spike warning");
    }
    static void Practice(ChamberGame game){
        var lan=Get<ChamberLan>(game,"lan");lan.OpenBotRoom();lan.AddBot(ChamberBotPersonality.Blocker,ChamberBotDifficulty.Hard);lan.StartMatch();
        Check(Get<object>(lan,"wire")==null,"Practice unexpectedly opened a network transport");
        Check(game.Bot(1)!=null&&game.Bot(2)!=null,"Starting a match did not create controllers for the bot seats");
        for(int i=0;i<600;i++)lan.Poll(1f/120,i==400?Vector2Int.up:Vector2Int.zero);
        Check(lan.Playing&&Get<int>(lan,"sequence")==600&&game.Bot(1).Decisions>0,"Practice input loop did not advance");
        // Force a normal round finish, then let the ordinary retry/countdown path reset memory.
        if(!game.RoundFinished){Call(game,"KnockOut",0,TrapKind.Spike,Get<float>(game,"elapsed"));Call(game,"KnockOut",1,TrapKind.Spike,Get<float>(game,"elapsed"));Call(game,"FinishIfDecided",Get<float>(game,"elapsed"));}
        bool restarted=false;
        for(int i=0;i<1200;i++){lan.Poll(1f/120,Vector2Int.zero);if(game.CountingDown){restarted=true;break;}}
        Check(restarted&&lan.IsBot(1)&&game.Bot(1).RememberedMoves==0,"Round restart lost bot settings or retained stale memory");
        lan.Leave();Check(!lan.Busy&&game.Bot(1)==null,"Practice exit retained a bot controller");
        Note("Offline practice advances without any network transport; round restart keeps seats and clears short-term memory; exit restores solo play");
    }
    sealed class FakeWire:IChamberWire {
        public FakeWire partner;public bool accept,connect;public bool Created=>true;
        readonly Queue<byte[]> queue=new Queue<byte[]>();
        public ChamberConnection Accept(){if(!accept)return default;accept=false;return new ChamberConnection(1u);}
        public void Update(){}public void Flush(){}public void Disconnect(ChamberConnection c){}public void Dispose(){}
        public bool Send(ChamberConnection c,byte[] bytes,bool ordered){partner.queue.Enqueue((byte[])bytes.Clone());return true;}
        public NetworkEvent.Type Receive(ChamberConnection c,out byte[] bytes){bytes=null;if(connect){connect=false;return NetworkEvent.Type.Connect;}if(queue.Count==0)return NetworkEvent.Type.Empty;bytes=queue.Dequeue();return NetworkEvent.Type.Data;}
    }
    static void Network(ChamberGame[] games){
        var host=Get<ChamberLan>(games[0],"lan");var guest=Get<ChamberLan>(games[1],"lan");
        host.OpenBotRoom();Check(host.Practice&&host.OccupiedSeats==2&&host.CanStart,"Offline bot room cannot start without network");
        host.AddBot(ChamberBotPersonality.Blocker,ChamberBotDifficulty.Hard);host.AddBot(ChamberBotPersonality.Opportunist,ChamberBotDifficulty.Easy);
        Check(!host.AddBot(),"Room capacity exceeded");
        Check(host.SetBot(1,new ChamberBotSettings{Personality=ChamberBotPersonality.Adventurer,Difficulty=ChamberBotDifficulty.Normal}),"Bot settings not editable");
        Check(host.RemoveBot(3)&&host.AddBot(),"Bot removal/addition failed");
        var hw=new FakeWire{accept=true};var gw=new FakeWire{connect=true};hw.partner=gw;gw.partner=hw;
        Set(host,"practice",false);Set(host,"wire",hw);guest.OpenMenu();Set(guest,"wire",gw);EnumField(guest,"mode","Connecting");Set(guest,"connectingAt",Time.realtimeSinceStartup);
        var peerType=typeof(ChamberLan).GetNestedType("Peer",BindingFlags.NonPublic);var peer=Activator.CreateInstance(peerType,true);
        Set(peer,"Connection",new ChamberConnection(1u));((IList)Get<object>(guest,"peers")).Add(peer);
        for(int i=0;i<4;i++){host.Poll(0,Vector2Int.zero);guest.Poll(0,Vector2Int.zero);}
        Check(host.OccupiedSeats==4&&!host.IsBot(1)&&host.IsBot(2)&&host.IsBot(3),"Joining human did not replace overflow bot");
        Check(guest.InRoom&&guest.Slot==1&&guest.IsBot(2)&&guest.BotSettings(2).Personality==ChamberBotPersonality.Adventurer,"Bot roster/config did not reach guest");
        Check(!host.CanStart,"Host started before human guest was ready");guest.SetReady(true);guest.Poll(0,Vector2Int.zero);host.Poll(0,Vector2Int.zero);
        Check(host.CanStart,"Ready human and bots cannot start");host.StartMatch();host.Poll(0,Vector2Int.zero);guest.Poll(0,Vector2Int.zero);
        Check(host.Playing&&guest.Playing&&games[0].Bot(2)!=null&&games[1].Bot(2)==null,"Guest independently runs bot AI");
        Check(!host.RemoveBot(2),"Bot roster changed during match");
        foreach(var game in games)ForcePattern(game,"GroundRenewal");
        Check(Get<int>(games[0],"groundSignature")==Get<int>(games[1],"groundSignature"),"Ground warning differs between peers");
        int inputFrames=0;var timer=System.Diagnostics.Stopwatch.StartNew();
        for(int i=0;i<1200;i++){
            Set(host,"lastSend",-100f);Set(guest,"lastSend",-100f);host.Poll(1f/120,Vector2Int.zero);guest.Poll(1f/120,Vector2Int.zero);
            Check(host.Playing&&guest.Playing,"Network simulation failed or diverged");
            var history=(Array)Get<object>(host,"history");var frame=history.GetValue(Get<int>(host,"sequence")%4096);
            if((Get<int>(frame,"Keys")>>6)!=0)inputFrames++;
        }
        for(int i=0;i<100&&Get<int>(guest,"sequence")<Get<int>(host,"sequence");i++){Set(host,"lastSend",-100f);Set(guest,"lastSend",-100f);host.Poll(0,Vector2Int.zero);guest.Poll(.05f,Vector2Int.zero);}
        Check(Get<int>(guest,"sequence")==Get<int>(host,"sequence")&&inputFrames>5,"Bot input replay did not catch up: host="+Get<int>(host,"sequence")+", guest="+Get<int>(guest,"sequence")+", inputs="+inputFrames);
        Note("Lobby/host-only bot inputs passed; ground warning, fracture and restoration replayed over 1200 ticks with matching hashes, "+inputFrames+" bot-input frames ("+timer.ElapsedMilliseconds+" ms)");
        host.Leave();guest.Leave();Check(!host.Busy&&!guest.Busy&&games[0].Bot(2)==null,"Leaving retained bots");
    }
    static void Setup(ChamberGame game,int width=5){
        var lan=Get<ChamberLan>(game,"lan");if(lan.Busy)lan.Leave();lan.OpenBotRoom();lan.StartMatch();game.BeginDuel(9137,2);
        var board=Get<RisingBoard>(game,"board");board.Resize(width);
        var actors=Get<ChamberRunner[]>(game,"players");actors[0].Reset(0,board);actors[1].Reset(4,board);
        EnumField(game,"phase","Rest");Set(game,"remaining",5f);
    }
    static void Run(ChamberGame game,float seconds){
        var keys=new Vector2Int[4];
        for(int i=0;i<seconds*120&&!game.RoundFinished;i++){keys[1]=game.BotInput(1,1f/120);game.DuelTick(keys,1f/120);}
    }
    static void Warning(ChamberGame game){EnumField(game,"phase","Warning");Set(game,"remaining",.8f);Set(game,"warningSeconds",.8f);Set(game,"strikeSeconds",1.3f);}
    static float FullScanRisk(ChamberGame game,int from,int to,float delay,float duration){
        var board=Get<RisingBoard>(game,"board");var tiles=board.Tiles;var poison=Get<PoisonFloor[]>(game,"poison");
        var order=Get<List<int>>(game,"spikeOrder");var onsets=Get<List<float>>(game,"onsets");
        string phase=Get<object>(game,"phase").ToString();float elapsed=Get<float>(game,"elapsed"),remaining=Get<float>(game,"remaining");
        bool telling=phase=="Warning"||phase=="FirstStep";float age=telling?-remaining:Get<float>(game,"strikeSeconds")-remaining;
        float now=elapsed+delay,hit=float.PositiveInfinity;var start=tiles[from].transform.position;var end=tiles[to].transform.position;
        if(telling||phase=="Strike")for(int k=0;k<order.Count;k++)hit=Mathf.Min(hit,tiles[order[k]].Contact(start,end,age+delay-onsets[k],duration,Get<bool>(game,"globalSpikes"),.46f));
        for(int i=0;i<board.Count;i++)if(poison[i].EndsAt>now||poison[i].Pending&&telling)
            hit=Mathf.Min(hit,poison[i].ForecastContact(start,end,now,duration,telling?elapsed+remaining:float.PositiveInfinity));
        var lava=Get<LavaBarrage>(game,"lava");
        if(lava.Active)for(int i=0;i<lava.Count;i++){
            float strike=lava.StrikeAt(i);if(elapsed<strike-lava.WarningSeconds||now+duration<strike||now>=strike+LavaBarrage.Hot)continue;
            float a=Mathf.Clamp01((strike-now)/duration),b=Mathf.Clamp01((strike+LavaBarrage.Hot-now)/duration);var center=tiles[lava.Target(i)].transform.position;
            hit=Mathf.Min(hit,TrapContact.Entry(Vector3.Lerp(start,end,a)-center,Vector3.Lerp(start,end,b)-center,.84f,.84f));
        }
        return hit<=1?1:0;
    }
    static void ForecastEquivalence(ChamberGame game){
        int queries=0;
        foreach(int width in new[]{3,10})for(int pattern=0;pattern<5;pattern++){
            Setup(game,width);var board=Get<RisingBoard>(game,"board");var arena=(IChamberBotArena)game;Warning(game);
            if(pattern<2){
                Set(game,"globalSpikes",pattern==0);
                for(int i=0;i<board.Count;i++)Call(game,"AddSpike",i,pattern==0?0f:(i%4)*.2f);
            }else if(pattern<4){
                foreach(var patch in Get<PoisonFloor[]>(game,"poison").Take(board.Count)){patch.Mark(.12f);if(pattern==3)patch.Activate(0);}
            }else{
                Get<LavaBarrage>(game,"lava").Plan(board,new bool[100],0,1,.8f,.72f);EnumField(game,"phase","LavaWarning");
            }
            for(int cell=0;cell<board.Count;cell++)foreach(var direction in ChamberBot.Actions){
                int next=direction==Vector2Int.zero?cell:board.Neighbor(cell,direction);if(next<0)continue;
                foreach(float delay in new[]{0f,.35f,.79f,.95f,1.2f,1.6f}){
                    float expected=FullScanRisk(game,cell,next,delay,.2f),actual=arena.TravelRisk(1,cell,next,delay,.2f);
                    Check(expected==actual,$"Local forecast missed a trap: width={width}, pattern={pattern}, {cell}->{next}, delay={delay}");queries++;
                }
            }
            // Revealing another spike in the same tick must invalidate the shared lookup.
            if(pattern==2){
                int cell=4;Get<PoisonFloor[]>(game,"poison")[cell].Clear();
                Check(arena.TravelRisk(1,cell,cell,.9f,.2f)==0,"Cache invalidation fixture was already dangerous");
                Call(game,"AddSpike",cell,0f);
                Check(arena.TravelRisk(1,cell,cell,.9f,.2f)==1&&FullScanRisk(game,cell,cell,.9f,.2f)==1,"New visible front was not observed");
            }
        }
        Get<ChamberLan>(game,"lan").Leave();
        Note("Local static-trap forecast matches full-map collision queries: "+queries+" stationary/adjacent paths, simultaneous/staggered spikes, pending/live poison and visible/hidden lava");
    }
    static void Hazards(ChamberGame game){
        Setup(game);Call(game,"AddSpike",4,0f);Warning(game);Run(game,1.4f);
        Check(Get<ChamberRunner[]>(game,"players")[1].Alive,"Bot failed visible spike evacuation");Note("Actual spike warning: bot evacuated before impact");
        Setup(game);Get<PoisonFloor[]>(game,"poison")[4].Mark();Warning(game);Run(game,1.5f);
        Check(Get<ChamberRunner[]>(game,"players")[1].Alive,"Bot failed pending poison evacuation");Note("Pending poison forecast: bot evacuated before the spill");
        Setup(game);var board=Get<RisingBoard>(game,"board");var tide=Get<TidalWave>(game,"tide");tide.Plan(board,4,0,0);Set(game,"tideCasts",1);
        EnumField(game,"phase","TideWarning");Set(game,"remaining",.8f);Set(game,"warningSeconds",.8f);Run(game,1.7f);
        var actor=Get<ChamberRunner[]>(game,"players")[1];Check(actor.Alive&&actor.LastWave==-1,"Bot failed visible wave evacuation");Note("Actual wave warning: bot escaped the moving wave lane");
        Setup(game);board=Get<RisingBoard>(game,"board");var lava=Get<LavaBarrage>(game,"lava");lava.Plan(board,new bool[100],0,1,.8f,.72f);
        int target=lava.Target(0);Get<ChamberRunner[]>(game,"players")[1].Reset(target,board);
        if(target==0)Get<ChamberRunner[]>(game,"players")[0].Reset(8,board);
        var arena=(IChamberBotArena)game;
        Check(arena.TravelRisk(1,target,target,.81f,.1f)>0,"Visible lava target not recognized");
        int hidden=lava.Target(1);Check(arena.TravelRisk(1,hidden,hidden,1.53f,.1f)==0,"Bot read a lava target before its marker appeared");
        EnumField(game,"phase","LavaWarning");Set(game,"remaining",.8f);Set(game,"warningSeconds",.8f);Run(game,1.4f);
        Check(Get<ChamberRunner[]>(game,"players")[1].Alive,"Bot failed lava evacuation");Note("Lava avoidance uses visible targets only");
        Setup(game,4);board=Get<RisingBoard>(game,"board");Get<ChamberRunner[]>(game,"players")[1].Reset(15,board);
        EnumField(game,"phase","Collapse");Set(game,"remaining",1.2f);Run(game,1.3f);actor=Get<ChamberRunner[]>(game,"players")[1];
        Check(actor.Alive&&actor.Target<9,"Bot stayed on collapsing rim");Note("Bot left the collapsing rim before the board shrank");
        Setup(game);actor=Get<ChamberRunner[]>(game,"players")[1];actor.StartStun();Run(game,2);
        Check(!actor.Stunned&&actor.WakeCount==6,"Bot did not use the normal six-key recovery path");Note("Actual runner recovered through six ordinary direction inputs");
        Setup(game);board=Get<RisingBoard>(game,"board");var arrows=Get<ArrowVolley>(game,"arrow");arrows.Plan(board,4,true,1,6,new bool[100],0);
        bool filtered=false;
        for(float age=1.5f;age<4&&!filtered;age+=.025f)for(int i=0;i<board.Count&&!filtered;i++){
            var p=board.Tiles[i].transform.position;
            filtered=arrows.Contact(p,p,age,.02f)<=1&&float.IsPositiveInfinity(arrows.Contact(p,p,age,.02f,-1));
        }
        Check(filtered,"Arrow forecast failed to filter unrevealed delayed shots");Note("Unrevealed delayed arrows are excluded from bot observations");
        Setup(game);board=Get<RisingBoard>(game,"board");Get<ArrowVolley>(game,"arrow").Plan(board,4,true,1,0,new bool[100],0);Warning(game);Run(game,1.5f);
        Check(Get<ChamberRunner[]>(game,"players")[1].Alive,"Bot failed the actual arrow sweep");Note("Actual arrow sweep: bot left the announced firing lane");
        Get<ChamberLan>(game,"lan").Leave();
    }
    static void ForcePattern(ChamberGame game,string name){
        var type=typeof(ChamberGame).GetNestedType("PatternKind",BindingFlags.NonPublic);
        var outside=UnityEngine.Random.state;UnityEngine.Random.state=Get<UnityEngine.Random.State>(game,"duelRandom");
        try{Call(game,"Warn",Enum.Parse(type,name),0);Set(game,"duelRandom",UnityEngine.Random.state);}
        finally{UnityEngine.Random.state=outside;}
    }
    static void Idle(ChamberGame game,float seconds){
        var keys=new Vector2Int[4];for(int tick=0;tick<seconds*120;tick++)game.DuelTick(keys,1f/120);
    }
    static void GroundSetup(ChamberGame game,int width,float tempo,out int cracked,out int safe){
        Setup(game,width);ForcePattern(game,"GroundRenewal");Set(game,"patternTempo",tempo);
        Set(game,"remaining",.8f);Set(game,"warningSeconds",.8f);
        var board=Get<RisingBoard>(game,"board");var cells=Get<List<int>>(game,"groundTiles");var human=Get<ChamberRunner[]>(game,"players")[0];
        foreach(int cell in cells)foreach(var direction in ChamberBot.Actions.Skip(1)){
            int next=board.Neighbor(cell,direction);
            if(next>=0&&!cells.Contains(next)&&!human.Occupies(next)){cracked=cell;safe=next;return;}
        }
        throw new Exception("Ground fixture has no cracked tile next to an available safe tile");
    }
    static void NewPatterns(ChamberGame game){
        foreach(float tempo in new[]{1f,2f}){
            GroundSetup(game,5,tempo,out int cracked,out int safe);
            var arena=(IChamberBotArena)game;float until=.8f+.52f/tempo,move=arena.MoveSeconds;
            Check(arena.SafetySeconds>until,"Safety window ends before the announced fracture");
            Check(arena.TravelRisk(1,cracked,cracked,0,until-.01f)==0&&arena.TravelRisk(1,cracked,cracked,0,until+.01f)==1,"Ground forecast missed its actual disappearance time");
            Check(arena.TravelRisk(1,cracked,safe,until-move-.01f,move)==0,"Ground forecast forbids a completed escape");
            Check(arena.TravelRisk(1,cracked,safe,until-move+.01f,move)==1,"Ground forecast forgot the departure tile reservation");
            Check(arena.TravelRisk(1,safe,cracked,until-move+.01f,move)==1,"Ground forecast forgot the arrival tile reservation");
            Check(arena.TravelRisk(1,safe,cracked,until+.88f/tempo+.01f,move)==0,"Ground forecast never releases a restored tile");
            EnumField(game,"phase","Rest");Check(arena.TravelRisk(1,cracked,cracked,0,4)==0,"Inactive ground plans leaked into observations");
            EnumField(game,"phase","GroundWarning");
            var cells=Get<List<int>>(game,"groundTiles");cells[cells.IndexOf(cracked)]=safe;Set(game,"groundSignature",Get<int>(game,"groundSignature")+1);
            Check(arena.TravelRisk(1,cracked,cracked,0,4)==0&&arena.TravelRisk(1,safe,safe,0,4)==1,"Same-tick ground warning change kept a stale lookup");
        }
        Note("Ground prediction covers warning, exact disappearance, both movement endpoints, restoration, hidden plans and cache changes at 1x/2x tempo");
        foreach(float remaining in new[]{.08f,.30f}){
            GroundSetup(game,5,1,out int cracked,out int safe);Call(game,"NextPhase");Set(game,"remaining",remaining);
            var board=Get<RisingBoard>(game,"board");var actor=Get<ChamberRunner[]>(game,"players")[1];actor.Reset(cracked,board);
            var arena=(IChamberBotArena)game;bool predicted=arena.TravelRisk(1,cracked,safe,0,arena.MoveSeconds)>0;
            Call(game,"Move",actor,board.Cell(safe)-board.Cell(cracked));Idle(game,.4f);
            Check(predicted==!actor.Alive&&predicted==(remaining<arena.MoveSeconds),"Ground forecast disagreed with actual fall during a hop");
        }
        Note("Actual runner fall agrees with the forecast when departure ground breaks before/after landing");
        foreach(float tempo in new[]{1f,2f}){
            GroundSetup(game,5,tempo,out int cracked,out int safe);Call(game,"NextPhase");Call(game,"NextPhase");
            var board=Get<RisingBoard>(game,"board");var arena=(IChamberBotArena)game;var direction=board.Cell(cracked)-board.Cell(safe);
            Check(!board.IsPresent(cracked)&&board.Neighbor(safe,direction)<0&&arena.TravelRisk(1,safe,cracked,0,.16f)==1,"Missing ground remained walkable");
            Idle(game,.88f/tempo+.02f);
            Check(board.IsPresent(cracked)&&board.Neighbor(safe,direction)==cracked&&arena.TravelRisk(1,safe,cracked,0,.16f)==0,"Restored ground remained blocked");
        }
        Note("Missing tiles are excluded from routes and become usable after actual restoration at 1x/2x tempo");
        int evacuations=0;
        foreach(int width in new[]{3,10})foreach(float tempo in new[]{1f,2f})
        foreach(var difficulty in new[]{ChamberBotDifficulty.Normal,ChamberBotDifficulty.Hard})
        foreach(ChamberBotPersonality personality in Enum.GetValues(typeof(ChamberBotPersonality))){
            GroundSetup(game,width,tempo,out int cracked,out int safe);
            var board=Get<RisingBoard>(game,"board");var actor=Get<ChamberRunner[]>(game,"players")[1];actor.Reset(cracked,board);
            Get<ChamberBot[]>(game,"bots")[1]=Make(personality,difficulty,41+evacuations);
            Run(game,.8f+.52f/tempo+.12f);
            Check(actor.Alive&&board.IsPresent(actor.Cell)&&board.IsPresent(actor.Target),"Bot failed ground evacuation: "+width+"/"+tempo+"/"+personality+"/"+difficulty);
            evacuations++;
        }
        Note("Normal/hard bots escaped announced ground fractures in "+evacuations+" map/tempo/personality scenarios");
        foreach(int width in new[]{3,5,10})foreach(bool horizontal in new[]{false,true}){
            Setup(game,width);var board=Get<RisingBoard>(game,"board");var arrows=Get<ArrowVolley>(game,"arrow");
            var exits=new bool[100];exits[0]=true;arrows.Plan(board,4,horizontal,1,7,exits,0);Warning(game);Set(game,"strikeSeconds",arrows.FlightTime+.1f);
            var arena=(IChamberBotArena)game;int safeCount=0,start=-1;
            for(int cell=0;cell<board.Count;cell++){
                bool safe=arena.TravelRisk(1,cell,cell,0,.8f+arrows.FlightTime+.1f)==0;
                if(!safe)continue;safeCount++;
                foreach(var direction in ChamberBot.Actions.Skip(1)){
                    int next=board.Neighbor(cell,direction);
                    if(next>=0&&arena.TravelRisk(1,next,next,0,.8f+arrows.FlightTime+.1f)>0)start=next;
                }
            }
            Check(safeCount==width&&start>=0,"Arrow row safe-zone forecast is wrong");
            var actor=Get<ChamberRunner[]>(game,"players")[1];actor.Reset(start,board);Get<ChamberBot[]>(game,"bots")[1]=Make(d:ChamberBotDifficulty.Hard);
            Run(game,.8f+arrows.FlightTime+.05f);Check(actor.Alive,"Bot failed the one-safe-row arrow pattern");
        }
        Note("One-safe-row arrows: horizontal/vertical forecasts and actual bot survival pass on 3x3/5x5/10x10");
        Get<ChamberLan>(game,"lan").Leave();
    }
    static void Automatic(ChamberGame game){
        foreach(int width in new[]{3,5,10}){
            var lan=Get<ChamberLan>(game,"lan");lan.OpenBotRoom();lan.AddBot(ChamberBotPersonality.Blocker,ChamberBotDifficulty.Hard);lan.AddBot(ChamberBotPersonality.Opportunist,ChamberBotDifficulty.Easy);lan.StartMatch();
            game.BeginDuel(6043+width,4);Get<RisingBoard>(game,"board").Resize(width);EnumField(game,"phase","Rest");Set(game,"remaining",.1f);
            var controllers=Get<ChamberBot[]>(game,"bots");controllers[0]=Make(ChamberBotPersonality.Adventurer,ChamberBotDifficulty.Hard,width);
            var keys=new Vector2Int[4];var elapsed=System.Diagnostics.Stopwatch.StartNew();int moves=0,maxWave=0;
            for(int tick=0;tick<1800;tick++){
                for(int i=0;i<4;i++){keys[i]=game.BotInput(i,1f/120);if(keys[i]!=Vector2Int.zero)moves++;}
                game.DuelTick(keys,1f/120);
                maxWave=Mathf.Max(maxWave,Get<int>(game,"wave"));
                var actors=Get<ChamberRunner[]>(game,"players");var board=Get<RisingBoard>(game,"board");
                foreach(var actor in actors)if(actor.Alive)Check(actor.Target>=0&&actor.Target<board.Count,"Bot moved out of the board");
            }
            // Holding a safe tile is intentional. Check ongoing decisions and
            // actual pattern progression instead of requiring needless movement.
            int decisions=controllers.Where(bot=>bot!=null).Sum(bot=>bot.Decisions);
            Check(moves>0&&maxWave>=2&&decisions>20,"Automatic match stalled: moves="+moves+", patterns="+maxWave+", decisions="+decisions);
            Note(width+"x"+width+" automatic 4-bot simulation: 1800 ticks, "+moves+" inputs, "+maxWave+" patterns, "+decisions+" decisions, "+elapsed.ElapsedMilliseconds+" ms; no invalid moves");lan.Leave();
        }
    }
    static void Finish(Exception error){
        EditorApplication.update-=Update;SessionState.SetBool(Key,false);
        if(error!=null){Results.Add("FAIL: "+error);Debug.LogException(error);}else Results.Add("ALL BOT CHECKS PASSED");
        File.WriteAllLines(Report,Results);EditorApplication.Exit(error==null?0:1);
    }
}
