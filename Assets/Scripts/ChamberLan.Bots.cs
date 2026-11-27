using System.Collections.Generic;
using UnityEngine;

public sealed partial class ChamberLan
{
    readonly List<ChamberBotSettings> botSeats=new List<ChamberBotSettings>();
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
        botSeats.Add(new ChamberBotSettings {Personality=ChamberBotPersonality.Survivor,Difficulty=ChamberBotDifficulty.Normal});
        UpdateLobby();
    }
    public bool AddBot(ChamberBotPersonality personality=ChamberBotPersonality.Survivor,ChamberBotDifficulty difficulty=ChamberBotDifficulty.Normal){
        if(!host||!InRoom||count>=Capacity)return false;
        botSeats.Add(ChamberBotSettings.Decode((int)personality|((int)difficulty<<2)));UpdateLobby();return true;
    }
    public bool SetBot(int slot,ChamberBotSettings settings){
        if(!host||!InRoom||!IsBot(slot))return false;
        botSeats[slot-humanSeats]=ChamberBotSettings.Decode(settings.Code);UpdateLobby();return true;
    }
    public bool RemoveBot(int slot){
        if(!host||!InRoom||!IsBot(slot))return false;
        botSeats.RemoveAt(slot-humanSeats);UpdateLobby();return true;
    }
    void AppendBotSeats(){
        humanSeats=count;botMask=botCodes=0;
        while(botSeats.Count>Capacity-humanSeats)botSeats.RemoveAt(botSeats.Count-1);
        for(int i=0;i<botSeats.Count;i++){
            var options=botSeats[i];int slot=count++;
            botMask|=1<<slot;botCodes|=options.Code<<(slot*4);readySeats|=1<<slot;
            roster[slot]=ChamberProfile.Create("BOT "+ChamberBotSettings.Personalities[(int)options.Personality],0,(int)options.Personality+1);
        }
    }
    void ClearBots(){botSeats.Clear();botMask=botCodes=0;humanSeats=1;practice=false;}
    void QueueHostInput(Vector2Int direction){
        byte key=Encode(direction);
        if(key!=0&&!game.RoundFinished&&!game.CountingDown&&inputs[0].Count<8)inputs[0].Enqueue(key);
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
                if(UI.Button(new Rect(left,y+58,width,22),"봇 빼기",ink,PartyMenuUI.Paper,13,MenuTone.Back))RemoveBot(seat);
            }else UI.Text(new Rect(left,y,width,32),settings.Label,13,ink);
        }else if(host && seat==count && count<Capacity){
            if(UI.Button(new Rect(left,y+3,width,32),"+  봇 추가",PartyMenuUI.Teal,PartyMenuUI.Paper,15))
                AddBot((ChamberBotPersonality)(botSeats.Count%4));
        }
    }
}
