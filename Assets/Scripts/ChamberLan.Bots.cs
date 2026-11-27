using System.Collections.Generic;
using UnityEngine;

public sealed partial class ChamberLan
{
    sealed class BotSeat {
        public ChamberBotSettings Settings;
        public string Name="",DraftName="";
        public BotSeat(ChamberBotSettings settings){Settings=settings;}
    }
    readonly List<BotSeat> botSeats=new List<BotSeat>();
    bool botNameImeActive;
    IMECompositionMode previousBotNameIme;
    int lastBotNameCompositionFrame=-10;
    int botMask,botCodes,humanSeats=1;
    bool practice;
    public bool Practice=>practice;
    public bool ControlsBots=>host&&Playing;
    public bool IsBot(int slot)=>slot>=0&&slot<count&&(botMask&(1<<slot))!=0;
    public ChamberBotSettings BotSettings(int slot)=>ChamberBotSettings.Decode((botCodes>>(slot*4))&15);

    public void OpenBotRoom(){
        OpenMenu();if(mode!=Mode.Menu)return;
        host=true;practice=true;online=false;Slot=0;roster[0]=Local;message="";
        mode=Mode.Room;
        botSeats.Add(new BotSeat(new ChamberBotSettings {Personality=ChamberBotPersonality.Survivor,Difficulty=ChamberBotDifficulty.Normal}));
        UpdateLobby();
    }
    public bool AddBot(ChamberBotPersonality personality=ChamberBotPersonality.Survivor,ChamberBotDifficulty difficulty=ChamberBotDifficulty.Normal){
        if(!host||!InRoom||count>=Capacity)return false;
        botSeats.Add(new BotSeat(ChamberBotSettings.Decode((int)personality|((int)difficulty<<2))));UpdateLobby();return true;
    }
    public bool SetBot(int slot,ChamberBotSettings settings){
        if(!host||!InRoom||!IsBot(slot))return false;
        botSeats[slot-humanSeats].Settings=ChamberBotSettings.Decode(settings.Code);UpdateLobby();return true;
    }
    public bool SetBotName(int slot,string name){
        if(!host||!InRoom||!IsBot(slot))return false;
        if(ApplyBotName(botSeats[slot-humanSeats],name))UpdateLobby();
        return true;
    }
    static bool ApplyBotName(BotSeat bot,string name){
        name=ChamberProfile.Create(name,0).Name;
        bot.DraftName=name;
        if(bot.Name==name)return false;
        bot.Name=name;return true;
    }
    void CommitBotNames(){
        bool changed=false;
        foreach(var bot in botSeats)changed=ApplyBotName(bot,bot.DraftName)||changed;
        if(changed)UpdateLobby();
    }
    public bool RemoveBot(int slot){
        if(!host||!InRoom||!IsBot(slot))return false;
        botSeats.RemoveAt(slot-humanSeats);UpdateLobby();return true;
    }
    void AppendBotSeats(){
        humanSeats=count;botMask=botCodes=0;
        while(botSeats.Count>Capacity-humanSeats)botSeats.RemoveAt(botSeats.Count-1);
        for(int i=0;i<botSeats.Count;i++){
            var bot=botSeats[i];var options=bot.Settings;int slot=count++;
            botMask|=1<<slot;botCodes|=options.Code<<(slot*4);readySeats|=1<<slot;
            string name=string.IsNullOrEmpty(bot.Name)?"BOT "+ChamberBotSettings.Personalities[(int)options.Personality]:bot.Name;
            roster[slot]=ChamberProfile.Create(name,0,(int)options.Personality+1);
        }
    }
    void ClearBots(){StopBotNameIme();botSeats.Clear();botMask=botCodes=0;humanSeats=1;practice=false;}
    void QueueHostInput(Vector2Int direction){
        byte key=Encode(direction);
        if(key!=0&&!game.RoundFinished&&!game.CountingDown&&inputs[0].Count<8)inputs[0].Enqueue(key);
    }
    void DrawBotName(int seat,float x,float y,float span){

        var bot=botSeats[seat-humanSeats];
        float width=span-16,left=x-width/2;
        string control="BotName"+(seat-humanSeats);
        var e=Event.current;
        var field=new Rect(left,y,width-48,26);
        bool focused=GUI.GetNameOfFocusedControl()==control;
        if(focused||(e.type==EventType.MouseDown&&e.button==0&&field.Contains(e.mousePosition)))StartBotNameIme();
        if(focused&&!string.IsNullOrEmpty(Input.compositionString))lastBotNameCompositionFrame=Time.frameCount;
        bool submit=e.type==EventType.KeyDown&&(e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter)
            &&focused&&string.IsNullOrEmpty(Input.compositionString)&&Time.frameCount>lastBotNameCompositionFrame+1;
        GUI.SetNextControlName(control);
        // Keep the draft intact while typing, so Korean IME composition and spaces are not stripped mid-edit.
        bot.DraftName=UI.Input(field,bot.DraftName,12);
        if(GUI.GetNameOfFocusedControl()==control){
            StartBotNameIme();
            Input.compositionCursorPos=GUIUtility.GUIToScreenPoint(new Vector2(field.x+8,field.yMax));
        }
        if(string.IsNullOrEmpty(bot.DraftName)&&GUI.GetNameOfFocusedControl()!=control)
            UI.Text(new Rect(field.x+8,field.y,field.width-12,field.height),"봇 이름 · 12자",12,PartyMenuUI.Muted,false,TextAnchor.MiddleLeft);
        if(UI.Button(new Rect(left+width-44,y,44,26),"적용",PartyMenuUI.Mint,PartyMenuUI.Navy,13)||submit){
            CommitFocusedBotName();SetBotName(seat,bot.DraftName);
            if(submit)e.Use();
        }
    }
    void StartBotNameIme(){
        if(botNameImeActive)return;
        previousBotNameIme=Input.imeCompositionMode;botNameImeActive=true;
        Input.imeCompositionMode=IMECompositionMode.On;
    }
    void StopBotNameIme(){
        if(!botNameImeActive)return;
        Input.imeCompositionMode=previousBotNameIme;botNameImeActive=false;
    }
    void UpdateBotNameIme(){
        string focus=GUI.GetNameOfFocusedControl();
        if(!host||!InRoom||string.IsNullOrEmpty(focus)||!focus.StartsWith("BotName",System.StringComparison.Ordinal))StopBotNameIme();
    }
    void CommitFocusedBotName(){
        string focus=GUI.GetNameOfFocusedControl(),composition=Input.compositionString;
        if(!string.IsNullOrEmpty(composition))for(int i=0;i<botSeats.Count;i++){
            if(focus!="BotName"+i)continue;
            // IMGUI returns committed text only. Include the active syllable at its caret/selection before blurring.
            var editor=(TextEditor)GUIUtility.GetStateObject(typeof(TextEditor),GUIUtility.keyboardControl);
            editor.ReplaceSelection(composition);
            botSeats[i].DraftName=editor.text;
            Input.imeCompositionMode=IMECompositionMode.Off;
            break;
        }
        GUI.FocusControl(null);StopBotNameIme();
    }
    void DrawBotSeat(int seat,float x,float y,float span){
        float width=span-16,left=x-width/2;
        var ink=PartyMenuUI.Navy;var mint=PartyMenuUI.Mint;
        if(IsBot(seat)){
            var settings=BotSettings(seat);
            if(host){
                if(UI.Button(new Rect(left,y,width,24),"성향 · "+ChamberBotSettings.Personalities[(int)settings.Personality],mint,ink,13)){
                    settings.Personality=(ChamberBotPersonality)(((int)settings.Personality+1)%4);SetBot(seat,settings);
                }
                if(UI.Button(new Rect(left,y+29,width,24),"난이도 · "+ChamberBotSettings.Difficulties[(int)settings.Difficulty],mint,ink,13)){
                    settings.Difficulty=(ChamberBotDifficulty)(((int)settings.Difficulty+1)%3);SetBot(seat,settings);
                }
                if(UI.Button(new Rect(left,y+58,width,22),"봇 빼기",ink,PartyMenuUI.Paper,13,MenuTone.Back)){RemoveBot(seat);GUI.FocusControl(null);}
            }else UI.Text(new Rect(left,y,width,32),settings.Label,13,ink);
        }else if(host && seat==count && count<Capacity){
            if(UI.Button(new Rect(left,y+3,width,32),"+  봇 추가",PartyMenuUI.Teal,PartyMenuUI.Paper,15))
                AddBot((ChamberBotPersonality)(botSeats.Count%4));
        }
    }
}
