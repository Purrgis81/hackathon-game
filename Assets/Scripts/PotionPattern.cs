using UnityEngine;

public enum SpillShape { Cross, Line, Star }

public static class PotionPattern
{
    static bool Covers(SpillShape shape,Vector2Int d,int radius)
    {
        int x=Mathf.Abs(d.x), z=Mathf.Abs(d.y);
        if (Mathf.Max(x,z)>(radius==1 ? 1 : 2)) return false;
        if (shape==SpillShape.Line) return z==0;
        if (shape==SpillShape.Cross) return x==0 || z==0;
        // In the starter room the diagonal arms leave the four cardinal exits open.
        return x==z || (radius>1 && (x==0 || z==0));
    }

    public static int Mark(RisingBoard board,PoisonFloor[] patches,int origin,int escape,SpillShape shape,bool[] protectedTiles)
    {
        int chosen=-1, best=int.MinValue;
        for (int i=0;i<board.Count;i++)
        {
            var center=board.Cell(i);
            if (escape>=0 && Covers(shape,board.Cell(escape)-center,board.Radius)) continue;
            bool hitsExit=false;
            for(int j=0;j<board.Count;j++)if(protectedTiles[j] && Covers(shape,board.Cell(j)-center,board.Radius)){hitsExit=true;break;}
            if(hitsExit)continue;
            if(escape<0)
            {
                // Solo throws allow a two-hop dodge during the full wind-up.
                bool reachable=false;
                for(int j=0;j<board.Count;j++)
                {
                    var walk=board.Cell(origin)-board.Cell(j);
                    if(Mathf.Abs(walk.x)+Mathf.Abs(walk.y)<=2 && !Covers(shape,board.Cell(j)-center,board.Radius))
                    {reachable=true;break;}
                }
                if(!reachable)continue;
            }
            int count=0;
            for (int j=0;j<board.Count;j++)
                if (Covers(shape,board.Cell(j)-center,board.Radius)) count++;
            // Prefer the most legible footprint, then a throw close to the explorer.
            int distance=(board.Cell(origin)-center).sqrMagnitude;
            int score=count*100-distance*4+Random.Range(0,4);
            if (score>best) {best=score;chosen=i;}
        }
        if (chosen<0) return -1;
        for (int i=0;i<board.Count;i++)
        {
            var delta=board.Cell(i)-board.Cell(chosen);
            if (Covers(shape,delta,board.Radius))
                patches[i].Mark(.14f*Mathf.Max(Mathf.Abs(delta.x),Mathf.Abs(delta.y)));
        }
        return chosen;
    }
}
