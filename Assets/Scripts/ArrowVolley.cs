using UnityEngine;

public sealed class ArrowVolley
{
    readonly GameObject owner;
    readonly ArrowLane[] lanes=new ArrowLane[32];
    readonly float[] delay=new float[32],height=new float[32];
    readonly bool[] fired=new bool[32];
    int count;
    public const float LiftRiseAt=4.3f,LiftRiseSeconds=.65f;
    public bool Armed=>count>0;
    public Vector3 LaunchPosition=>lanes[0].LaunchPosition;
    public float Speed=>lanes[0].Speed;
    public float FlightTime {get;private set;}
    public int Signature {get;private set;}
    public ArrowVolley(GameObject owner){this.owner=owner;}
    public void Clear(){
        foreach(var lane in lanes)if(lane)lane.Clear();
        count=0;FlightTime=0;Signature=0;
    }
    public void Plan(RisingBoard board,int origin,bool horizontal,int sign,int pattern,bool[] exits,float pressure){
        Clear();
        if(pattern>=5){
            int sides=pattern==5?4:8;
            float beat=pattern==5?.45f:.34f;
            var center=new Vector3(board.Center,0,board.Center);
            for(int i=0;i<sides;i++){
                float angle=i*2*Mathf.PI/sides;
                var direction=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
                AddShot(center,direction,(board.Width-1)*.5f*(sides==8?1.42f:1),i*beat,pressure);
            }
            return;
        }
        var cell=board.Cell(origin);int row=horizontal?cell.y:cell.x;
        Add(board,horizontal,row,sign,0,pressure);
        if(pattern==0)return;
        if(pattern==1||pattern==4)Add(board,horizontal,row,-sign,.48f,pressure);
        bool axis=pattern==3?!horizontal:horizontal;
        int baseRow=axis?cell.y:cell.x;
        int min=-(board.Width-1)/2,max=board.Width/2;
        for(int offset=0;offset<board.Width&&count<(pattern==4?3:2);offset++){
            for(int side=1;side>=-1;side-=2){
                int candidate=baseRow+side*offset;
                if(candidate<min||candidate>max||(axis==horizontal&&candidate==row))continue;
                bool safe=true;
                for(int i=0;i<board.Count;i++)if(exits[i]&&(axis?board.Cell(i).y:board.Cell(i).x)==candidate){safe=false;break;}
                if(!safe)continue;
                Add(board,axis,candidate,-sign,pattern==2?.12f:.64f,pressure);return;
            }
        }
    }
    public void PlanLift(RisingBoard board,float pressure){
        Clear();bool horizontal=board.LiftHorizontal;int lo=-(board.Width-1)/2,hi=board.Width/2;
        for(int row=lo;row<=hi;row++){
            int index=row-lo;
            Add(board,horizontal,row,1,.65f+index*.055f,pressure);
            if(index%3==1)Add(board,horizontal,row,-1,2.4f+index*.04f,pressure,-2.8f);
            if(index%3==2)Add(board,horizontal,row,-1,LiftRiseAt+.60f+index*.025f,pressure);
        }
        FlightTime=Mathf.Max(FlightTime,LiftRiseAt+LiftRiseSeconds+.35f);
    }
    void Add(RisingBoard board,bool horizontal,int row,int sign,float wait,float pressure,float floor=0){
        var center=horizontal?new Vector3(board.Center,floor,row*RisingBoard.Spacing):new Vector3(row*RisingBoard.Spacing,floor,board.Center);
        AddShot(center,(horizontal?Vector3.right:Vector3.forward)*sign,(board.Width-1)*.5f,wait,pressure);
    }
    void AddShot(Vector3 center,Vector3 direction,float radius,float wait,float pressure){
        var lane=lanes[count];
        if(!lane){lane=owner.AddComponent<ArrowLane>();lanes[count]=lane;}
        delay[count]=wait;height[count]=center.y+.55f;fired[count]=false;
        lane.Aim(center,direction,radius,pressure);count++;
        if(wait>.85f)lane.Preview(false);
        FlightTime=Mathf.Max(FlightTime,wait+lane.FlightTime);
        unchecked{Signature=Signature*107+Mathf.RoundToInt(direction.x*10)+Mathf.RoundToInt(direction.z*100)+Mathf.RoundToInt(wait*1000)+Mathf.RoundToInt(center.y*100);}
    }
    public void Draw(float t,float age){
        for(int i=0;i<count;i++){
            bool visible=delay[i]<=.85f;
            lanes[i].Preview(visible);
            if(visible)lanes[i].Draw(t,age);
        }
    }
    public void Sample(float age,ChamberAudio sound){
        for(int i=0;i<count;i++){
            float local=age-delay[i];
            if(local<0){
                bool visible=local>=-.85f;lanes[i].Preview(visible);
                if(visible)lanes[i].Draw(1+local/.85f,age);
                continue;
            }
            if(!fired[i]){lanes[i].Preview(true);lanes[i].Fire();sound.Activate(TrapKind.Arrow);fired[i]=true;}
            lanes[i].Sample(local/lanes[i].FlightTime);
        }
    }
    public float Contact(Vector3 from,Vector3 to,float age,float slice){
        float hit=float.PositiveInfinity;
        for(int i=0;i<count;i++){
            float local=age-delay[i],flight=lanes[i].FlightTime;
            if(local+slice<0||local>=flight)continue;
            float a=slice>0?Mathf.Clamp01(-local/slice):0,b=slice>0?Mathf.Clamp01((flight-local)/slice):1;
            float y=from.y+.55f-height[i],dy=to.y-from.y;
            if(Mathf.Abs(dy)<.000001f){if(Mathf.Abs(y)>.58f)continue;}
            else{
                float first=(-.58f-y)/dy,last=(.58f-y)/dy;
                a=Mathf.Max(a,Mathf.Min(first,last));b=Mathf.Min(b,Mathf.Max(first,last));
                if(a>b)continue;
            }
            float h=lanes[i].Contact(Vector3.Lerp(from,to,a),Vector3.Lerp(from,to,b),(local+a*slice)/flight,(local+b*slice)/flight);
            if(h<=1)hit=Mathf.Min(hit,a+h*(b-a));
        }
        return hit;
    }
}
