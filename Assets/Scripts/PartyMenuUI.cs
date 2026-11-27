using UnityEngine;

internal enum MenuTone { Focus, Confirm, Back }

internal sealed class PartyMenuUI
{
    internal static readonly Color Navy = new Color32(23,59,68,255), Teal = new Color32(30,104,106,255),
        Paper = new Color32(255,250,230,255), Gold = new Color32(255,207,95,255),
        Mint = new Color32(204,234,218,255), Muted = new Color32(78,107,108,255);
    readonly AudioSource speaker;
    readonly AudioClip tick, confirm, back;
    readonly Font font;
    GUIStyle label, field;
    float nextTick;

    public PartyMenuUI(GameObject owner)
    {
        speaker=owner.AddComponent<AudioSource>();
        speaker.playOnAwake=false;
        tick=Resources.Load<AudioClip>("Sound/UIFocus");
        confirm=Resources.Load<AudioClip>("Sound/UIConfirm");
        back=Resources.Load<AudioClip>("Sound/UIBack");
        font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Apple SD Gothic Neo","Noto Sans CJK KR","Arial"},36);
    }
    public void Dispose(){if(font)Object.Destroy(font);}
    public void Sound(MenuTone tone)
    {
        if(tone==MenuTone.Focus){
            if(Time.unscaledTime<nextTick)return;
            nextTick=Time.unscaledTime+.065f;
        }
        var clip=tone==MenuTone.Focus?tick:tone==MenuTone.Back?back:confirm;
        if(clip)speaker.PlayOneShot(clip,tone==MenuTone.Focus?.17f:.30f);
    }
    public static void Fill(Rect r, Color color, float radius=0){
        GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,radius);
    }
    public void Text(Rect r,string value,int size,Color color,bool bold=false,TextAnchor align=TextAnchor.MiddleCenter){
        label ??=new GUIStyle(GUI.skin.label){font=font,padding=new RectOffset(),wordWrap=false,clipping=TextClipping.Clip};
        label.fontSize=size;label.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;label.alignment=align;
        label.normal.textColor=label.hover.textColor=color;
        GUI.Label(r,value,label);
    }
    public bool Hit(Rect r,MenuTone tone=MenuTone.Confirm){
        if(!GUI.Button(r,GUIContent.none,GUIStyle.none))return false;
        Sound(tone);return true;
    }
    public bool Button(Rect r,string caption,Color fill,Color ink,int size=17,MenuTone tone=MenuTone.Confirm,bool enabled=true){
        bool over=enabled&&r.Contains(Event.current.mousePosition);
        var shadow=r;shadow.y+=3;
        Fill(shadow,Navy,10);
        Fill(r,enabled?(over?Color.Lerp(fill,Color.white,.23f):fill):new Color32(180,197,189,255),10);
        Text(r,caption,size,enabled?ink:Muted,true);
        bool prior=GUI.enabled;GUI.enabled=prior&&enabled;
        bool pressed=Hit(r,tone);
        GUI.enabled=prior;return pressed;
    }
    public string Input(Rect r,string value,int max){
        Fill(r,Teal,9);
        Fill(new Rect(r.x+2,r.y+2,r.width-4,r.height-4),Color.white,8);
        field ??=new GUIStyle(GUIStyle.none){font=font,fontSize=18,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(10,10,0,0),clipping=TextClipping.Clip};
        field.normal.textColor=field.focused.textColor=Navy;
        return GUI.TextField(r,value,max,field);
    }
    // Simple silhouettes for solo, a party and a practice bot.
    public static void ModeIcon(Rect r,int mode,Color color){
        float k=r.width/32f;
        void Shape(float x,float y,float w,float h,float radius){
            Fill(new Rect(r.x+x*k,r.y+y*k,w*k,h*k),color,radius*k);
        }
        void Person(float x,float y,float s){
            Shape(x+3*s,y,8*s,8*s,4*s);Shape(x,y+9*s,14*s,10*s,4*s);
        }
        if(mode==0)Person(6,2,1.42f);
        else if(mode==1){Person(1,3,1.04f);Person(16,9,.95f);}
        else{
            Shape(15,3,2,7,1);Shape(12,1,8,5,2);Shape(6,9,20,17,5);
            Shape(2,14,4,7,2);Shape(26,14,4,7,2);
            Fill(new Rect(r.x+10*k,r.y+14*k,3*k,4*k),Paper,k);
            Fill(new Rect(r.x+19*k,r.y+14*k,3*k,4*k),Paper,k);
        }
    }
}
