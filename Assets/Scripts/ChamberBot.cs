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
    public bool Alive,Moving,Stunned,Sliding,IsBot;
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
    int HazardRevision {get;}
    Vector2Int Coordinates(int cell);
    int Neighbor(int cell,Vector2Int direction);
    ChamberBotActor Actor(int slot);
    float TravelRisk(int slot,int from,int to,float delay,float duration,float observedThrough=float.PositiveInfinity);
}

public sealed class ChamberBot
{
    const int Cells=100,MaxDepth=10;
    const float EasyMistakeChance=.20f;
    const float HardLateNoticeChance=.10f;
    public static readonly Vector2Int[] Actions={Vector2Int.zero,Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
    readonly ChamberBotSettings settings;
    readonly System.Random random;
    readonly System.Random attentionRandom;
    readonly float[] visited=new float[Cells],preferences=new float[Cells];
    readonly int[] previousOpponent=new int[ChamberLan.Capacity];
    readonly float[,] headings=new float[ChamberLan.Capacity,4];
    readonly float[] lastSeenMove=new float[ChamberLan.Capacity];
    readonly ChamberBotActor[] actors=new ChamberBotActor[ChamberLan.Capacity];
    readonly ChamberBotActor[] perceivedActors=new ChamberBotActor[ChamberLan.Capacity];
    readonly float[] perceivedMotionAt=new float[ChamberLan.Capacity];
    const int MotionSamples=16;
    struct MotionSample { public ChamberBotActor Actor;public float At; }
    readonly MotionSample[,] motionHistory=new MotionSample[ChamberLan.Capacity,MotionSamples];
    readonly int[] motionHeads=new int[ChamberLan.Capacity],motionCounts=new int[ChamberLan.Capacity];
    int motionBoardCount=-1,committedTarget=-1,committedAction,repeatInputs;
    float commitmentEnds;
    bool repeatedInput;
    int pressureTarget=-1,pressureCell=-1;
    float pressureUntil;
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
    int noticedRevision=-1,standoffTarget=-1,standoffCell=-1,standoffOtherCell=-1;
    float noticedThrough=float.NegativeInfinity,noticeDelay,nextLateNotice,standoffSince,disengageUntil;
    public ChamberBotSettings Settings=>settings;
    public string Intent {get;private set;}="주변 살피기";
    public float Aggression {get;private set;}
    public int Decisions {get;private set;}
    public int Mistakes {get;private set;}
    public int LateNotices {get;private set;}
    public int StandoffBreaks {get;private set;}
    public int RepeatedInputs {get;private set;}
    public int RememberedMoves {get;private set;}
    public int LastRiskChecks {get;private set;}
    public int LastExpandedNodes {get;private set;}
    public int LastScoredCells {get;private set;}
    // Movement opportunities are identical. Skill changes what is noticed and
    // how far it is planned, rather than adding a movement cooldown.
    public float DecisionInterval=>.13f;
    public float PerceptionSeconds=>settings.Difficulty==ChamberBotDifficulty.Easy?.36f:settings.Difficulty==ChamberBotDifficulty.Normal?.18f:.07f;
    public float ObservedThrough=>noticedThrough;
    public float TrackingSeconds=>settings.Difficulty==ChamberBotDifficulty.Easy?.42f:settings.Difficulty==ChamberBotDifficulty.Normal?.30f:.22f;
    public int SearchDepth=>settings.Difficulty==ChamberBotDifficulty.Easy?2:settings.Difficulty==ChamberBotDifficulty.Normal?6:10;
    public int SearchRadius=>settings.Difficulty==ChamberBotDifficulty.Easy?2:settings.Difficulty==ChamberBotDifficulty.Normal?3:4;
    int BeamWidth=>settings.Difficulty==ChamberBotDifficulty.Easy?1:2;

    public ChamberBot(ChamberBotSettings options,int seed){
        settings=options;random=new System.Random(seed);attentionRandom=new System.Random(unchecked(seed*31+1769));ResetRound();
    }
    public void ResetRound(){
        for(int i=0;i<Cells;i++)visited[i]=-100;
        for(int i=0;i<previousOpponent.Length;i++){previousOpponent[i]=-1;lastSeenMove[i]=-100;}
        Array.Clear(headings,0,headings.Length);
        previousCell=beforePrevious=goal=-1;observedAt=goalUntil=stillSince=0;
        countdown=DecisionInterval*(.6f+(float)random.NextDouble()*.4f);
        Aggression=0;Intent="주변 살피기";RememberedMoves=0;
        previousMistake=false;Mistakes=0;
        noticedRevision=standoffTarget=standoffCell=standoffOtherCell=-1;
        noticedThrough=float.NegativeInfinity;noticeDelay=PerceptionSeconds;nextLateNotice=standoffSince=disengageUntil=0;
        LateNotices=StandoffBreaks=0;
        Array.Clear(motionCounts,0,motionCounts.Length);Array.Clear(motionHeads,0,motionHeads.Length);
        motionBoardCount=-1;ClearCommittedInput();RepeatedInputs=0;repeatedInput=false;
        pressureTarget=pressureCell=-1;pressureUntil=0;
        LastRiskChecks=LastExpandedNodes=LastScoredCells=0;
    }
    static bool SameMotion(ChamberBotActor a,ChamberBotActor b)=>a.Cell==b.Cell&&a.Target==b.Target
        &&a.Alive==b.Alive&&a.Moving==b.Moving&&a.Stunned==b.Stunned&&a.Sliding==b.Sliding&&a.IsBot==b.IsBot;
    void CaptureOpponentMotion(IChamberBotArena arena,int slot){
        if(motionBoardCount!=arena.CellCount){
            motionBoardCount=arena.CellCount;Array.Clear(motionCounts,0,motionCounts.Length);ClearCommittedInput();
        }
        float cutoff=arena.Now-TrackingSeconds;
        for(int i=0;i<arena.ActorCount;i++){
            if(i==slot)continue;
            var current=arena.Actor(i);int count=motionCounts[i],head=motionHeads[i];
            if(count==0||!SameMotion(current,motionHistory[i,(head+MotionSamples-1)%MotionSamples].Actor)){
                motionHistory[i,head]=new MotionSample{Actor=current,At=arena.Now};
                motionHeads[i]=head=(head+1)%MotionSamples;motionCounts[i]=count=Math.Min(count+1,MotionSamples);
            }
            int oldest=(head-count+MotionSamples)%MotionSamples;var perceived=motionHistory[i,oldest];
            // Read only past visible movement. A first sighting supplies the initial
            // location; subsequent turns/stops reach tactical decisions after a delay.
            for(int j=0;j<count;j++){
                var sample=motionHistory[i,(oldest+j)%MotionSamples];if(sample.At>cutoff)break;
                perceived=sample;
            }
            perceivedActors[i]=perceived.Actor;perceivedMotionAt[i]=perceived.At;
        }
    }
    void ClearCommittedInput(){committedTarget=-1;committedAction=repeatInputs=0;commitmentEnds=0;}
    void CommitBlockingInput(IChamberBotArena arena,int action,int target){
        ClearCommittedInput();var other=perceivedActors[target];
        if(action==0||!other.Moving||other.Stunned||other.Sliding)return;
        committedTarget=target;committedAction=action;
        repeatInputs=settings.Difficulty==ChamberBotDifficulty.Hard?1:2;
        commitmentEnds=arena.Now+TrackingSeconds+arena.MoveSeconds;
    }
    bool TryRepeatBlockingInput(IChamberBotArena arena,out int action){
        action=committedAction;
        if(repeatInputs<=0)return false;
        if(arena.Now>=commitmentEnds||!actors[committedTarget].Alive||firstRisk[0]>0||firstRisk[action]!=0){
            ClearCommittedInput();return false;
        }
        repeatInputs--;repeatedInput=true;return true;
    }
    void ObserveWarnings(IChamberBotArena arena){
        if(noticedRevision!=arena.HazardRevision){
            noticedRevision=arena.HazardRevision;noticeDelay=PerceptionSeconds;
            if(noticedRevision>0&&settings.Difficulty==ChamberBotDifficulty.Hard&&arena.Now>=nextLateNotice&&attentionRandom.NextDouble()<HardLateNoticeChance){
                noticeDelay+=.18f+(float)attentionRandom.NextDouble()*.14f;
                nextLateNotice=arena.Now+2;LateNotices++;
            }
        }
        // A late notice can postpone new information, never erase a warning
        // already understood. The controller continues moving while noticing.
        noticedThrough=Mathf.Max(noticedThrough,arena.Now-noticeDelay);
    }
    public Vector2Int Tick(IChamberBotArena arena,int slot,float dt){
        countdown-=dt;
        var self=arena.Actor(slot);
        if(!self.Alive||self.Sliding){ClearCommittedInput();return Vector2Int.zero;}
        CaptureOpponentMotion(arena,slot);
        if(countdown>0||self.Moving)return Vector2Int.zero;
        countdown=DecisionInterval*(.9f+(float)random.NextDouble()*.2f);
        if(self.Stunned){ClearCommittedInput();Intent="기절 탈출";return Actions[1+random.Next(4)];}
        UnityEngine.Profiling.Profiler.BeginSample("ChamberBot.Decide");
        try{
            var planned=Decide(arena,slot);var input=ApplyEasyMistake(arena,slot,planned);
            if(input!=planned)ClearCommittedInput();else if(repeatedInput)RepeatedInputs++;
            return input;
        }
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
            // rules or deliberately steps into a recognized hazard covering the move.
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
            var other=perceivedActors[i];
            if(i==slot||!actors[i].Alive||!other.Alive)continue;
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
        var other=motionCounts[otherSlot]>0?perceivedActors[otherSlot]:arena.Actor(otherSlot);
        if(other.Moving)return other.Target;
        if(other.Stunned||arena.Now-lastSeenMove[otherSlot]>3)return other.Cell;
        int direction=-1;float largest=.15f;
        for(int d=0;d<4;d++)if(headings[otherSlot,d]>largest){largest=headings[otherSlot,d];direction=d;}
        int next=direction<0?-1:arena.Neighbor(other.Cell,Actions[direction+1]);
        return next>=0?next:other.Cell;
    }
    int BlockingAnchor(IChamberBotArena arena,int otherSlot,int own){
        var other=perceivedActors[otherSlot];int anchor=other.Moving?other.Target:other.Cell;
        if(settings.Personality!=ChamberBotPersonality.Blocker||!other.Moving||other.Stunned||other.Sliding)return anchor;
        var direction=arena.Coordinates(other.Target)-arena.Coordinates(other.Cell);
        if(Mathf.Abs(direction.x)+Mathf.Abs(direction.y)!=1)return anchor;
        // Estimate progress along the last recognized direction instead of chasing
        // an old position. An unseen turn still sends this estimate the wrong way.
        int steps=Mathf.Clamp(Mathf.FloorToInt((arena.Now-perceivedMotionAt[otherSlot])/arena.MoveSeconds)-1,0,2);
        // Do not predict an opponent walking through the lane we already occupy.
        for(int i=0;i<steps;i++){int next=arena.Neighbor(anchor,direction);if(next<0||next==own)break;anchor=next;}
        return anchor;
    }
    float TravelRisk(IChamberBotArena arena,int slot,int from,int to,float delay,float duration){
        LastRiskChecks++;return arena.TravelRisk(slot,from,to,delay,duration,noticedThrough);
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
        for(int i=0;i<arena.ActorCount;i++)if(i!=slot&&actors[i].Alive&&perceivedActors[i].Alive){
            var p=perceivedActors[i];opportunity|=p.Stunned&&Distance(arena,own,p.Cell)<4;predicted[i]=PredictedCell(arena,i);
        }
        float baseline=settings.Personality==ChamberBotPersonality.Survivor?.06f:
            settings.Personality==ChamberBotPersonality.Blocker?.85f:
            settings.Personality==ChamberBotPersonality.Opportunist?(opportunity?.95f:.25f):0f;
        Aggression=baseline*(1-Mathf.Clamp01(danger))*(exits<2?.2f:1f);
        if(arena.Now<disengageUntil)Aggression=0;
        if(goal<0||goal>=arena.CellCount||arena.Now>=goalUntil||own==goal){
            goal=random.Next(arena.CellCount);goalUntil=arena.Now+2+(float)random.NextDouble()*2;
        }
    }
    void PreparePressure(IChamberBotArena arena,int slot){
        int previous=pressureTarget;pressureTarget=pressureCell=-1;
        if(settings.Personality!=ChamberBotPersonality.Blocker||firstRisk[0]!=0||arena.Now<disengageUntil)return;
        int own=actors[slot].Cell;float best=float.NegativeInfinity;
        for(int i=0;i<arena.ActorCount;i++){
            var other=perceivedActors[i];if(i==slot||!actors[i].Alive||!other.Alive||other.Sliding)continue;
            int anchor=BlockingAnchor(arena,i,own);if(anchor<0||anchor>=arena.CellCount)continue;
            int distance=Distance(arena,own,anchor);
            if(distance>6&&(i!=previous||distance>8||arena.Now>=pressureUntil))continue;
            int ahead=predicted[i];
            if(other.Moving){
                var direction=arena.Coordinates(other.Target)-arena.Coordinates(other.Cell);
                if(Mathf.Abs(direction.x)+Mathf.Abs(direction.y)==1)ahead=arena.Neighbor(anchor,direction);
            }
            int lane=-1;float laneScore=float.NegativeInfinity;
            for(int a=1;a<5;a++){
                int cell=arena.Neighbor(anchor,Actions[a]);
                if(cell<0||occupied[cell]||StationaryRisk(arena,slot,cell)!=0)continue;
                float score=-Distance(arena,own,cell)+(cell==ahead?3f:0)+(cell==own?.2f:0);
                if(score>laneScore){laneScore=score;lane=cell;}
            }
            if(lane<0)continue;
            float priority=-distance+(i==previous?1.25f:0)+(other.Stunned?.4f:0);
            if(priority>best){best=priority;pressureTarget=i;pressureCell=lane;}
        }
        if(pressureTarget>=0&&(pressureTarget!=previous||Distance(arena,own,BlockingAnchor(arena,pressureTarget,own))<=6))pressureUntil=arena.Now+1;
    }
    bool TryBlock(IChamberBotArena arena,int slot,out int chosen,out int target){
        chosen=0;target=-1;
        if(firstRisk[0]!=0||arena.Now<disengageUntil||settings.Personality==ChamberBotPersonality.Adventurer)return false;
        int own=actors[slot].Cell;float best=.35f;
        // Inspect only nearby opponents and the current/adjacent cells. Holding a
        // lane blocks through the same occupancy rule used for human players.
        for(int i=0;i<arena.ActorCount;i++){
            var other=perceivedActors[i];if(i==slot||!actors[i].Alive||!other.Alive||other.Sliding)continue;
            int anchor=BlockingAnchor(arena,i,own);
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
                // Being beside a moving player is not the same as occupying its lane.
                if(a==0&&settings.Personality==ChamberBotPersonality.Blocker&&other.Moving&&cell!=ahead)continue;
                if(a!=0&&SafeExits(arena,slot,cell)==0)continue;
                float score=(1.3f+(cell==ahead?3f:0)+(exits<=2?1.2f:0)+(other.Stunned?1f:0))*Aggression;
                score+=a==0?.5f:-.2f;
                if(score>best){best=score;chosen=a;target=i;}
            }
        }
        return best>.35f;
    }
    bool TryEndStandoff(IChamberBotArena arena,int slot,int target,out int chosen){
        chosen=0;var other=actors[target];int own=actors[slot].Cell;
        if(!other.IsBot||other.Moving||other.Stunned||other.Sliding||Distance(arena,own,other.Cell)!=1){standoffTarget=-1;return false;}
        if(standoffTarget!=target||standoffCell!=own||standoffOtherCell!=other.Cell){
            standoffTarget=target;standoffCell=own;standoffOtherCell=other.Cell;standoffSince=arena.Now;return false;
        }
        // Seat offsets break symmetry; a temporary truce prevents immediately
        // walking back into the same mutual block. Human blocking is unchanged.
        if(arena.Now-standoffSince<1.2f+slot*.18f)return false;
        var toward=arena.Coordinates(other.Cell)-arena.Coordinates(own);float best=float.NegativeInfinity;
        for(int a=1;a<5;a++){
            if(firstRisk[a]!=0)continue;
            int next=arena.Neighbor(own,Actions[a]);
            bool sideways=Actions[a].x*toward.x+Actions[a].y*toward.y==0;
            float score=SafeExits(arena,slot,next)+(sideways?.75f:0)+(float)random.NextDouble()*.2f;
            if(score>best){best=score;chosen=a;}
        }
        if(chosen==0)return false; // Keep the refuge when no safe way out exists.
        disengageUntil=arena.Now+1.5f;Aggression=0;standoffTarget=-1;StandoffBreaks++;return true;
    }
    float Preference(IChamberBotArena arena,int slot,int cell){
        if(preferenceStamp[cell]==decision)return preferences[cell];
        preferenceStamp[cell]=decision;LastScoredCells++;
        int own=actors[slot].Cell;
        float room=0,nearest=20,block=0;
        for(int a=1;a<5;a++){int n=arena.Neighbor(cell,Actions[a]);if(n>=0&&!occupied[n])room++;}
        for(int i=0;i<arena.ActorCount;i++)if(i!=slot){
            var other=perceivedActors[i];if(!actors[i].Alive||!other.Alive)continue;
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
        if(pressureCell>=0)preferences[cell]-=Distance(arena,cell,pressureCell)*2.2f*Aggression;
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
        repeatedInput=false;
        LastRiskChecks=LastExpandedNodes=LastScoredCells=0;
        Array.Clear(occupied,0,occupied.Length);
        for(int i=0;i<arena.ActorCount;i++){
            var actor=actors[i]=arena.Actor(i);
            if(i!=slot&&actor.Alive){occupied[actor.Cell]=true;occupied[actor.Target]=true;}
        }
        Observe(arena,slot);ObserveWarnings(arena);Decisions++;decision++;
        if(decision==int.MaxValue){Array.Clear(riskStamp,0,riskStamp.Length);Array.Clear(preferenceStamp,0,preferenceStamp.Length);Array.Clear(safeStamp,0,safeStamp.Length);decision=1;}
        int own=actors[slot].Cell,depths=SearchDepth;
        float step=Mathf.Max(arena.MoveSeconds+1f/ChamberLan.TickRate,DecisionInterval);
        bool nearbyDanger=PrepareSafety(arena,slot);
        PreparePreferences(arena,slot);
        PreparePressure(arena,slot);
        if(TryRepeatBlockingInput(arena,out int continued)){
            standoffTarget=-1;Intent="이전 방향 입력";return Actions[continued];
        }
        if(TryBlock(arena,slot,out int blocking,out int blockTarget)){
            if(blocking==0&&TryEndStandoff(arena,slot,blockTarget,out int release)){
                Intent="견제 해제 · 재배치";return Actions[release];
            }
            if(blocking!=0)standoffTarget=-1;
            CommitBlockingInput(arena,blocking,blockTarget);
            Intent=blocking==0?"블로킹 유지":"길목 선점";return Actions[blocking];
        }
        ClearCommittedInput();
        standoffTarget=-1;
        // Adventurers explore inside the safe region even when a neighboring tile
        // is dangerous. The first-step filter below still forbids entering danger.
        if(firstRisk[0]==0&&nearbyDanger&&pressureCell<0&&settings.Personality!=ChamberBotPersonality.Adventurer){Intent="안전구역 유지";return Vector2Int.zero;}
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
        Intent=firstRisk[0]>0?"위험 회피":chosen==0?"안전구역 유지":pressureCell>=0?"견제 접근":settings.Personality==ChamberBotPersonality.Survivor?"거리 유지":"자리 탐색";
        return Actions[chosen];
    }
}
