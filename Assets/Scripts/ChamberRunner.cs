using UnityEngine;

// A hop reserves both its takeoff and landing tiles until the feet touch down.
public sealed class ChamberRunner
{
    public readonly Transform Feet;
    public readonly ExplorerHop Hop;
    public readonly ChamberAnimalAvatar Avatar;
    public readonly Transform View;
    Vector3 displayed;
    int visualFrom,visualTarget=-1,bufferedTarget=-1;
    float visualTime,predictionAge;
    public int NextVisualCell => visualTarget>=0 ? visualTarget : Target;

    public void Anticipate(int target,float duration){
        if(!Alive||Sliding||Stunned)return;
        // The host keeps the most recent turn pressed before landing.
        if(visualTarget<0&&Moving){
            visualFrom=Cell;visualTarget=Target;visualTime=progress;
        }
        if(visualTarget>=0&&visualTime<duration){
            bufferedTarget=target;
        }else if(target>=0){
            visualFrom=NextVisualCell;visualTarget=target;visualTime=0;
            bufferedTarget=-1;
        }
        predictionAge=0;
    }

    public void ClearPrediction(){
        visualTarget=bufferedTarget=-1;
        View.localRotation=Quaternion.identity;
    }
    public void Present(RisingBoard board,float duration,float dt,bool freeze){
        if(freeze||!Alive){ClearPrediction();displayed=Feet.position;View.localPosition=Vector3.zero;Hop.Present(dt,true);return;}
        predictionAge+=dt;
        if(visualTarget>=0){
            if(Sliding||Stunned||visualFrom>=board.Count||visualTarget>=board.Count||predictionAge>.9f){
                ClearPrediction();
            }else{
                visualTime+=dt;
                if(visualTime>=duration&&bufferedTarget>=0&&bufferedTarget<board.Count){
                    visualFrom=visualTarget;visualTarget=bufferedTarget;bufferedTarget=-1;visualTime-=duration;
                }
                var from=board.Tiles[visualFrom].transform.position;
                var to=board.Tiles[visualTarget].transform.position;
                var facing=to-from;facing.y=0;
                if(facing.sqrMagnitude>.001f)View.rotation=Quaternion.LookRotation(facing);
                float t=Mathf.Clamp01(visualTime/duration);
                displayed=Vector3.Lerp(from,to,t);
                View.position=displayed;
                Hop.PresentLocalStep(t);
                if(t>=1&&!Moving&&Cell==visualTarget)ClearPrediction();
                return;
            }
        }
        Vector3 goal=Feet.position;
        if(Moving){
            var end=board.Tiles[Target].transform.position;
            goal=Vector3.MoveTowards(goal,end,RisingBoard.Spacing*.16f);
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
        SlideDirection=direction;ClearPrediction();
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
        var pose=Hop.Body;
        Avatar=new ChamberAnimalAvatar(pose, feet.Find("Bear"));
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
        ClearPrediction();displayed=Feet.position;View.localPosition=Vector3.zero;
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
