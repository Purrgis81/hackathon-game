using UnityEngine;

// The portrait borrows the real board while the match clock is stopped.
internal sealed class ChamberTitleScreen
{
    public enum Choice { None, Friends, Bots, Profile, Solo }
    static readonly (Choice action, string label)[] MenuItems = {
        (Choice.Solo, "솔로 플레이"), (Choice.Friends, "멀티플레이"),
        (Choice.Bots, "봇과 대전"), (Choice.Profile, "캐릭터 설정")
    };
    const int ModeCount = 3;
    const float Beat = 8.4f, Takeoff = 2.8f, Flight = .56f;
    readonly RisingBoard board;
    readonly Camera camera;
    readonly Transform water, cast;
    readonly Transform[] actors = new Transform[4];
    readonly Vector3 waterHome;
    readonly Quaternion viewHome;
    readonly float homeFieldOfView;
    readonly Vector3[] positions = {
        new Vector3(-2.04f,0,.18f), new Vector3(-.62f,0,.90f),
        new Vector3(0,0,-RisingBoard.Spacing), new Vector3(1.77f,0,.48f)
    };
    readonly float[] facing = {170,175,183,194};
    readonly int looseTile;
    readonly Renderer looseFloor;
    readonly AudioSource shutter;
    readonly AudioClip step;
    float clock;
    bool active, menu;
    int selection, openedFrame;
    GUIStyle lettering;
    Font font;
    readonly Font titleFont;
    readonly string[] words;
    readonly float[] wordWidths;
    GUIStyle logoStyle;
    float logoWidth;
    static readonly Color Ink = new Color32(21,62,69,255);
    static readonly Color Cream = new Color32(255,249,224,255);
    static readonly Color Gold = new Color32(255,213,104,255);

    public ChamberTitleScreen(ChamberGame game, RisingBoard board,string title,Font titleFont)
    {
        this.titleFont=titleFont;
        words=title.Split(new[]{' '},System.StringSplitOptions.RemoveEmptyEntries);
        wordWidths=new float[words.Length];
        this.board=board;
        camera=game.chamberCamera;
        viewHome=camera.transform.rotation;
        homeFieldOfView=camera.fieldOfView;
        water=board.Tiles[0].transform.parent.Find("Coastal Sea");
        waterHome=water.localPosition;
        looseTile=board.At(0,-1);
        looseFloor=board.Tiles[looseTile].transform.Find("floor").GetComponent<Renderer>();
        cast=new GameObject("Island group portrait").transform;
        cast.SetParent(game.transform,false);
        shutter=cast.gameObject.AddComponent<AudioSource>();
        shutter.playOnAwake=false;
        shutter.clip=Resources.Load<AudioClip>("Sound/PlateLatch");
        step=Resources.Load<AudioClip>("Sound/JumpStep");
        for(int i=0;i<actors.Length;i++){
            var actor=new GameObject("Portrait "+(i+1)).transform;
            actor.SetParent(cast,false);
            new ChamberAnimalAvatar(actor).Show(default,i);
            actors[i]=actor;
        }
    }

    public void Open(bool showMenu)
    {
        active=true;
        menu=showMenu;
        openedFrame=Time.frameCount;
        selection=0;
        clock=0;
        cast.gameObject.SetActive(true);
        water.localPosition=new Vector3(waterHome.x,-2.15f,waterHome.z);
        board.Resize(3);
        var aim=board.Tiles[4].transform.position+Vector3.up*.23f;
        camera.transform.position=aim+new Vector3(0,7.5f,-15);
        camera.transform.LookAt(aim);
        Pose();
    }

    public void Close()
    {
        if(!active)return;
        active=false;
        cast.gameObject.SetActive(false);
        water.localPosition=waterHome;
        camera.fieldOfView=homeFieldOfView;
        camera.transform.rotation=viewHome;
        // Both gameplay entry points reset the board and its camera position.
    }

    public void Advance(float dt,bool acceptsInput)
    {
        float previous=clock;
        clock=(clock+dt)%Beat;
        if(previous<1.65f && clock>=1.65f)shutter.PlayOneShot(shutter.clip,.16f);
        if(previous<Takeoff && clock>=Takeoff)shutter.PlayOneShot(step,.27f);
        float oldBottom=looseFloor.bounds.min.y;
        Pose();
        if(oldBottom>water.position.y && looseFloor.bounds.min.y<=water.position.y)
            board.Splash(looseFloor.bounds);
        if(acceptsInput && !menu && Input.anyKeyDown)ShowMenu();
    }

    static float Ease(float t,float a,float b)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));

    void Pose()
    {
        camera.fieldOfView=Mathf.Max(29.5f,Camera.HorizontalToVerticalFieldOfView(34f,camera.aspect));
        float sink=Ease(clock,2.45f,Takeoff)*.20f+Ease(clock,Takeoff,3.85f)*3.05f;
        sink*=1-Ease(clock,5.1f,6.0f);
        board.SetTemporaryPose(looseTile,Vector3.zero,-sink,Vector3.zero);
        float jump=Mathf.Clamp01((clock-Takeoff)/Flight);
        float back=Mathf.Clamp01((clock-6.37f)/Flight);
        float arc=4*(jump*(1-jump)+back*(1-back));
        float look=Ease(clock,2.5f,3.15f)*(1-Ease(clock,5.7f,6.3f));
        for(int i=0;i<actors.Length;i++){
            var actor=actors[i];
            Vector3 pos=positions[i];
            float turn=facing[i];
            float size=1.14f, squash=0;
            if(i==2){
                pos.x+=RisingBoard.Spacing*(jump-back);
                pos.y=1.35f*arc-Mathf.Min(sink,.2f)*(1-jump);
                turn-=53*Mathf.Sin(Mathf.PI*jump);
                turn+=55*Mathf.Sin(Mathf.PI*back);
                squash=.13f*(Ease(clock,2.60f,2.76f)-Ease(clock,2.76f,2.95f))
                    +.13f*(Ease(clock,3.28f,3.38f)-Ease(clock,3.38f,3.55f));
            }else turn+=(i==3?30:-23)*look;
            actor.localPosition=pos;
            actor.localRotation=Quaternion.Euler(0,turn,0);
            actor.localScale=size*new Vector3(1+squash,1-squash,1+squash);
        }
    }

    void ShowMenu(){menu=true;openedFrame=Time.frameCount;selection=0;}

    public Choice Draw()
    {
        var e=Event.current;
        if(!menu && (e.type==EventType.KeyDown || e.type==EventType.MouseDown)){
            ShowMenu();e.Use();
        }
        float scale=Mathf.Min(Screen.height/720f,Screen.width/670f);
        float w=Screen.width/scale,h=Screen.height/scale;
        GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
        if(lettering==null){
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Apple SD Gothic Neo","Noto Sans CJK KR","Arial"},72);
            lettering=new GUIStyle(GUI.skin.label){font=font,fontStyle=FontStyle.Bold,
                alignment=TextAnchor.MiddleCenter,wordWrap=false,padding=new RectOffset()};
        }
        Logo(w,h);
        float frameWidth=Mathf.Min(w-60,720);
        var frame=new Rect((w-frameWidth)*.5f,h*.285f,frameWidth,h*.425f);
        var line=new Color(1,.98f,.86f,.67f);
        for(int i=0;i<4;i++){
            bool right=(i&1)!=0,lower=(i&2)!=0;
            float x=right?frame.xMax:frame.x, y=lower?frame.yMax:frame.y;
            Plate(new Rect(right?x-23:x,lower?y-2:y,23,2),line,1);
            Plate(new Rect(right?x-2:x,lower?y-19:y,2,19),line,1);
        }
        float flash=1-Mathf.InverseLerp(1.65f,1.86f,clock);
        if(clock>=1.65f && flash>0)Plate(new Rect(0,0,w,h),new Color(1,.98f,.88f,.32f*flash),0);
        if(!menu){
            var pill=new Rect((w-230)*.5f,h*.835f,230,47);
            Plate(pill,Ink,24);
            Label(pill,"아무 키나 눌러 시작",19,Cream);
            return Choice.None;
        }
        if(Time.frameCount!=openedFrame && e.type==EventType.KeyDown){
            switch(e.keyCode){
                case KeyCode.LeftArrow: case KeyCode.RightArrow:
                    if(selection<ModeCount){
                        int step=e.keyCode==KeyCode.LeftArrow?-1:1;
                        selection=(selection+ModeCount+step)%ModeCount;
                    }
                    e.Use();break;
                case KeyCode.DownArrow: selection=ModeCount; e.Use();break;
                case KeyCode.UpArrow:
                    if(selection==ModeCount)selection=1;
                    e.Use();break;
                case KeyCode.KeypadEnter: case KeyCode.Return: case KeyCode.Space:
                    e.Use();return MenuItems[selection].action;
                case KeyCode.Escape: menu=false; e.Use();return Choice.None;
            }
        }
        const float buttonWidth=186, gap=18;
        for(int i=0;i<MenuItems.Length;i++){
            bool primary=i<ModeCount;
            float center=w*.5f+(primary?(i-1)*(buttonWidth+gap):0);
            var rect=new Rect(center-buttonWidth*.5f,h*.79f+(primary?0:62),buttonWidth,primary?51:34);
            if(e.type==EventType.MouseMove && rect.Contains(e.mousePosition))selection=i;
            bool chosen=selection==i;
            if(primary){
                var shadow=rect;shadow.y+=4;
                Plate(shadow,Ink,22);
                Plate(rect,chosen?Gold:Cream,22);
            }else Plate(rect,chosen?Cream:Ink,16);
            Label(rect,MenuItems[i].label,primary?23:15,
                primary||chosen?Ink:Cream);
            if(GUI.Button(rect,GUIContent.none,GUIStyle.none) && Time.frameCount!=openedFrame)return MenuItems[i].action;
        }
        return Choice.None;
    }

    void Logo(float w,float h)
    {
        const float gap=17;
        if(logoStyle==null){
            logoStyle=new GUIStyle(lettering){font=titleFont?titleFont:font,fontSize=108,
                fontStyle=titleFont?FontStyle.Normal:FontStyle.Bold};
            for(int i=0;i<words.Length;i++){
                wordWidths[i]=logoStyle.CalcSize(new GUIContent(words[i])).x;
                logoWidth+=wordWidths[i]+(i>0?gap:0);
            }
        }
        float cx=w*.5f,cy=h*.159f;
        var original=GUI.matrix;
        float fit=Mathf.Min(1,(w-64)/Mathf.Max(1,logoWidth));
        float left=-logoWidth*.5f;
        for(int i=0;i<words.Length;i++){
            var center=new Vector2(cx+(left+wordWidths[i]*.5f)*fit,cy+(i%2==0?0:4));
            left+=wordWidths[i]+gap;
            GUIUtility.ScaleAroundPivot(Vector2.one*fit,center);
            GUIUtility.RotateAroundPivot(i%2==0?-4:4,center);
            var face=new Rect(center.x-wordWidths[i]*.5f-8,center.y-75,wordWidths[i]+16,150);
            logoStyle.normal.textColor=logoStyle.hover.textColor=Ink;
            for(int side=0;side<8;side++){
                float angle=side*Mathf.PI*.25f;
                var edge=face;edge.x+=Mathf.Cos(angle)*4;edge.y+=4+Mathf.Sin(angle)*5;
                GUI.Label(edge,words[i],logoStyle);
            }
            logoStyle.normal.textColor=logoStyle.hover.textColor=i%2==0?Cream:Gold;
            GUI.Label(face,words[i],logoStyle);
            GUI.matrix=original;
        }
    }
    void Label(Rect r,string text,int size,Color color){
        lettering.fontSize=size;
        lettering.normal.textColor=lettering.hover.textColor=color;
        GUI.Label(r,text,lettering);
    }
    static void Plate(Rect r,Color color,float radius){
        GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,radius);
    }
    public void Dispose(){if(font)Object.Destroy(font);}
}
