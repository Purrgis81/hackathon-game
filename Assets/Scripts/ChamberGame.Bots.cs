using UnityEngine;

public sealed partial class ChamberGame : IChamberBotArena
{
    readonly ChamberBot[] bots=new ChamberBot[Capacity];
    readonly int[] botSpikeIndices=new int[100];
    readonly bool[] botGroundTiles=new bool[100];
    float botObservedAt=float.NaN;
    Phase botObservedPhase;
    int botObservedSpikes=-1;
    int botObservedGroundCount=-1,botObservedGroundSignature;
    void PrepareBots(int seed){
        for(int i=0;i<Capacity;i++)bots[i]=lan.ControlsBots&&lan.IsBot(i)
            ?new ChamberBot(lan.BotSettings(i),unchecked(seed*397+i*7919)):null;
    }
    void ResetBotRound(){botObservedAt=float.NaN;foreach(var bot in bots)bot?.ResetRound();}
    public ChamberBot Bot(int slot)=>slot>=0&&slot<Capacity?bots[slot]:null;
    public Vector2Int BotInput(int slot,float dt){
        if(RoundFinished||CountingDown||phase==Phase.Arrival)return Vector2Int.zero;
        return bots[slot]?.Tick(this,slot,dt)??Vector2Int.zero;
    }
    int IChamberBotArena.CellCount=>board.Count;
    int IChamberBotArena.ActorCount=>participants;
    float IChamberBotArena.Now=>elapsed;
    float IChamberBotArena.MoveSeconds=>stepSeconds;
    // Cover the complete visible warning as well as a minimum reaction window.
    // TravelRisk still excludes unrevealed follow-up attacks.
    float BotGroundBreakDelay=>phase==Phase.GroundWarning?remaining+PatternInterval(GroundBreakSeconds)
        :phase==Phase.GroundCracking?remaining:float.PositiveInfinity;
    float IChamberBotArena.SafetySeconds=>Mathf.Max(1.2f,
        (phase==Phase.GroundWarning||phase==Phase.GroundCracking?BotGroundBreakDelay:
        (phase==Phase.Warning||phase==Phase.FirstStep||phase==Phase.TideWarning
        ||phase==Phase.LavaWarning||phase==Phase.Collapse?remaining:0))+stepSeconds);
    Vector2Int IChamberBotArena.Coordinates(int cell)=>board.Cell(cell);
    int IChamberBotArena.Neighbor(int cell,Vector2Int direction)=>board.Neighbor(cell,direction);
    ChamberBotActor IChamberBotArena.Actor(int slot){
        var p=players[slot];return new ChamberBotActor {Cell=p.Cell,Target=p.Target,Alive=p.Alive,Moving=p.Moving,Stunned=p.Stunned,Sliding=p.Sliding};
    }
    void ObserveBotHazards(){
        if(botObservedAt==elapsed&&botObservedPhase==phase&&botObservedSpikes==spikeOrder.Count
            &&botObservedGroundCount==groundTiles.Count&&botObservedGroundSignature==groundSignature)return;
        botObservedAt=elapsed;botObservedPhase=phase;botObservedSpikes=spikeOrder.Count;
        botObservedGroundCount=groundTiles.Count;botObservedGroundSignature=groundSignature;
        for(int i=0;i<botSpikeIndices.Length;i++)botSpikeIndices[i]=-1;
        System.Array.Clear(botGroundTiles,0,botGroundTiles.Length);
        // One index of the currently revealed front, shared by all bots this simulation tick.
        for(int k=0;k<spikeOrder.Count;k++)botSpikeIndices[spikeOrder[k]]=k;
        foreach(int cell in groundTiles)botGroundTiles[cell]=true;
    }
    bool BotGroundDanger(int from,int to,float delay,float duration){
        float breaks=BotGroundBreakDelay;
        if(float.IsPositiveInfinity(breaks)||delay>=breaks+PatternInterval(GroundMissingSeconds))return false;
        // The runner reserves both endpoints until landing. Leaving a breaking
        // tile is safe only if the entire hop finishes before it disappears.
        return botGroundTiles[to]&&delay+duration>=breaks
            ||botGroundTiles[from]&&delay+duration>breaks;
    }
    bool BotTileDanger(int cell,Vector3 start,Vector3 end,float age,float now,float duration,bool telling,bool spiking){
        int spike=botSpikeIndices[cell];
        if(spiking&&spike>=0){
            float local=age-onsets[spike];
            if(local<SpikeSeconds&&local+duration>=0&&tiles[cell].Contact(start,end,local,duration,globalSpikes,SpikeSeconds)<=1)return true;
        }
        var patch=poison[cell];
        return (patch.EndsAt>now||patch.Pending&&telling)
            &&patch.ForecastContact(start,end,now,duration,telling?elapsed+remaining:float.PositiveInfinity)<=1;
    }
    float IChamberBotArena.TravelRisk(int slot,int from,int to,float delay,float duration){
        if(from<0||to<0||from>=board.Count||to>=board.Count)return 1;
        if(!board.IsPresent(from)||!board.IsPresent(to))return 1;
        var delta=board.Cell(from)-board.Cell(to);
        if(Mathf.Abs(delta.x)+Mathf.Abs(delta.y)>1)return 1;
        if(phase==Phase.Collapse&&delay+duration>=remaining&&to>=board.NextCount)return 1;
        ObserveBotHazards();
        if(BotGroundDanger(from,to,delay,duration))return 1;
        var start=tiles[from].transform.position;var end=tiles[to].transform.position;
        float now=elapsed+delay;
        bool telling=phase==Phase.Warning||phase==Phase.FirstStep;
        float age=(telling?-remaining:strikeSeconds-remaining);
        bool spiking=telling||phase==Phase.Strike;
        // A bot step joins adjacent tile centers. Static trap footprints (including the
        // player's width) cannot reach this segment from any tile except its endpoints.
        if(BotTileDanger(from,start,end,age+delay,now,duration,telling,spiking)
            ||to!=from&&BotTileDanger(to,start,end,age+delay,now,duration,telling,spiking))return 1;
        // Moving hazards still test the complete swept segment, including distant shots.
        if(spiking&&arrow.Armed&&arrow.Contact(start,end,age+delay,duration,age)<=1)return 1;
        // Later lava targets are internal plans until their own marker appears.
        if(lava.Active)for(int i=0;i<lava.Count;i++){
            int target=lava.Target(i);if(target!=from&&target!=to)continue;
            float strike=lava.StrikeAt(i);
            if(elapsed<strike-lava.WarningSeconds||now+duration<strike||now>=strike+LavaBarrage.Hot)continue;
            float a=Mathf.Clamp01((strike-now)/duration),b=Mathf.Clamp01((strike+LavaBarrage.Hot-now)/duration);
            var center=target==from?start:end;
            if(TrapContact.Entry(Vector3.Lerp(start,end,a)-center,Vector3.Lerp(start,end,b)-center,.84f,.84f)<=1)return 1;
        }
        if(players[slot].LastWave!=tideCasts){
            float waveHit=tide.ForecastContact(start,end,now,duration,phase==Phase.TideWarning?elapsed+remaining:float.PositiveInfinity);
            if(waveHit<=1)return .55f;
        }
        return 0;
    }
}
