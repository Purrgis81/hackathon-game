using System.Collections.Generic;
using UnityEngine;

public sealed class ChamberGame : MonoBehaviour
{
    public Transform explorer;
    public SpikeTile[] tiles;
    public Camera chamberCamera;
    [Min(0.01f)] public float stepSeconds = 0.16f;
    [Min(0.2f)] public float firstWarning = 1.35f;
    const float SpikeSeconds = 0.46f;
    const string RecordKey = "StoneChamber.Best";
    enum Phase { Arrival, AwaitStep, FirstStep, Warning, Strike, Rest, Collapse, LiftWarning, LiftTravel, TideWarning, TideTravel, LavaWarning, LavaTravel, Dead, Returning }
    Phase phase;
    ArrowVolley arrow;
    TidalWave tide;
    LavaBarrage lava;
    int tideCasts;
    int liftCasts;
    bool HasControlledPlayers {
        get{
            for(int i=0;i<participants;i++)if(players[i].Alive&&(players[i].Sliding||players[i].Stunned))return true;
            return false;
        }
    }
    const int Capacity=ChamberLan.Capacity;
    readonly List<int> spiral=new List<int>(100);
    readonly List<PoisonFloor> livePoison=new List<PoisonFloor>();
    int spiralIndex,spikeCasts,arrowCasts,finalBursts;
    bool spiralWave,spiralPause,singleShelter;
    float nextLift,liftLength;
    string patternName;
    readonly ChamberProfile[] profiles=new ChamberProfile[Capacity];
    readonly ChamberRunner[] players=new ChamberRunner[Capacity];
    ChamberRunner first => players[0];
    readonly int[] wins=new int[Capacity];
    readonly float[] hits=new float[Capacity], knockedOutAt=new float[Capacity];
    readonly TrapKind[] causes=new TrapKind[Capacity];
    readonly Vector3[] destinations=new Vector3[Capacity];
    int participants=1, priority;
    bool duel => participants>1;
    ChamberLan lan;
    AbyssLighting lighting;
    readonly bool[] exits=new bool[100];
    const float SpikeLead=2f;
    const float FollowBeat=.34f;
    readonly List<float> onsets=new List<float>();
    int budget,batch,heading;
    float nextReveal, nextCollapse;
    string result;
    UnityEngine.Random.State duelRandom;
    public bool RoundFinished => phase==Phase.Dead || phase==Phase.Returning;
    ChamberArrival arrival;
    ChamberAudio sound;
    DeathFocus deathFocus;
    float transitionTime;
    PoisonFloor[] poison;
    Material poisonMaterial;
    string deathMessage;
    float strikeSeconds;
    float nextBurstAt;
    int lateCasts;
    bool globalSpikes;
    float Pressure => Mathf.Clamp01(elapsed/120f);
    static readonly Vector2Int[] Directions={Vector2Int.right,Vector2Int.up,Vector2Int.left,Vector2Int.down};
    readonly List<int> spikeOrder=new List<int>(49);
    PotionVolley potion;
    int casts, poisonCenter=-1;
    bool followPotion;
    RisingBoard board;
    bool[] dangerous;
    int wave, openingTile;

    float elapsed, best, remaining, warningSeconds;
    GUIStyle title, hint, score;

    void Start()
    {
        // A scene already open in the editor can still contain the former chamber dressing.
        foreach (Transform child in transform)
        {
            switch (child.name)
            {
                case "Recessed floor": case "Stone plinth": case "Bronze course":
                case "Stepped foundation": case "column": case "pot":
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                    break;
            }
        }
        chamberCamera.clearFlags=CameraClearFlags.SolidColor;
        chamberCamera.backgroundColor=Color.black;
        board=new RisingBoard(tiles,chamberCamera);
        tiles=board.Tiles;
        dangerous=new bool[tiles.Length];
        for(int i=Capacity-1;i>=0;i--){
            var feet=i==0?explorer:Instantiate(explorer,explorer.parent);
            feet.name=$"{i+1}P explorer";players[i]=new ChamberRunner(feet);
        }
        lan=new ChamberLan(this);ApplyProfiles();
        arrow=new ArrowVolley(gameObject);
        potion=gameObject.AddComponent<PotionVolley>();
        poisonMaterial = new Material(Resources.Load<Shader>("PoisonFloor"));
        poison = new PoisonFloor[tiles.Length];
        for (int i=0; i<tiles.Length; i++) poison[i] = new PoisonFloor(tiles[i],poisonMaterial);
        best = PlayerPrefs.GetFloat(RecordKey);
        ResetBoard();
        sound = gameObject.AddComponent<ChamberAudio>();
        deathFocus = gameObject.AddComponent<DeathFocus>();
        var quad = tiles[4].warning.GetComponent<MeshFilter>().sharedMesh;
        tide=new TidalWave(transform,quad);lava=new LavaBarrage(transform,quad);
        deathFocus.Prepare(chamberCamera,quad);
        lighting=chamberCamera.gameObject.AddComponent<AbyssLighting>();lighting.Prepare(explorer,tiles,quad);
        arrival = gameObject.AddComponent<ChamberArrival>();
        arrival.Prepare(this,first.Hop);
        phase = Phase.Arrival;
    }

    public void ApplyProfiles(){
        for(int i=0;i<participants;i++){
            profiles[i]=duel?lan.Profile(i):lan.Local;
            var tint=new MaterialPropertyBlock();tint.SetColor("_Color",profiles[i].Tint(i));
            foreach(var renderer in players[i].View.GetComponentsInChildren<Renderer>(true))renderer.SetPropertyBlock(tint);
        }
    }
    string PlayerLabel(int i)=>string.IsNullOrEmpty(profiles[i].Name)?$"{i+1}P":$"{i+1}P {profiles[i].Name}";

    void ResetBoard()
    {
        foreach (var tile in tiles) tile.ResetTile();
        foreach (var patch in poison) patch.Clear();
        board.Reset();
        potion.Clear();
        spikeOrder.Clear();
        casts=0;
        lateCasts=arrowCasts=spikeCasts=finalBursts=0;nextLift=19;
        spiralWave=singleShelter=false;spiral.Clear();livePoison.Clear();patternName="";
        nextBurstAt=28f;
        globalSpikes=false;
        budget=0;onsets.Clear();nextCollapse=12;result="";
        followPotion=false;
        poisonCenter=-1;
        arrow.Clear();
        tide?.Clear();lava?.Clear();tideCasts=liftCasts=0;
        int[] starts={0,2,6,8,4};
        for(int i=0;i<Capacity;i++){
            players[i].Reset(duel ? starts[i] : 4,board);
            players[i].Feet.gameObject.SetActive(i<participants);
        }
        if(lighting)lighting.Follow(players[duel ? lan.Slot : 0].View);
        openingTile=4;
        elapsed = 0;
        wave = 0;
    }

    void OpenForRetry(float dt)
    {
        transitionTime += dt;
        if (phase == Phase.Dead)
        {
            deathFocus.Frame(transitionTime);
            if (transitionTime < DeathFocus.Hold + DeathFocus.Close) return;
            transitionTime -= DeathFocus.Hold + DeathFocus.Close;
            ResetBoard();
            sound.ResumeMusic();
            phase = Phase.Returning;
        }
        deathFocus.Reveal(transitionTime/DeathFocus.Reopen);
        if (transitionTime >= DeathFocus.Reopen) phase = Phase.AwaitStep;
    }

    void Warn()
    {
        if(board.Width>3 && elapsed>=nextCollapse&&!lava.Active&&!HasControlledPlayers){
            remaining=RisingBoard.CollapseLead;phase=Phase.Collapse;board.Preview(0);
            sound.Latch(0,true);return;
        }
        if(elapsed>=nextLift&&!lava.Active&&!HasControlledPlayers){StartLift();return;}
        singleShelter=spiralWave=false;spiral.Clear();patternName="";
        System.Array.Clear(dangerous,0,dangerous.Length);
        System.Array.Clear(exits,0,exits.Length);
        bool potionLesson=wave==3||wave==6;
        bool potionWave=wave>=3&&wave%3==0;
        globalSpikes=elapsed>=nextBurstAt&&!potionWave&&!followPotion&&!HasControlledPlayers;
        bool useArrow=wave>0&&wave%4!=2&&!potionLesson&&!globalSpikes;
        bool useSpike=wave!=1&&!potionLesson;
        int leader=wave%participants;
        while(!players[leader].Alive)leader=(leader+1)%participants;
        int origin=players[leader].Target;
        int escape=EscapeNeighbor(origin);
        bool horizontal=board.Cell(escape).y!=board.Cell(origin).y;
        exits[escape]=true;
        for(int offset=1;offset<participants;offset++){
            var actor=players[(leader+offset)%participants];if(!actor.Alive)continue;
            int from=actor.Target,choice=-1;float bestCost=float.PositiveInfinity;
            for(int i=0;i<board.Count;i++){
                if(i!=from&&!Neighbors(i,from))continue;
                var cell=board.Cell(i);var lane=board.Cell(origin);
                if(useArrow&&(horizontal?cell.y==lane.y:cell.x==lane.x))continue;
                float cost=(exits[i]?10:0)+(i==origin?5:0)+Mathf.Max(0,SafeAfter(i)-elapsed)+(i==from?.0f:.05f);
                if(cost<bestCost){bestCost=cost;choice=i;}
            }
            if(choice>=0)exits[choice]=true;
        }
        if(wave%7==2){
            arrow.Clear();spikeOrder.Clear();onsets.Clear();budget=0;potion.Clear();
            tide.Plan(board,origin,Pressure,tideCasts++);
            patternName=$"밀려오는 파도 · {tide.Columns}칸 · 옆으로 피하세요";
            phase=Phase.TideWarning;remaining=warningSeconds=TidalWave.WarningSeconds;
            wave++;sound.WaveWarning();return;
        }
        if(wave%7==4&&!lava.Active){
            arrow.Clear();spikeOrder.Clear();onsets.Clear();budget=0;potion.Clear();
            foreach(var tile in tiles)tile.ResetTile();
            lava.Plan(board,exits,elapsed,Pressure);
            sound.LavaWarning();patternName="용암 분출 · 붉은 표적에서 벗어나세요";
            wave++;phase=Phase.LavaWarning;remaining=warningSeconds=LavaBarrage.Lead;return;
        }
        if(useArrow){
            int pattern=arrowCasts++%7;
            arrow.Plan(board,origin,horizontal,wave%4<2?1:-1,pattern,exits,Pressure);
            patternName=new[]{"화살 · 단발","화살 · 왕복","화살 · 평행 사격","화살 · 교차 사격","화살 · 시차 연사","화살 · 사방 순환","화살 · 팔방 회전"}[pattern];
            if(pattern>=5){useSpike=false;potionWave=false;}
        }
        spikeOrder.Clear();onsets.Clear();budget=0;
        if(globalSpikes){
            singleShelter=board.Width==3&&finalBursts++%2==0;
            int refuge=singleShelter?PickShelter():-1;
            patternName=singleShelter?"최후의 피난처 · 안전 칸 하나":"전역 가시";
            for(int i=0;i<board.Count;i++){
                var cell=board.Cell(i);
                if(singleShelter?i==refuge:exits[i]||(cell.x%3==0&&cell.y%3==0))tiles[i].ShowWarning(0,-1);
                else AddSpike(i,0);
            }
            nextBurstAt=elapsed+Mathf.Lerp(18,12,Pressure);
        }else if(useSpike){
            budget=Mathf.Clamp(Mathf.RoundToInt(board.Count*Mathf.Lerp(.20f,.72f,Pressure)),3,28);
            batch=Mathf.Max(1,Mathf.CeilToInt((budget-1)/4f));
            heading=Random.Range(0,4);nextReveal=0;
            int style=spikeCasts++%4;
            spiralWave=style==1||style==2;spiralPause=style!=0;
            if(spiralWave){
                BuildSpiral(style==2);
                budget=spiral.Count;batch=Mathf.Max(1,Mathf.CeilToInt((budget-1)/10f));
                if(budget>0)AddSpike(spiral[spiralIndex++],0);
                patternName=style==1?"가시 · 안쪽으로 감기는 나선":"가시 · 바깥으로 풀리는 나선";
            }else if(!exits[origin])AddSpike(origin,0);
            else for(int i=0;i<board.Count;i++)if(!exits[i]&&Neighbors(origin,i)){AddSpike(i,0);break;}
            if(style==3)patternName="가시 · 한 박자 쉬고 쇄도";
            if(spikeOrder.Count==0)budget=0;
        }
        if(potionWave)
        {
            bool late=elapsed>=60;
            var shape=late && lateCasts%3==0 ? SpillShape.Star :
                (casts%2==0 ? SpillShape.Cross : SpillShape.Line);
            int center=PotionPattern.Mark(board,poison,origin,escape,shape,exits);
            if(center>=0)
            {
                casts++;
                if(late)lateCasts++;
                poisonCenter=center;
                potion.Aim(poison[center].center);
            }
        }
        float lastSpike=spikeOrder.Count==0 ? 0 : SpikeSeconds;
        if(budget>1)lastSpike=SpikeLead+FollowBeat*(Mathf.CeilToInt((budget-1)/(float)batch)-1)+(spiralPause?.68f:0)+SpikeSeconds+.035f;
        strikeSeconds=Mathf.Max(.30f,Mathf.Max(useArrow?arrow.FlightTime:0,lastSpike));
        float lead=spikeOrder.Count>0 ? SpikeLead : followPotion?.63f:Mathf.Lerp(firstWarning,.85f,Pressure);
        wave++;
        warningSeconds=lead;
        for(int i=0;i<board.Count;i++)if(exits[i])warningSeconds=Mathf.Max(warningSeconds,SafeAfter(i)-elapsed+stepSeconds+.16f);
        followPotion=false;
        remaining = warningSeconds;
        phase = Phase.Warning;
        sound.Latch((useArrow ? arrow.LaunchPosition.x : tiles[origin].transform.position.x)/6f);
        ShowSpikeSequence(remaining);
    }

    int PickShelter(){
        int choice=0,best=int.MaxValue;
        for(int i=0;i<board.Count;i++){
            int longest=0;
            bool occupied=false;
            for(int j=0;j<participants;j++)if(players[j].Alive){
                var d=board.Cell(i)-board.Cell(players[j].Target);
                occupied|=players[j].Occupies(i);
                longest=Mathf.Max(longest,Mathf.Abs(d.x)+Mathf.Abs(d.y));
            }
            int cost=(occupied?1000:0)+(lava.LastDanger(i)>elapsed?2000:0)+Mathf.Max(0,longest-2)*100+Random.Range(0,100);
            if(cost<best){best=cost;choice=i;}
        }
        return choice;
    }
    void BuildSpiral(bool outward){
        spiralIndex=0;
        int lo=-(board.Width-1)/2,hi=board.Width/2;
        for(int a=lo,b=hi;a<=b;a++,b--){
            for(int x=a;x<=b;x++)spiral.Add(board.At(x,a));
            for(int y=a+1;y<=b;y++)spiral.Add(board.At(b,y));
            if(a==b)break;
            for(int x=b-1;x>=a;x--)spiral.Add(board.At(x,b));
            for(int y=b-1;y>a;y--)spiral.Add(board.At(a,y));
        }
        spiral.RemoveAll(i=>exits[i]);if(outward)spiral.Reverse();
    }
    void StartLift(){
        int lead=wave%participants;while(!players[lead].Alive)lead=(lead+1)%participants;
        int origin=players[lead].Target;
        board.PlanLift(origin,liftCasts++);arrow.PlanLift(board,Pressure);
        spikeOrder.Clear();onsets.Clear();budget=0;potion.Clear();
        patternName="승강 시험 · 내려갈 곳과 복귀할 곳을 고르세요";
        remaining=warningSeconds=2.8f;phase=Phase.LiftWarning;sound.Latch(0);
        nextLift=elapsed+26;
    }

    bool Neighbors(int a, int b)
    {
        var delta=board.Cell(a)-board.Cell(b);
        return Mathf.Abs(delta.x)+Mathf.Abs(delta.y)==1;
    }

    float SafeAfter(int cell)=>Mathf.Max(poison[cell].EndsAt,lava.LastDanger(cell));

    int EscapeNeighbor(int origin)
    {
        int best = -1, bestScore = -1;
        float bestWait=float.PositiveInfinity;
        for (int i=0; i<board.Count; i++)
        {
            if (!Neighbors(origin,i)) continue;
            float wait=Mathf.Max(0,SafeAfter(i)-elapsed);
            bool preferredAxis = (board.Cell(i).y != board.Cell(origin).y) == (wave%2==1);
            int score = Random.Range(0,100) + (preferredAxis ? 100 : 0);
            if (wait<bestWait || (wait==bestWait && score>bestScore))
            {bestWait=wait;bestScore=score;best=i;}
        }
        return best;
    }

    void AddSpike(int cell,float onset){
        dangerous[cell]=true;spikeOrder.Add(cell);onsets.Add(onset);
    }

    // Only the first tile exists during the initial tell. Grow the front from already revealed tiles.
    void GrowFront(float age){
        if(budget==0||spikeOrder.Count>=budget||age+.00001f<nextReveal)return;
        int limit=Mathf.Min(budget,spikeOrder.Count+batch);
        int previous=spikeOrder.Count;
        if(spiralWave){
            while(spikeOrder.Count<limit&&spiralIndex<spiral.Count){
                int n=spiral[spiralIndex++];AddSpike(n,age+SpikeLead);tiles[n].ShowWarning(0);
            }
        }else for(int head=0;head<spikeOrder.Count&&spikeOrder.Count<limit;head++){
            for(int d=0;d<4&&spikeOrder.Count<limit;d++){
                int n=board.Neighbor(spikeOrder[head],Directions[(heading+d)%4]);
                if(n>=0&&!exits[n]&&!dangerous[n]){
                    AddSpike(n,age+SpikeLead);
                    tiles[n].ShowWarning(0);
                }
            }
        }
        if(spikeOrder.Count<limit)budget=spikeOrder.Count;
        nextReveal=age+FollowBeat+(spiralPause&&previous<budget/2&&spikeOrder.Count>=budget/2?.68f:0);
    }
    void ShowSpikeSequence(float left){
        for(int k=0;k<spikeOrder.Count;k++)tiles[spikeOrder[k]].ShowWarning(Mathf.Clamp01(1-left/warningSeconds),globalSpikes?0:1);
    }

    void Move(ChamberRunner actor,Vector2Int direction)
    {
        if(RoundFinished || phase==Phase.Arrival || direction==Vector2Int.zero)return;
        if(!actor.Alive)return;
        if(actor.Mash()){sound.StunTap(actor.WakeCount);return;}
        if(actor.Sliding)return;
        if(!actor.Moving){
            int next=board.Neighbor(actor.Cell,direction);
            for(int i=0;i<participants&&next>=0;i++){
                var other=players[i];
                if(other==actor||!other.Alive||!other.Occupies(next))continue;
                if(elapsed-actor.BlockAt>.14f){
                    actor.BlockAt=elapsed;actor.Blocker=i;
                    actor.BlockPoint=(actor.Feet.position+tiles[next].transform.position)*.5f+Vector3.up*.78f;
                    sound.Block(actor.BlockPoint.x/10f);
                }
                return;
            }
        }
        if(!actor.TryStep(direction,board))return;
        sound.PushOff();
        if(phase!=Phase.AwaitStep)return;
        openingTile=actor.Cell;
        System.Array.Clear(dangerous,0,dangerous.Length);
        spikeOrder.Clear();onsets.Clear();AddSpike(openingTile,0);budget=0;
        dangerous[openingTile]=true;
        tiles[openingTile].ShowWarning(0);
        phase=Phase.FirstStep;
        remaining=warningSeconds=SpikeLead;
        strikeSeconds=SpikeSeconds;
        wave=1;
        sound.Latch(0);
    }

    static Vector2Int ReadDirection()
    {
        if(Input.GetKeyDown(KeyCode.UpArrow)||Input.GetKeyDown(KeyCode.W))return Vector2Int.up;
        if(Input.GetKeyDown(KeyCode.DownArrow)||Input.GetKeyDown(KeyCode.S))return Vector2Int.down;
        if(Input.GetKeyDown(KeyCode.LeftArrow)||Input.GetKeyDown(KeyCode.A))return Vector2Int.left;
        if(Input.GetKeyDown(KeyCode.RightArrow)||Input.GetKeyDown(KeyCode.D))return Vector2Int.right;
        return Vector2Int.zero;
    }

    public void BeginDuel(int seed,int count)
    {
        var outside=Random.state;Random.InitState(seed);duelRandom=Random.state;Random.state=outside;
        participants=count;priority=(seed&int.MaxValue)%count;
        System.Array.Clear(wins,0,wins.Length);ApplyProfiles();ResetBoard();phase=Phase.AwaitStep;sound.ResumeMusic();
    }
    public void EndDuel(){
        participants=1;priority=0;ApplyProfiles();ResetBoard();deathFocus.Reveal(1);phase=Phase.AwaitStep;sound.ResumeMusic();
    }
    public int DuelTick(Vector2Int[] keys,float dt){
        var outside=Random.state;Random.state=duelRandom;
        try{
            if(RoundFinished)OpenForRetry(dt);
            else{
                for(int j=0;j<participants;j++){int i=(j+priority)%participants;Move(players[i],keys[i]);}
                priority=(priority+1)%participants;Simulate(dt);
            }
            unchecked{
                int h=((int)phase*31+wave)*107+board.Count;
                for(int i=0;i<participants;i++){var p=players[i];h=h*107+p.Cell;h=h*107+p.Target;h=h*31+wins[i];h=h*2+(p.Alive?1:0);}
                for(int i=0;i<participants;i++){
                    h=h*2+(players[i].Sliding?1:0);h=h*2+(players[i].Stunned?1:0);h=h*7+players[i].WakeCount;
                }
                h=h*107+arrow.Signature;h=h*7+board.LiftPattern;
                foreach(int n in spikeOrder)h=h*107+n;
                h=h*107+tide.Signature;h=h*107+lava.Signature;
                return (h*107+poisonCenter)*31+(arrow.Armed?Mathf.RoundToInt(arrow.Speed*100):0);
            }
        }finally{duelRandom=Random.state;Random.state=outside;}
    }

    public void Anticipate(int slot,Vector2Int dir){
        var p=players[slot];if(p.Moving||!p.Alive||p.Sliding||p.Stunned)return;
        int target=board.Neighbor(p.Cell,dir);
        for(int i=0;i<participants;i++)if(i!=slot&&players[i].Alive&&players[i].Occupies(target))return;
        p.Anticipate(target);
    }
    public void ConfirmLocalInput(int slot)=>players[slot].ConfirmInput();
    void LateUpdate(){
        if(board==null)return;
        if(lan.Guest)for(int i=0;i<participants;i++)
            players[i].Present(board,players[i].Duration(stepSeconds),Mathf.Min(.06f,Time.unscaledDeltaTime),RoundFinished);
        foreach(var patch in poison)patch.Draw(elapsed);
        if(tide!=null){tide.Draw(elapsed);lava.Draw(elapsed);}
    }
    void Update()
    {
        chamberCamera.fieldOfView=Mathf.Max(38f,Camera.HorizontalToVerticalFieldOfView(42f,chamberCamera.aspect));
        if(lan.Editing)return;
        if(lan.Busy){lan.Poll(Time.unscaledDeltaTime,ReadDirection());return;}
        if(phase==Phase.Arrival){Tick(Time.deltaTime);return;}
        if(RoundFinished){OpenForRetry(Time.unscaledDeltaTime);return;}
        Move(first,ReadDirection());
        Simulate(Mathf.Min(Time.deltaTime,.12f));
    }

    void Simulate(float dt)
    {
        if(phase==Phase.AwaitStep)return;
        while(dt>0 && phase!=Phase.Dead){
            float slice=Mathf.Min(dt,.005f);Tick(slice);dt-=slice;
        }
    }
    void Tick(float dt)
    {
        if (phase == Phase.Arrival)
        {
            if (arrival.Advance(dt))
            {
                phase = Phase.AwaitStep;
                sound.ResumeMusic();
                lan.OpenFromCommandLine();
            }
            return;
        }
        // Split at arrivals and phase boundaries so a newly active trap is sampled at its actual time.
        while (dt > 0 && phase != Phase.Dead && phase != Phase.Returning && phase != Phase.Arrival && phase != Phase.AwaitStep)
        {
            PlanForcedMovement();
            float slice = Mathf.Min(dt, remaining);
            for(int i=0;i<participants;i++)if(players[i].Alive)slice=Mathf.Min(slice,players[i].TimeToLand(players[i].Duration(stepSeconds)));
            float strikeAge = strikeSeconds-remaining;
            if(phase==Phase.Strike)
            {
                GrowFront(strikeAge);
                for(int k=0;k<spikeOrder.Count;k++)
                {
                    float onset=onsets[k];
                    if(onset-strikeAge>.00001f)slice=Mathf.Min(slice,onset-strikeAge);
                    if(onset+SpikeSeconds-strikeAge>.00001f)slice=Mathf.Min(slice,onset+SpikeSeconds-strikeAge);
                }
            }
            if(phase==Phase.LiftTravel){
                float age=liftLength-remaining;
                float depth=Mathf.Min(Mathf.Clamp01((age+slice)/.55f),1-Mathf.Clamp01((age+slice-ArrowVolley.LiftRiseAt)/ArrowVolley.LiftRiseSeconds));
                board.LiftHeight(depth);board.PreviewLift(1);
                arrow.Sample(age+slice,sound);
            }
            for(int i=0;i<participants;i++)if(players[i].Alive){
                var p=players[i];
                destinations[i]=p.NextPosition(slice,p.Duration(stepSeconds),board);
                if(!p.Moving||Mathf.Abs(tiles[p.Cell].transform.position.y-tiles[p.Target].transform.position.y)<.01f)
                    destinations[i].y=tiles[p.Cell].transform.position.y;
            }
            float used=AdvanceActors(strikeAge,slice);
            if(phase==Phase.Dead)return;
            slice=used;
            elapsed+=slice;board.Animate(slice);
            for(int i=0;i<participants;i++)if(players[i].Alive)players[i].StatusTick(slice);
            for(int i=0;i<participants;i++)if(!players[i].Alive&&elapsed-knockedOutAt[i]>.65f)players[i].Feet.gameObject.SetActive(false);
            remaining -= slice;
            if (phase==Phase.Warning && arrow.Armed && remaining<=.24f && remaining+slice>.24f)
                sound.Latch(arrow.LaunchPosition.x/6f,true);
            dt -= slice;
            if (remaining <= .000001f) NextPhase();
            if (phase == Phase.Dead) return;
            for(int j=0;j<participants;j++){var actor=players[(priority+j)%participants];if(actor.Alive)ConsumeBuffered(actor);}
        }
        if (phase == Phase.FirstStep)
            tiles[openingTile].ShowWarning(1-remaining/warningSeconds);
        else if(phase==Phase.LiftWarning){
            board.PreviewLift(1-remaining/warningSeconds);arrow.Draw(1-remaining/warningSeconds,warningSeconds-remaining);
        }
        else if(phase==Phase.Collapse)board.Preview(1-remaining/RisingBoard.CollapseLead);
        else if (phase == Phase.Warning)
        {
            ShowSpikeSequence(remaining);
            arrow.Draw(1-remaining/warningSeconds,warningSeconds-remaining);
            potion.Fall(remaining);
        }
    }

    void PlanForcedMovement(){
        for(int j=0;j<participants;j++){
            var actor=players[(priority+j)%participants];
            if(!actor.Alive||!actor.Sliding||actor.Moving)continue;
            int next=board.Neighbor(actor.Cell,actor.SlideDirection);
            if(next<0){actor.StartStun();sound.Block(actor.Feet.position.x/10f);continue;}
            ChamberRunner blocker=null;
            for(int i=0;i<participants;i++){
                var other=players[i];
                if(other!=actor&&other.Alive&&other.Occupies(next)){blocker=other;break;}
            }
            if(blocker!=null){
                if(blocker.LastWave!=actor.LastWave){
                    blocker.WavePush(actor.SlideDirection,actor.LastWave,board);sound.WaveCaught();
                }else if(!blocker.Sliding){actor.StartStun();sound.Block(actor.Feet.position.x/10f);}
                continue;
            }
            actor.TryStep(actor.SlideDirection,board);
        }
    }

    void ConsumeBuffered(ChamberRunner actor)
    {
        if(actor.Moving || actor.Buffered==Vector2Int.zero)return;
        var direction=actor.Buffered;actor.Buffered=Vector2Int.zero;
        Move(actor,direction);
    }

    float EarliestHit(Vector3 from,Vector3 to,float age,float slice,out TrapKind cause)
    {
        float spikeHit = float.PositiveInfinity;
        bool striking = phase == Phase.Strike;
        for(int k=0;striking && k<spikeOrder.Count;k++)
        {
            float local=age-onsets[k];
            if(local>=-.00001f && local<SpikeSeconds-.00001f)
                spikeHit=Mathf.Min(spikeHit,tiles[spikeOrder[k]].Contact(from,to,local,slice,globalSpikes));
        }
        bool lift=phase==Phase.LiftTravel;
        float flightAge=lift?liftLength-remaining:age;
        float arrowHit=(striking||lift)&&arrow.Armed
            ? arrow.Contact(from,to,flightAge,slice):float.PositiveInfinity;
        float poisonHit=float.PositiveInfinity;
        foreach(var patch in livePoison)poisonHit=Mathf.Min(poisonHit,patch.Contact(from,to,elapsed,slice));
        float hit = Mathf.Min(Mathf.Min(spikeHit,arrowHit),poisonHit);
        cause=hit==poisonHit ? TrapKind.Poison : spikeHit<=arrowHit ? TrapKind.Spike : TrapKind.Arrow;
        float waveHit=tide.Contact(from,to,elapsed,slice),lavaHit=lava.Contact(from,to,elapsed,slice);
        if(waveHit<hit){hit=waveHit;cause=TrapKind.Wave;}
        if(lavaHit<hit){hit=lavaHit;cause=TrapKind.Lava;}
        return hit;
    }

    float AdvanceActors(float age,float slice)
    {
        float hit=1f;bool touched=false;
        for(int i=0;i<participants;i++){
            var p=players[i];hits[i]=p.Alive&&!p.Protected ? EarliestHit(p.Feet.position,destinations[i],age,slice,out causes[i]):float.PositiveInfinity;
            if(hits[i]<=1){hit=Mathf.Min(hit,hits[i]);touched=true;}
        }
        float actualAge=age+slice*hit;
        lava.Sample(elapsed+slice*hit,sound);
        if(touched){tide.Draw(elapsed+slice*hit);lava.Draw(elapsed+slice*hit);}
        bool striking=phase==Phase.Strike;
        if(striking)
        {
            potion.Impact(actualAge);
            if(arrow.Armed)arrow.Sample(actualAge,sound);
            for(int k=0;k<spikeOrder.Count;k++)
            {
                var tile=tiles[spikeOrder[k]];
                float local=actualAge-onsets[k];
                if(local<-.00001f){tile.ShowWarning(Mathf.Clamp01(1+local/SpikeLead));continue;}
                if(local>=SpikeSeconds-.00001f){if(tile.spikes.gameObject.activeSelf)tile.ResetTile();continue;}
                if(!tile.spikes.gameObject.activeSelf)
                {
                    tile.Strike(globalSpikes);
                    if(k==0 || onsets[k]!=onsets[k-1])sound.Activate(TrapKind.Spike);
                }
                tile.Sample(local,SpikeSeconds,globalSpikes);
            }
        }
        float used=slice*hit;
        for(int i=0;i<participants;i++){
            var p=players[i];if(!p.Alive)continue;
            bool lost=touched&&hits[i]<=hit+.00001f;
            if(p.Travel(Vector3.Lerp(p.Feet.position,destinations[i],hit),used,p.Duration(stepSeconds),lost))sound.Footfall();
            if(lost){
                if(causes[i]==TrapKind.Wave){p.WavePush(tide.PushDirection,tideCasts,board);sound.WaveCaught();}
                else KnockOut(i,causes[i],elapsed+used);
            }
        }
        if(touched)FinishIfDecided(elapsed+used);
        return used;
    }
    void KnockOut(int i,TrapKind cause,float when){
        var p=players[i];p.Alive=false;p.Buffered=Vector2Int.zero;knockedOutAt[i]=when;
        if(i==(duel?lan.Slot:0))lighting.Follow(null);
        string reason=cause==TrapKind.Spike?"가시":cause==TrapKind.Arrow?"화살":cause==TrapKind.Poison?"독 장판":
            cause==TrapKind.Wave?"파도":cause==TrapKind.Lava?"용암":"발판 붕괴";
        deathMessage=duel?$"{i+1}P 탈락 · {reason}":reason+"에 당했습니다";
        if(duel)sound.Eliminated(cause);else sound.Hit(cause);
    }
    void FinishIfDecided(float when){
        int alive=0,winner=-1,last=-1;
        for(int i=0;i<participants;i++){
            if(players[i].Alive){alive++;winner=i;}
            else if(last<0||knockedOutAt[i]>knockedOutAt[last])last=i;
        }
        if(alive>(duel?1:0))return;
        elapsed=when;phase=Phase.Dead;transitionTime=0;
        if(duel){result=alive==1?$"{winner+1}P 승리":"동시 탈락 · 무승부";if(winner>=0)wins[winner]++;}
        deathFocus.Show(causes[last],players[last].Feet.position);
        foreach(var tile in tiles)tile.dust.Pause();
        if(!duel&&elapsed>best){best=elapsed;PlayerPrefs.SetFloat(RecordKey,best);PlayerPrefs.Save();}
    }

    void NextPhase()
    {
        if(phase==Phase.LavaWarning){
            phase=Phase.LavaTravel;remaining=lava.End-elapsed+.4f;return;
        }
        if(phase==Phase.LavaTravel){
            lava.Clear();phase=Phase.Rest;remaining=.65f;return;
        }
        if(phase==Phase.TideWarning){
            tide.Fire(elapsed);phase=Phase.TideTravel;remaining=tide.TravelSeconds;sound.Activate(TrapKind.Wave);return;
        }
        if(phase==Phase.TideTravel){
            tide.Clear();phase=Phase.Rest;remaining=.18f;return;
        }
        if(phase==Phase.LiftWarning){
            phase=Phase.LiftTravel;remaining=liftLength=arrow.FlightTime+.25f;board.LiftHeight(0);return;
        }
        if(phase==Phase.LiftTravel){
            board.RestoreLift();arrow.Clear();patternName="";phase=Phase.Rest;remaining=.25f;return;
        }
        if (phase == Phase.Warning || phase == Phase.FirstStep)
        {
            phase = Phase.Strike;
            remaining = strikeSeconds;
            if (System.Array.Exists(poison,p=>p.Pending))
            {
                sound.Activate(TrapKind.Poison);
                followPotion=true;
            }
            livePoison.Clear();
            foreach(var patch in poison){patch.Activate(elapsed);if(patch.EndsAt>elapsed)livePoison.Add(patch);}
            for(int i=0;i<participants;i++)destinations[i]=players[i].Feet.position;
            AdvanceActors(0,0);
        }
        else if (phase == Phase.Strike)
        {
            foreach (var tile in tiles) tile.ResetTile();
            arrow.Clear();
            phase = Phase.Rest;
            remaining = followPotion ? .045f : Mathf.Lerp(.42f,.12f,Pressure);
        }
        else if(phase==Phase.Collapse){
            board.Collapse();
            bool lost=false;
            for(int i=0;i<participants;i++)if(players[i].Alive&&players[i].Target>=board.Count){
                causes[i]=TrapKind.Fall;KnockOut(i,TrapKind.Fall,elapsed);lost=true;
            }
            nextCollapse=elapsed+12f;
            if(lost)FinishIfDecided(elapsed);
            if(phase!=Phase.Dead){phase=Phase.Rest;remaining=.75f;}
        }else Warn();
    }

    void DrawStun(ChamberRunner actor,Vector3 point,float scale,float height,bool mine){
        var original=GUI.contentColor;GUI.contentColor=new Color(1,.9f,.28f);
        float x=point.x/scale,y=height-point.y/scale;
        for(int i=0;i<3;i++){
            float angle=Time.unscaledTime*5+i*2*Mathf.PI/3;
            GUI.Label(new Rect(x+Mathf.Cos(angle)*23-9,y+Mathf.Sin(angle)*8-18,20,20),"✦",hint);
        }
        GUI.Box(new Rect(x-70,y-47,140,24),mine?$"방향키 연타 {actor.WakeCount}/{ChamberRunner.WakePresses}":"기절");
        GUI.contentColor=original;
    }

    string Scores(){
        string text="";
        for(int i=0;i<participants;i++)text+=(i==0?"":"   ")+$"{i+1}P {wins[i]}승";
        return text;
    }
    void DrawBlock(ChamberRunner runner,float age,float scale,float height){
        var s=chamberCamera.WorldToScreenPoint(runner.BlockPoint);
        var center=new Vector2(s.x/scale,height-s.y/scale);
        var matrix=GUI.matrix;var color=GUI.color;
        float t=age/.46f;
        GUI.color=new Color(1,.82f,.35f,1-t);
        for(int i=0;i<8;i++){
            GUIUtility.RotateAroundPivot(45*i,center);
            GUI.DrawTexture(new Rect(center.x+4+t*19,center.y-1.5f,10*(1-t)+3,3),Texture2D.whiteTexture);
            GUI.matrix=matrix;
        }
        GUI.color=new Color(1,.92f,.66f,Mathf.Clamp01((1-t)*3));
        GUI.Box(new Rect(center.x-46,center.y-40-t*10,92,24),$"{runner.Blocker+1}P 블록!");
        GUI.color=color;
    }
    void OnDestroy() { lan?.Dispose();tide?.Dispose();lava?.Dispose(); if (poisonMaterial) Destroy(poisonMaterial); }

    void OnGUI()
    {
        if (phase == Phase.Arrival || phase == Phase.Returning) return;
        title ??= new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
        score ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 16 };
        hint ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15 };
        float scale = Mathf.Clamp(Screen.height / 640f, 0.65f, 1.6f);
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        float w = Screen.width / scale, h = Screen.height / scale;
        lan.Draw(w,h,phase==Phase.AwaitStep);
        if(lan.Editing||(lan.Busy && !duel))return;
        if(duel)
        {
            for(int i=0;i<participants;i++){
                var actor=players[i];if(!actor.Feet.gameObject.activeSelf)continue;
                var point=chamberCamera.WorldToScreenPoint(actor.View.position+Vector3.up*1.65f);
                GUI.Label(new Rect(point.x/scale-73,h-point.y/scale-16,146,22),actor.Alive?PlayerLabel(i):"탈락",hint);
                if(actor.Alive&&actor.Stunned)DrawStun(actor,point,scale,h,i==(duel?lan.Slot:0));
                float age=elapsed-actor.BlockAt;
                if(age>=0&&age<.46f)DrawBlock(actor,age,scale,h);
            }
            string connection=lan.Guest ? $"{lan.PingMs} ms" : "방장";
            GUI.Box(new Rect(12,62,240,28),$"{PlayerLabel(lan.Slot)} · {ChamberLan.TickRate} Hz · {connection}");
        }
        if(!duel&&first.Stunned){
            var point=chamberCamera.WorldToScreenPoint(first.View.position+Vector3.up*1.65f);
            DrawStun(first,point,scale,h,true);
        }
        if(!string.IsNullOrEmpty(patternName)&&(phase==Phase.Warning||phase==Phase.Strike||phase==Phase.LiftWarning||phase==Phase.LiftTravel||phase==Phase.TideWarning||phase==Phase.TideTravel||phase==Phase.LavaWarning||phase==Phase.LavaTravel))
            GUI.Box(new Rect((w-276)/2,h-57,276,28),patternName);
        if(phase==Phase.LiftWarning||phase==Phase.LiftTravel)
            GUI.Box(new Rect((w-440)/2,h-122,440,30),"민트 안전 · 주황 하층 위험 · 분홍 복귀 위험");
        if(lava.Active)GUI.Box(new Rect((w-276)/2,h-91,276,28),"용암 분출 · 붉은 표적 주의");
        if(phase==Phase.Collapse){
            GUI.Box(new Rect((w-250)/2,h-68,250,34),$"갈라진 발판 붕괴  {remaining:0.0}초  ·  안쪽으로!");
        }
        if (phase == Phase.Dead)
        {
            var color = GUI.color;
            GUI.color = new Color(1,1,1,1-Mathf.InverseLerp(DeathFocus.Hold,DeathFocus.Hold+DeathFocus.Close,transitionTime));
            GUI.Box(new Rect((w-296)/2,h-84,296,66),GUIContent.none);
            GUI.contentColor = deathFocus.Accent;
            GUI.Label(new Rect((w-292)/2,h-80,292,29),duel ? result+" · "+deathMessage : deathMessage,hint);
            GUI.contentColor = Color.white;
            GUI.Label(new Rect((w-292)/2,h-51,292,24),duel ? Scores() : $"생존 {elapsed:0.0}초   ·   최고 {best:0.0}초",hint);
            GUI.color = color;
            return;
        }
        if (phase == Phase.AwaitStep || phase == Phase.FirstStep)
        {
            if (phase == Phase.AwaitStep)
            {
                GUI.Box(new Rect((w-230)/2,h-65,230,38),GUIContent.none);
                GUI.Label(new Rect((w-230)/2,h-65,230,38),"방향키로 첫발을 내디디세요",hint);
            }
            return;
        }
        GUI.Box(new Rect(12, 10, w - 24, 46), GUIContent.none);
        GUI.Label(new Rect(24, 14, w / 2, 38), "침묵의 석실", title);
        GUI.Label(new Rect(w / 2, 14, w / 2 - 24, 38), duel ? $"라운드  {elapsed:0.0}초" : $"생존  {elapsed:0.0}초     최고  {best:0.0}초", score);
        if (poisonCenter>=0)
        {
            var patch=poison[poisonCenter];
            float left=patch.EndsAt-elapsed;
            if (patch.Pending || left>0)
            {
                Vector3 point=chamberCamera.WorldToScreenPoint(patch.center+Vector3.back*.64f);
                var rect=new Rect(point.x/scale-35,h-point.y/scale-10,70,20);
                GUI.Box(rect,patch.Pending ? "독병 낙하" : $"독 {left:0.0}초");
            }
        }
    }
}
