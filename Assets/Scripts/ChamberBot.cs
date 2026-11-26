using System;
using UnityEngine;

public enum ChamberBotPersonality { Survivor, Blocker, Opportunist, Adventurer }
public enum ChamberBotDifficulty { Easy, Normal, Hard }

[Serializable]
public struct ChamberBotSettings
{
    public ChamberBotPersonality Personality;
    public ChamberBotDifficulty Difficulty;
    public static readonly string[] Personalities={"생존형","견제형","기회형","모험형"};
    public static readonly string[] Difficulties={"쉬움","보통","어려움"};
    public int Code=>(int)Personality|((int)Difficulty<<2);
    public string Label=>Personalities[(int)Personality]+" · "+Difficulties[(int)Difficulty];
    public static ChamberBotSettings Decode(int code)=>new ChamberBotSettings {
        Personality=(ChamberBotPersonality)(code&3),Difficulty=(ChamberBotDifficulty)Mathf.Clamp((code>>2)&3,0,2)
    };
}

public struct ChamberBotActor
{
    public int Cell,Target;
    public bool Alive,Moving,Stunned,Sliding;
}

// Only observable actors and announced hazards are exposed to the planner.
// Keeping this interface independent of rendering also makes behavior reproducible in tests.
public interface IChamberBotArena
{
    int CellCount {get;}
    int ActorCount {get;}
    float Now {get;}
    float MoveSeconds {get;}
    float SafetySeconds {get;}
    Vector2Int Coordinates(int cell);
    int Neighbor(int cell,Vector2Int direction);
    ChamberBotActor Actor(int slot);
    float TravelRisk(int slot,int from,int to,float delay,float duration);
}

public sealed class ChamberBot
{
    const int Cells=100,MaxDepth=10;
    const float EasyMistakeChance=.20f;
    public static readonly Vector2Int[] Actions={Vector2Int.zero,Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
    readonly ChamberBotSettings settings;
    readonly System.Random random;
    readonly float[] visited=new float[Cells],preferences=new float[Cells];
    readonly int[] previousOpponent=new int[ChamberLan.Capacity];
    readonly float[,] headings=new float[ChamberLan.Capacity,4];
    readonly float[] lastSeenMove=new float[ChamberLan.Capacity];
    readonly ChamberBotActor[] actors=new ChamberBotActor[ChamberLan.Capacity];
    readonly int[] predicted=new int[ChamberLan.Capacity];
    readonly bool[] occupied=new bool[Cells];
    readonly int[] preferenceStamp=new int[Cells];
    struct Path { public int Cell;public float Score,Risk; }
    readonly Path[] frontier=new Path[2],candidates=new Path[10];
    readonly float[] actionScores=new float[5],noise=new float[5],firstRisk=new float[5];
    readonly float[] safeRisk=new float[Cells];
    readonly int[] safeStamp=new int[Cells];
    readonly float[] riskCache=new float[MaxDepth*Cells*5];
    readonly int[] riskStamp=new int[MaxDepth*Cells*5];
    int decision,previousCell=-1,beforePrevious=-1,goal=-1;
    float countdown,observedAt,goalUntil,stillSince;
    bool previousMistake;
    public ChamberBotSettings Settings=>settings;
    public string Intent {get;private set;}="주변 살피기";
    public float Aggression {get;private set;}
    public int Decisions {get;private set;}
    public int Mistakes {get;private set;}
    public int RememberedMoves {get;private set;}
    public int LastRiskChecks {get;private set;}
    public int LastExpandedNodes {get;private set;}
    public int LastScoredCells {get;private set;}
    public float DecisionInterval=>settings.Difficulty==ChamberBotDifficulty.Easy?.50f:settings.Difficulty==ChamberBotDifficulty.Normal?.21f:.13f;
    public int SearchDepth=>settings.Difficulty==ChamberBotDifficulty.Easy?2:settings.Difficulty==ChamberBotDifficulty.Normal?6:10;
    public int SearchRadius=>settings.Difficulty==ChamberBotDifficulty.Easy?2:settings.Difficulty==ChamberBotDifficulty.Normal?3:4;
    int BeamWidth=>settings.Difficulty==ChamberBotDifficulty.Easy?1:2;

    public ChamberBot(ChamberBotSettings options,int seed){settings=options;random=new System.Random(seed);ResetRound();}
    public void ResetRound(){
        for(int i=0;i<Cells;i++)visited[i]=-100;
        for(int i=0;i<previousOpponent.Length;i++){previousOpponent[i]=-1;lastSeenMove[i]=-100;}
        Array.Clear(headings,0,headings.Length);
        previousCell=beforePrevious=goal=-1;observedAt=goalUntil=stillSince=0;
        countdown=DecisionInterval*(.6f+(float)random.NextDouble()*.4f);
        Aggression=0;Intent="주변 살피기";RememberedMoves=0;
        previousMistake=false;Mistakes=0;
        LastRiskChecks=LastExpandedNodes=LastScoredCells=0;
    }
    public Vector2Int Tick(IChamberBotArena arena,int slot,float dt){
        countdown-=dt;
        var self=arena.Actor(slot);
        if(!self.Alive||self.Sliding)return Vector2Int.zero;
        if(countdown>0||self.Moving)return Vector2Int.zero;
        countdown=DecisionInterval*(.9f+(float)random.NextDouble()*.2f);
        if(self.Stunned){Intent="기절 탈출";return Actions[1+random.Next(4)];}
        UnityEngine.Profiling.Profiler.BeginSample("ChamberBot.Decide");
        try{return ApplyEasyMistake(arena,slot,Decide(arena,slot));}
        finally{UnityEngine.Profiling.Profiler.EndSample();}
    }
    Vector2Int ApplyEasyMistake(IChamberBotArena arena,int slot,Vector2Int planned){
        // Normal/hard consume no additional random values and keep their behavior.
        if(settings.Difficulty!=ChamberBotDifficulty.Easy)return planned;
        if(previousMistake){previousMistake=false;return planned;}
        if(random.NextDouble()>=EasyMistakeChance)return planned;
        bool hesitate=planned!=Vector2Int.zero&&random.NextDouble()<.6;
        int chosen=0,count=0,own=actors[slot].Cell;
        if(!hesitate)for(int action=1;action<5;action++){
            if(Actions[action]==planned)continue;
            int next=arena.Neighbor(own,Actions[action]);
            if(next<0||occupied[next])continue;
            // A lapse can miss an upcoming warning, but never changes collision
            // rules or deliberately steps into a hazard already covering the move.
            if(TravelRisk(arena,slot,own,next,0,arena.MoveSeconds)>0
                ||TravelRisk(arena,slot,next,next,arena.MoveSeconds,.12f)>0)continue;
            if(random.Next(++count)==0)chosen=action;
        }
        if(chosen==0&&planned==Vector2Int.zero)return planned;
        previousMistake=true;Mistakes++;
        Intent=chosen==0?"반응 지연":"이동 실수";
        return Actions[chosen];
    }
    int Distance(IChamberBotArena arena,int a,int b){var d=arena.Coordinates(a)-arena.Coordinates(b);return Mathf.Abs(d.x)+Mathf.Abs(d.y);}
    void Observe(IChamberBotArena arena,int slot){
        float decay=Mathf.Exp(-Mathf.Max(0,arena.Now-observedAt)/3f);observedAt=arena.Now;
        for(int i=0;i<arena.ActorCount;i++){
            for(int d=0;d<4;d++)headings[i,d]*=decay;
            var other=actors[i];
            if(i==slot||!other.Alive)continue;
            int cell=other.Target;
            if(previousOpponent[i]>=0&&previousOpponent[i]!=cell){
                var delta=arena.Coordinates(cell)-arena.Coordinates(previousOpponent[i]);
                for(int d=0;d<4;d++)if(delta.x*Actions[d+1].x+delta.y*Actions[d+1].y>0)headings[i,d]+=1;
                lastSeenMove[i]=arena.Now;RememberedMoves++;
            }
            previousOpponent[i]=cell;
        }
        int own=actors[slot].Cell;
        if(previousCell!=own){
            if(previousCell>=0)visited[previousCell]=arena.Now;
            beforePrevious=previousCell;previousCell=own;stillSince=arena.Now;
        }
    }
    public int PredictedCell(IChamberBotArena arena,int otherSlot){
        var other=arena.Actor(otherSlot);
        if(other.Moving)return other.Target;
        if(other.Stunned||arena.Now-lastSeenMove[otherSlot]>3)return other.Cell;
        int direction=-1;float largest=.15f;
        for(int d=0;d<4;d++)if(headings[otherSlot,d]>largest){largest=headings[otherSlot,d];direction=d;}
        int next=direction<0?-1:arena.Neighbor(other.Cell,Actions[direction+1]);
        return next>=0?next:other.Cell;
    }
    float TravelRisk(IChamberBotArena arena,int slot,int from,int to,float delay,float duration){
        LastRiskChecks++;return arena.TravelRisk(slot,from,to,delay,duration);
    }
    float StationaryRisk(IChamberBotArena arena,int slot,int cell){
        if(cell<0||cell>=arena.CellCount)return 1;
        if(safeStamp[cell]!=decision){
            safeStamp[cell]=decision;
            safeRisk[cell]=TravelRisk(arena,slot,cell,cell,0,arena.SafetySeconds);
        }
        return safeRisk[cell];
    }
    int SafeExits(IChamberBotArena arena,int slot,int cell){
        int exits=0;
        for(int a=1;a<5;a++){
            int next=arena.Neighbor(cell,Actions[a]);
            if(next>=0&&!occupied[next]&&StationaryRisk(arena,slot,next)==0)exits++;
        }
        return exits;
    }
    bool PrepareSafety(IChamberBotArena arena,int slot){
        int own=actors[slot].Cell;bool nearbyDanger=false;
        firstRisk[0]=StationaryRisk(arena,slot,own);
        for(int a=1;a<5;a++){
            int next=arena.Neighbor(own,Actions[a]);
            firstRisk[a]=float.PositiveInfinity;
            if(next<0)continue;
            float risk=StationaryRisk(arena,slot,next);
            nearbyDanger|=risk>0;
            if(occupied[next])continue;
            risk=Mathf.Max(risk,TravelRisk(arena,slot,own,next,0,arena.MoveSeconds));
            nearbyDanger|=risk>0;firstRisk[a]=risk;
        }
        return nearbyDanger;
    }
    void PreparePreferences(IChamberBotArena arena,int slot){
        int own=actors[slot].Cell;
        float danger=firstRisk[0];
        int exits=0;bool opportunity=false;
        for(int a=1;a<5;a++)if(firstRisk[a]==0)exits++;
        for(int i=0;i<arena.ActorCount;i++)if(i!=slot&&actors[i].Alive){
            var p=actors[i];opportunity|=p.Stunned&&Distance(arena,own,p.Cell)<4;predicted[i]=PredictedCell(arena,i);
        }
        float baseline=settings.Personality==ChamberBotPersonality.Survivor?.06f:
            settings.Personality==ChamberBotPersonality.Blocker?.85f:
            settings.Personality==ChamberBotPersonality.Opportunist?(opportunity?.95f:.25f):0f;
        Aggression=baseline*(1-Mathf.Clamp01(danger))*(exits<2?.2f:1f);
        if(goal<0||goal>=arena.CellCount||arena.Now>=goalUntil||own==goal){
            goal=random.Next(arena.CellCount);goalUntil=arena.Now+2+(float)random.NextDouble()*2;
        }
    }
    bool TryBlock(IChamberBotArena arena,int slot,out int chosen){
        chosen=0;if(firstRisk[0]!=0||settings.Personality==ChamberBotPersonality.Adventurer)return false;
        int own=actors[slot].Cell;float best=.35f;
        // Inspect only nearby opponents and the current/adjacent cells. Holding a
        // lane blocks through the same occupancy rule used for human players.
        for(int i=0;i<arena.ActorCount;i++){
            var other=actors[i];if(i==slot||!other.Alive||other.Sliding)continue;
            int anchor=other.Moving?other.Target:other.Cell;
            if(anchor<0||anchor>=arena.CellCount||Distance(arena,own,anchor)>2)continue;
            int exits=SafeExits(arena,slot,anchor);
            int ahead=predicted[i];
            if(other.Moving){
                var direction=arena.Coordinates(other.Target)-arena.Coordinates(other.Cell);
                if(Mathf.Abs(direction.x)+Mathf.Abs(direction.y)==1)ahead=arena.Neighbor(anchor,direction);
            }
            for(int a=0;a<5;a++){
                if(firstRisk[a]!=0)continue;
                int cell=a==0?own:arena.Neighbor(own,Actions[a]);
                if(Distance(arena,cell,anchor)!=1)continue;
                if(a!=0&&SafeExits(arena,slot,cell)==0)continue;
                float score=(1.3f+(cell==ahead?3f:0)+(exits<=2?1.2f:0)+(other.Stunned?1f:0))*Aggression;
                score+=a==0?.5f:-.2f;
                if(score>best){best=score;chosen=a;}
            }
        }
        return best>.35f;
    }
    float Preference(IChamberBotArena arena,int slot,int cell){
        if(preferenceStamp[cell]==decision)return preferences[cell];
        preferenceStamp[cell]=decision;LastScoredCells++;
        int own=actors[slot].Cell;
        float room=0,nearest=20,block=0;
        for(int a=1;a<5;a++){int n=arena.Neighbor(cell,Actions[a]);if(n>=0&&!occupied[n])room++;}
        for(int i=0;i<arena.ActorCount;i++)if(i!=slot){
            var other=actors[i];if(!other.Alive)continue;
            int distance=Distance(arena,cell,other.Target);nearest=Mathf.Min(nearest,distance);
            if(Distance(arena,own,other.Target)<=2){
                if(distance==1)block=Mathf.Max(block,other.Stunned?3.5f:1.3f);
                if(predicted[i]!=other.Cell&&cell==predicted[i])block=Mathf.Max(block,4f);
            }
        }
        float avoid=settings.Personality==ChamberBotPersonality.Survivor?.55f:.14f;
        float novelty=settings.Personality==ChamberBotPersonality.Adventurer?1.2f:.75f;
        float recent=Mathf.Clamp01(1-(arena.Now-visited[cell])/5f);
        preferences[cell]=room*.45f+Mathf.Min(nearest,4)*avoid+Aggression*block
            -recent*novelty-Distance(arena,cell,goal)*.055f;
        if(cell==beforePrevious)preferences[cell]-=.65f;
        if(cell==own)preferences[cell]-=Mathf.Min(1.2f,(arena.Now-stillSince)*.25f);
        return preferences[cell];
    }
    float Risk(IChamberBotArena arena,int slot,int from,int to,int depth,int action,float step){
        int index=(depth*Cells+from)*5+action;
        if(riskStamp[index]==decision)return riskCache[index];
        float delay=depth*step,move=action==0?step:arena.MoveSeconds;
        float risk=TravelRisk(arena,slot,from,to,delay,move);
        if(action!=0&&step>move)risk=Mathf.Max(risk,TravelRisk(arena,slot,to,to,delay+move,step-move));
        riskStamp[index]=decision;return riskCache[index]=risk;
    }
    Vector2Int Decide(IChamberBotArena arena,int slot){
        LastRiskChecks=LastExpandedNodes=LastScoredCells=0;
        Array.Clear(occupied,0,occupied.Length);
        for(int i=0;i<arena.ActorCount;i++){
            var actor=actors[i]=arena.Actor(i);
            if(i!=slot&&actor.Alive){occupied[actor.Cell]=true;occupied[actor.Target]=true;}
        }
        Observe(arena,slot);Decisions++;decision++;
        if(decision==int.MaxValue){Array.Clear(riskStamp,0,riskStamp.Length);Array.Clear(preferenceStamp,0,preferenceStamp.Length);Array.Clear(safeStamp,0,safeStamp.Length);decision=1;}
        int own=actors[slot].Cell,depths=SearchDepth;
        float step=Mathf.Max(arena.MoveSeconds+1f/ChamberLan.TickRate,DecisionInterval);
        bool nearbyDanger=PrepareSafety(arena,slot);
        PreparePreferences(arena,slot);
        if(TryBlock(arena,slot,out int blocking)){
            Intent=blocking==0?"블로킹 유지":"길목 선점";return Actions[blocking];
        }
        // Adventurers explore inside the safe region even when a neighboring tile
        // is dangerous. The first-step filter below still forbids entering danger.
        if(firstRisk[0]==0&&nearbyDanger&&settings.Personality!=ChamberBotPersonality.Adventurer){Intent="안전구역 유지";return Vector2Int.zero;}
        for(int a=0;a<5;a++){actionScores[a]=float.NegativeInfinity;noise[a]=(float)random.NextDouble()*.35f;}
        // Bound work independently of map area. Keep a separate beam for every initial
        // input so a promising direction cannot prune all the other immediate escapes.
        for(int first=0;first<5;first++){
            // A safe refuge is never traded for an announced hazard, even when
            // novelty, a distant goal or blocking would give the move more points.
            if(firstRisk[0]==0&&firstRisk[first]!=0)continue;
            frontier[0]=new Path{Cell=own};int count=1;
            for(int depth=0;depth<depths&&count>0;depth++){
                int candidateCount=0;
                for(int i=0;i<count;i++)for(int action=0;action<5;action++){
                    if(depth==0&&action!=first)continue;
                    var path=frontier[i];int next=action==0?path.Cell:arena.Neighbor(path.Cell,Actions[action]);
                    if(next<0||occupied[next]||Distance(arena,own,next)>SearchRadius)continue;
                    LastExpandedNodes++;
                    float risk=Risk(arena,slot,path.Cell,next,depth,action,step);
                    // Survival dominates personality and novelty; danger is never added for variety.
                    float worst=Mathf.Max(path.Risk,risk*1000/(1+depth*.12f));
                    float value=path.Score-(worst-path.Risk)+Preference(arena,slot,next)/depths+(depth==0?noise[action]:0);
                    if(depth==0&&next==beforePrevious)value-=.3f;
                    int target=0;while(target<candidateCount&&candidates[target].Cell!=next)target++;
                    if(target==candidateCount||value>candidates[target].Score){
                        candidates[target]=new Path{Cell=next,Score=value,Risk=worst};
                        if(target==candidateCount)candidateCount++;
                    }
                }
                count=Mathf.Min(BeamWidth,candidateCount);
                for(int i=0;i<count;i++){
                    int best=0;for(int j=1;j<candidateCount;j++)if(candidates[j].Score>candidates[best].Score)best=j;
                    frontier[i]=candidates[best];candidates[best]=candidates[--candidateCount];
                }
            }
            for(int i=0;i<count;i++)actionScores[first]=Mathf.Max(actionScores[first],frontier[i].Score);
        }
        int chosen=0;
        for(int a=1;a<5;a++)if(actionScores[a]>actionScores[chosen])chosen=a;
        Intent=firstRisk[0]>0?"위험 회피":chosen==0?"안전구역 유지":settings.Personality==ChamberBotPersonality.Survivor?"거리 유지":"자리 탐색";
        return Actions[chosen];
    }
}
