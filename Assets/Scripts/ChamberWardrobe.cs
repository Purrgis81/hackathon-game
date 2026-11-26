using UnityEngine;

internal sealed class ChamberWardrobe
{
    public enum Action { None, Save, Cancel }
    const int Width=900, Height=556;
    static readonly Color Sea=new Color32(17,62,69,255), Ink=new Color32(24,59,62,255),
        Paper=new Color32(255,250,235,255), Mint=new Color32(199,232,218,255),
        Muted=new Color32(74,98,96,255), Gold=new Color32(255,210,103,255);
    static readonly string[] Requirements={"봇 대전 1회 완료","멀티플레이 1회 우승"};
    readonly GameObject stage;
    readonly Transform turntable;
    readonly ChamberAnimalAvatar avatar;
    readonly Camera camera;
    readonly RenderTexture portrait;
    readonly Texture2D hues, palette;
    readonly Material stand;
    ChamberProfile draft;
    float yaw,hue,saturation,value,paletteHue=-1;
    readonly bool[] trialUnlocked=new bool[Requirements.Length];
    GUIStyle text,field;
    Font font;
    public ChamberProfile Profile=>ChamberProfile.Create(draft.Name,draft.Swatch,draft.Animal);

    public ChamberWardrobe(Transform parent)
    {
        stage=new GameObject("Character fitting room");
        stage.transform.SetParent(parent,false);
        stage.transform.position=new Vector3(-2400,0,0);
        turntable=new GameObject("Preview animal").transform;
        turntable.SetParent(stage.transform,false);
        avatar=new ChamberAnimalAvatar(turntable);
        var plinth=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plinth.transform.SetParent(stage.transform,false);
        plinth.transform.localScale=new Vector3(2.25f,.095f,2.25f);
        plinth.transform.localPosition=Vector3.down*.095f;
        Object.Destroy(plinth.GetComponent<Collider>());
        var renderer=plinth.GetComponent<Renderer>();
        stand=renderer.material;stand.color=new Color(.30f,.59f,.54f);
        camera=new GameObject("Portrait camera").AddComponent<Camera>();
        camera.transform.SetParent(stage.transform,false);
        camera.transform.localPosition=new Vector3(0,1.78f,-4.8f);
        camera.transform.LookAt(stage.transform.position+Vector3.up*.82f);
        camera.orthographic=true;camera.orthographicSize=1.43f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Mint;
        camera.nearClipPlane=.1f;camera.farClipPlane=10;camera.allowHDR=false;
        portrait=new RenderTexture(600,420,24){name="Wardrobe portrait",antiAliasing=2};
        camera.targetTexture=portrait;
        hues=new Texture2D(128,1,TextureFormat.RGB24,false);
        palette=new Texture2D(64,40,TextureFormat.RGB24,false);
        hues.wrapMode=palette.wrapMode=TextureWrapMode.Clamp;
        var strip=new Color[128];
        for(int i=0;i<strip.Length;i++)strip[i]=Color.HSVToRGB(i/127f,1,1);
        hues.SetPixels(strip);hues.Apply();
        stage.SetActive(false);
    }

    public void Open(ChamberProfile profile){
        draft=profile;yaw=180;turntable.localRotation=Quaternion.Euler(0,yaw,0);
        System.Array.Clear(trialUnlocked,0,trialUnlocked.Length);
        stage.SetActive(true);SyncPicker();avatar.Show(draft,0);
    }
    public void Close()=>stage.SetActive(false);
    public void Dispose(){
        if(camera)camera.targetTexture=null;
        if(stage){stage.SetActive(false);Object.Destroy(stage);}
        portrait.Release();Object.Destroy(portrait);Object.Destroy(stand);
        Object.Destroy(hues);Object.Destroy(palette);if(font)Object.Destroy(font);
    }
    void SyncPicker()=>Color.RGBToHSV(draft.Tint(0),out hue,out saturation,out value);
    void Rotate(float degrees){
        yaw=Mathf.Repeat(yaw+degrees,360);
        turntable.localRotation=Quaternion.Euler(0,yaw,0);
    }

    public Action Draw(){
        var old=GUI.matrix;
        float scale=Mathf.Min(Screen.width/(Width+24f),Screen.height/(Height+24f));
        GUI.matrix=Matrix4x4.identity;
        Box(new Rect(0,0,Screen.width,Screen.height),Sea,0);
        GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-Width*scale)/2,(Screen.height-Height*scale)/2,0),
            Quaternion.identity,Vector3.one*scale);
        try{return DrawPanel();}finally{GUI.matrix=old;}
    }

    Action DrawPanel(){
        if(text==null){
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Apple SD Gothic Neo","Noto Sans CJK KR","Arial"},24);
            text=new GUIStyle(GUI.skin.label){font=font,padding=new RectOffset(),wordWrap=false};
            field=new GUIStyle(GUI.skin.textField){font=font,fontSize=17,padding=new RectOffset(9,6,4,3)};
        }
        if(Event.current.type==EventType.KeyDown && Event.current.keyCode==KeyCode.Escape){
            Event.current.Use();return Action.Cancel;
        }
        Box(new Rect(0,0,Width,Height),Paper,24);
        Label(new Rect(28,15,340,48),"캐릭터 설정",32,Ink);
        if(Button(new Rect(677,20,86,39),"취소",Mint,Ink))return Action.Cancel;
        if(Button(new Rect(773,20,100,39),"저장",Gold,Ink))return Action.Save;
        var window=new Rect(26,82,359,251);
        GUI.DrawTexture(window,portrait,ScaleMode.StretchToFill,false);
        HandleDrag(window,0);
        Box(new Rect(26,333,359,48),Ink,0);
        if(Button(new Rect(37,340,41,33),"‹",Mint,Ink))Rotate(-30);
        Label(new Rect(78,334,255,44),"드래그로 360° 회전",16,Paper,true);
        if(Button(new Rect(333,340,41,33),"›",Mint,Ink))Rotate(30);
        Label(new Rect(26,399,130,27),"동물",18,Ink);
        for(int i=0;i<ChamberProfile.AnimalLabels.Length;i++){
            var rect=new Rect(26+i*73,435,67,41);
            if(Button(rect,ChamberProfile.AnimalLabels[i],draft.Animal==i?Gold:Mint,Ink)){
                draft.Animal=i;avatar.Show(draft,0);
            }
        }
        if(draft.Animal==0)Label(new Rect(26,485,360,32),"자동은 대기실 자리 순서에 따라 정해져요.",14,Muted);
        Label(new Rect(411,78,90,32),"이름",17,Ink);
        draft.Name=GUI.TextField(new Rect(483,78,390,32),draft.Name??"",12,field);
        DrawColors();
        DrawUnlocks();
        return Action.None;
    }

    void DrawColors(){
        Label(new Rect(411,124,180,31),"몸 색상",19,Ink);
        var color=draft.Tint(0);
        Box(new Rect(733,131,18,18),color,8);
        Label(new Rect(757,124,115,31),"#"+ColorUtility.ToHtmlStringRGB(color),15,Ink);
        if(Button(new Rect(411,163,62,32),"자동",draft.Swatch==0?Gold:Mint,Ink))SetPreset(0);
        for(int i=1;i<=ChamberProfile.PresetCount;i++){
            var rect=new Rect(485+(i-1)*41,163,32,32);
            var preset=new ChamberProfile{Swatch=i};
            Box(rect,draft.Swatch==i?Ink:new Color(.82f,.84f,.78f),16);
            Box(new Rect(rect.x+3,rect.y+3,26,26),preset.Tint(0),13);
            if(GUI.Button(rect,GUIContent.none,GUIStyle.none))SetPreset(i);
        }
        var patch=new Rect(411,214,462,74);
        var strip=new Rect(411,301,462,17);
        HandleDrag(patch,1);
        HandleDrag(strip,2);
        if(paletteHue!=hue){
            var pixels=new Color32[palette.width*palette.height];
            for(int y=0;y<palette.height;y++)
                for(int x=0;x<palette.width;x++)
                    pixels[y*palette.width+x]=Color.HSVToRGB(hue,x/(float)(palette.width-1),y/(float)(palette.height-1));
            palette.SetPixels32(pixels);palette.Apply();paletteHue=hue;
        }
        GUI.DrawTexture(patch,palette);
        GUI.DrawTexture(strip,hues);
        Marker(patch.x+patch.width*saturation,patch.yMax-patch.height*value,color);
        Marker(strip.x+strip.width*hue,strip.center.y,Color.HSVToRGB(hue,1,1));
    }
    void SetPreset(int i){draft.Swatch=i;SyncPicker();avatar.Show(draft,0);}
    void HandleDrag(Rect rect,int control){
        var e=Event.current;
        int id=GUIUtility.GetControlID(45820+control,FocusType.Passive,rect);
        var kind=e.GetTypeForControl(id);
        if(kind==EventType.MouseDown && e.button==0 && rect.Contains(e.mousePosition)){
            GUIUtility.hotControl=id;GUI.FocusControl(null);
        }else if(kind==EventType.MouseUp && e.button==0 && GUIUtility.hotControl==id){
            GUIUtility.hotControl=0;e.Use();return;
        }else if(kind!=EventType.MouseDrag || GUIUtility.hotControl!=id)return;
        if(control==0){if(kind==EventType.MouseDrag)Rotate(-e.delta.x*360/rect.width);}
        else{
            float x=Mathf.InverseLerp(rect.xMin,rect.xMax,e.mousePosition.x);
            if(control==1){saturation=x;value=1-Mathf.InverseLerp(rect.yMin,rect.yMax,e.mousePosition.y);}
            else hue=x;
            draft.Swatch=ChamberProfile.CustomSwatch(Color.HSVToRGB(hue,saturation,value));
            avatar.Show(draft,0);
        }
        e.Use();
    }
    void DrawUnlocks(){
        Box(new Rect(411,344,462,1),Mint,0);
        Label(new Rect(411,355,195,31),"업적 치장",20,Ink);
        Label(new Rect(611,355,263,31),"테스트 슬롯 · 눌러서 해금 미리보기",13,Muted);
        for(int i=0;i<Requirements.Length;i++){
            var rect=new Rect(411,399+i*61,462,54);
            bool unlocked=trialUnlocked[i];
            Box(rect,unlocked?Mint:new Color32(222,226,216,255),12);
            float x=rect.x+16,y=rect.y+15;
            if(unlocked)Label(new Rect(x,y-7,32,38),"◆",20,Ink,true);
            else{
                Box(new Rect(x+6,y,14,16),Muted,7);
                Box(new Rect(x+9,y+3,8,13),new Color32(222,226,216,255),4);
                Box(new Rect(x+3,y+9,20,15),Muted,3);
                Box(new Rect(x+12,y+13,3,6),Paper,2);
            }
            Label(new Rect(x+39,rect.y+4,372,23),$"치장 슬롯 {i+1}  ·  "+(unlocked?"해금 미리보기":"잠김"),15,Ink);
            Label(new Rect(x+39,rect.y+25,372,23),"조건 예시   "+Requirements[i],14,Muted);
            if(GUI.Button(rect,GUIContent.none,GUIStyle.none))trialUnlocked[i]=!unlocked;
        }
    }
    static void Marker(float x,float y,Color color){
        Box(new Rect(x-7,y-7,14,14),Ink,7);
        Box(new Rect(x-5.5f,y-5.5f,11,11),Color.white,6);
        Box(new Rect(x-3.5f,y-3.5f,7,7),color,4);
    }
    static void Box(Rect r,Color color,float radius)=>
        GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,radius);
    void Label(Rect r,string caption,int size,Color color,bool center=false){
        text.fontSize=size;text.fontStyle=size>=15?FontStyle.Bold:FontStyle.Normal;
        text.alignment=center?TextAnchor.MiddleCenter:TextAnchor.MiddleLeft;
        text.normal.textColor=text.hover.textColor=color;GUI.Label(r,caption,text);
    }
    bool Button(Rect r,string caption,Color fill,Color ink){
        Box(r,fill,14);Label(r,caption,17,ink,true);return GUI.Button(r,GUIContent.none,GUIStyle.none);
    }
}
