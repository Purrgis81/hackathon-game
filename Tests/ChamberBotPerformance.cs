// Copy into Assets/Editor in an isolated project and run with -executeMethod ChamberBotPerformance.Begin.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ChamberBotPerformance
{
    const string Key="Codex.BotPerformance";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static double deadline;
    static readonly List<string> Results=new List<string>();
    static ChamberBotPerformance(){if(SessionState.GetBool(Key,false)){deadline=EditorApplication.timeSinceStartup+600;EditorApplication.update+=Update;}}
    public static void Begin(){EditorSceneManager.OpenScene("Assets/Scenes/StoneChamber.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
    static T Get<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Flags).GetValue(obj);
    static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Flags).SetValue(obj,value);
    static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Flags).Invoke(obj,args);
    static void Phase(ChamberGame game,string name){var f=game.GetType().GetField("phase",Flags);f.SetValue(game,Enum.Parse(f.FieldType,name));}
    sealed class Meter:IChamberBotArena {
        readonly IChamberBotArena arena;public int calls;
        public Meter(IChamberBotArena source){arena=source;}
        public int CellCount=>arena.CellCount;public int ActorCount=>arena.ActorCount;public float Now=>arena.Now;public float MoveSeconds=>arena.MoveSeconds;public float SafetySeconds=>arena.SafetySeconds;
        public int HazardRevision=>arena.HazardRevision;
        public Vector2Int Coordinates(int cell)=>arena.Coordinates(cell);public int Neighbor(int cell,Vector2Int d)=>arena.Neighbor(cell,d);public ChamberBotActor Actor(int slot)=>arena.Actor(slot);
        public float TravelRisk(int slot,int from,int to,float delay,float duration,float observedThrough=float.PositiveInfinity){calls++;return arena.TravelRisk(slot,from,to,delay,duration,observedThrough);}
    }
    static void Update(){
        try{
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Benchmark startup timeout");
            if(!Application.isPlaying)return;
            var game=UnityEngine.Object.FindFirstObjectByType<ChamberGame>();if(game==null||!game.DebugAvailable)return;
            EditorApplication.update-=Update;game.enabled=false;
            foreach(var config in new[]{new[]{10,0},new[]{10,1},new[]{10,2},new[]{10,3},new[]{5,1},new[]{3,1},new[]{10,4},new[]{10,5},new[]{3,4}})Measure(game,config[0],config[1]);
            MeasureMotionHistory();
            Finish(null);
        }catch(Exception e){Finish(e);}
    }
    static void Measure(ChamberGame game,int width,int pattern){
        game.BeginDuel(7214,4);var board=Get<RisingBoard>(game,"board");board.Resize(width);
        Phase(game,"Warning");Set(game,"remaining",.7f);Set(game,"strikeSeconds",3f);Set(game,"warningSeconds",.7f);
        if(pattern==1){Set(game,"globalSpikes",true);for(int i=0;i<board.Count;i++)Call(game,"AddSpike",i,0f);}
        if(pattern==2)foreach(var patch in Get<PoisonFloor[]>(game,"poison").Take(board.Count))patch.Mark();
        if(pattern==3)Get<ArrowVolley>(game,"arrow").Plan(board,4,true,1,6,new bool[100],1);
        if(pattern>=4){
            Call(game,"PlanGroundRenewal");Phase(game,pattern==4?"GroundWarning":"GroundGone");
            if(pattern==5)foreach(int cell in Get<List<int>>(game,"groundTiles"))board.SetTemporaryMissing(cell,true);
        }
        var bots=new ChamberBot[3];for(int i=0;i<3;i++)bots[i]=new ChamberBot(new ChamberBotSettings{Personality=(ChamberBotPersonality)(i+1),Difficulty=ChamberBotDifficulty.Hard},421+i);
        var arena=new Meter(game);var samples=new List<double>();int maxCalls=0;
        for(int iteration=0;iteration<28;iteration++){
            // Benchmark fully recognized warnings, not the cheaper unseen window.
            Set(game,"elapsed",1f+iteration/60f);arena.calls=0;
            long before=System.Diagnostics.Stopwatch.GetTimestamp();
            for(int i=0;i<3;i++)bots[i].Tick(arena,i+1,.4f);
            double ms=(System.Diagnostics.Stopwatch.GetTimestamp()-before)*1000.0/System.Diagnostics.Stopwatch.Frequency;
            if(iteration>=4){samples.Add(ms);maxCalls=Math.Max(maxCalls,arena.calls);}
        }
        samples.Sort();string line=$"{width}x{width} {new[]{"empty","all-spikes","all-poison","eight-way-arrows","ground-warning","ground-holes"}[pattern]} | 3 hard bots / batch | mean {samples.Average():F3} ms | p95 {samples[(int)(samples.Count*.95)]:F3} ms | max {samples.Last():F3} ms | max risk calls {maxCalls}";
        Results.Add(line);Debug.Log("BOT PERF: "+line);Save();
    }
    sealed class MotionArena:IChamberBotArena {
        public int tick;public int CellCount=>100;public int ActorCount=>4;public float Now=>tick/120f;
        public float MoveSeconds=>.16f;public float SafetySeconds=>1.2f;public int HazardRevision=>0;
        public Vector2Int Coordinates(int cell)=>new Vector2Int(cell%10,cell/10);
        public int Neighbor(int cell,Vector2Int d){var p=Coordinates(cell)+d;return p.x<0||p.x>=10||p.y<0||p.y>=10?-1:p.y*10+p.x;}
        public ChamberBotActor Actor(int slot){int side=(tick/20)&1;return new ChamberBotActor{Cell=slot*20+4+side,Target=slot*20+5-side,Alive=true,Moving=true};}
        public float TravelRisk(int slot,int from,int to,float delay,float duration,float observedThrough=float.PositiveInfinity)=>0;
    }
    static void MeasureMotionHistory(){
        var arena=new MotionArena();var bots=new ChamberBot[3];
        for(int i=0;i<3;i++)bots[i]=new ChamberBot(new ChamberBotSettings{Personality=ChamberBotPersonality.Blocker,Difficulty=ChamberBotDifficulty.Hard},i+41);
        var samples=new List<double>(2400);
        for(int tick=0;tick<2800;tick++){
            arena.tick=tick;long before=System.Diagnostics.Stopwatch.GetTimestamp();
            for(int i=0;i<3;i++)bots[i].Tick(arena,i+1,1f/120);
            double ms=(System.Diagnostics.Stopwatch.GetTimestamp()-before)*1000.0/System.Diagnostics.Stopwatch.Frequency;
            if(tick>=400)samples.Add(ms);
        }
        if(bots.Any(bot=>bot.Decisions!=0))throw new Exception("Motion observation benchmark unexpectedly ran planning");
        samples.Sort();string line=$"120Hz moving actors / full 16-entry histories | 3 hard bots / batch | mean {samples.Average():F4} ms | p95 {samples[(int)(samples.Count*.95)]:F4} ms | max {samples.Last():F4} ms";
        Results.Add(line);Debug.Log("BOT PERF: "+line);Save();
    }
    static void Save()=>File.WriteAllLines(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"bot-performance.txt"),Results);
    static void Finish(Exception error){
        EditorApplication.update-=Update;SessionState.SetBool(Key,false);
        if(error!=null){Results.Add("FAIL: "+error);Debug.LogException(error);}else Results.Add("BENCHMARK COMPLETE");
        Save();EditorApplication.Exit(error==null?0:1);
    }
}
