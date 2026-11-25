using UnityEngine;

// A hop reserves both its takeoff and landing tiles until the feet touch down.
public sealed class ChamberRunner
{
    public readonly Transform Feet;
    public readonly ExplorerHop Hop;
    public readonly Transform View;
    Vector3 displayed;
    int anticipated=-1;
    float predictionAge;
    Vector3 predictionFrom;
    public void Anticipate(int target){
        if(!Moving&&Alive&&target>=0){
            anticipated=target;predictionAge=0;predictionFrom=Feet.position;
        }
    }
    public void ConfirmInput(){anticipated=-1;}
    public void Present(RisingBoard board,float duration,float dt,bool freeze){
        if(freeze||!Alive){anticipated=-1;displayed=Feet.position;View.localPosition=Vector3.zero;Hop.Present(dt,true);return;}
        predictionAge+=dt;
        if(predictionAge>.45f)anticipated=-1;
        Vector3 goal=Feet.position;
        if(Moving){
            var end=board.Tiles[Target].transform.position;
            goal=Vector3.MoveTowards(goal,end,RisingBoard.Spacing*.16f);
        }else if(anticipated>=0){
            goal=Vector3.Lerp(predictionFrom,board.Tiles[anticipated].transform.position,
                Mathf.Clamp01(predictionAge/duration));
        }
        float speed=RisingBoard.Spacing/duration;
        displayed=Vector3.MoveTowards(displayed,goal,dt*speed*(Vector3.Distance(displayed,Feet.position)>RisingBoard.Spacing*.70f?1.35f:1));
        View.position=displayed;
        Hop.Present(dt,false);
    }
    public int Cell { get; private set; }
    public int Target { get; private set; }
    public Vector2Int Buffered;
    public bool Alive=true;
    public Vector3 BlockPoint;
    public float BlockAt=-10;
    public int Blocker;
    public bool Moving => Cell!=Target;
    float progress;
    Vector3 stepFrom;
    public const float SlideSeconds=.072f;
    public const int WakePresses=6;
    public bool Sliding {get;private set;}
    public bool Stunned {get;private set;}
    public int WakeCount {get;private set;}
    public int LastWave=-1;
    public Vector2Int SlideDirection;
    float stunAge,protection;
    public bool Protected=>Sliding||Stunned||protection>0;
    public float Duration(float ordinary)=>Sliding?SlideSeconds:ordinary;
    public void WavePush(Vector2Int direction,int stamp,RisingBoard board){
        if(!Alive||LastWave==stamp)return;
        LastWave=stamp;Sliding=true;Stunned=false;WakeCount=0;stunAge=0;Buffered=Vector2Int.zero;
        SlideDirection=direction;anticipated=-1;
        Feet.rotation=Quaternion.LookRotation(new Vector3(direction.x,0,direction.y));
        Cell=Target=board.Nearest(Feet.position);progress=0;
    }
    public void StartStun(){
        Sliding=false;Stunned=true;stunAge=0;WakeCount=0;Buffered=Vector2Int.zero;Hop.ArrivalPose(.16f,15);
    }
    public bool Mash(){
        if(!Stunned)return false;
        WakeCount++;
        if(WakeCount>=WakePresses)Recover();
        return true;
    }
    void Recover(){Stunned=false;protection=.65f;Buffered=Vector2Int.zero;Hop.Reset();}
    public void StatusTick(float dt){
        protection=Mathf.Max(0,protection-dt);
        if(Stunned){
            stunAge+=dt;
            Hop.ArrivalPose(.14f,Mathf.Sin(stunAge*14)*13);
            if(stunAge>=4f)Recover();
        }
    }

    public ChamberRunner(Transform feet)
    {
        Feet=feet;
        Hop=new ExplorerHop(feet);
        var pose=feet.GetChild(0);
        View=new GameObject("Rendered explorer").transform;
        View.SetParent(feet,false);pose.SetParent(View,false);
    }

    public void Reset(int cell,RisingBoard board)
    {
        Alive=true;BlockAt=-10;Cell=Target=cell;
        Sliding=Stunned=false;LastWave=-1;protection=stunAge=0;WakeCount=0;
        Feet.position=board.Tiles[cell].transform.position;
        stepFrom=Feet.position;
        Buffered=Vector2Int.zero;
        progress=0;
        anticipated=-1;displayed=Feet.position;View.localPosition=Vector3.zero;
        Hop.Reset();
    }

    public bool Occupies(int cell) => cell==Cell || cell==Target;
    public float TimeToLand(float duration) => Moving ? duration-progress : float.PositiveInfinity;

    public bool TryStep(Vector2Int direction,RisingBoard board)
    {
        if(Moving){Buffered=direction;return false;}
        int next=board.Neighbor(Cell,direction);
        if(next<0)return false;
        Target=next;
        stepFrom=Feet.position;
        progress=0;
        Hop.PushOff(direction);
        return true;
    }

    public Vector3 NextPosition(float dt,float duration,RisingBoard board) => Moving
        ? Vector3.Lerp(stepFrom,board.Tiles[Target].transform.position,(progress+dt)/duration)
        : Feet.position;

    public bool Travel(Vector3 to,float dt,float duration,bool freeze)
    {
        Feet.position=to;
        if(!Moving){Hop.Settle(dt);return false;}
        progress+=dt;
        if(Sliding)Hop.ArrivalPose(.10f,-20);
        else Hop.Step(Mathf.Clamp01(progress/duration));
        if(freeze || progress<duration-.000001f)return false;
        Cell=Target;
        return true;
    }
}
