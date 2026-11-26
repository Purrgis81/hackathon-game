#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

public enum ChamberDebugPattern
{
    SpikeSpread, SpikeInward, SpikeOutward, SpikeRush, GlobalSpikes, SingleShelter,
    ArrowSingle, ArrowReturn, ArrowParallel, ArrowCross, ArrowBurst, ArrowFourWay, ArrowEightWay,
    PoisonCross, PoisonLine, PoisonStar, Wave, Lava,
    LiftColumns, LiftRows, LiftBlocks, LiftDiagonal, Collapse
}

// These controls only exist in the Editor and development players. Network
// matches use their ordinary deterministic simulation and cannot invoke them.
public sealed partial class ChamberGame
{
    static readonly string[] DebugPatternLabels={
        "가시 · 순차 확산", "가시 · 안쪽 나선", "가시 · 바깥 나선", "가시 · 쉬고 쇄도",
        "가시 · 전역", "가시 · 안전 1칸", "화살 · 단발", "화살 · 왕복",
        "화살 · 평행", "화살 · 교차", "화살 · 시차 연사", "화살 · 사방", "화살 · 팔방",
        "독 · 십자", "독 · 직선", "독 · 별", "파도", "용암",
        "승강 · 세로 띠", "승강 · 가로 띠", "승강 · 블록", "승강 · 대각선", "발판 붕괴"
    };

    bool debugOpen,debugSession;
    bool debugAutomatic=true;
    int debugBoardWidth=10;
    float debugPressure=-1;
    float debugWarningSeconds=-1;
    float debugTempo=-1;
    Vector2 debugScroll;
    string debugMessage="맵 또는 패턴을 클릭하면 단독 테스트를 시작합니다.";
    GUIStyle debugHint;

    public bool DebugAvailable=>Application.isPlaying && board!=null && lan!=null && !lan.Busy && !lan.Editing && phase!=Phase.Arrival;
    public int DebugMapWidth=>board==null?0:board.Width;
    public bool DebugAutomaticPatterns=>debugAutomatic;
    public float DebugWarningSeconds=>debugWarningSeconds;
    public float DebugTempo=>debugTempo;
    bool DebugHoldPatterns=>debugSession && !debugAutomatic && !lan.Busy;

    void UpdateDebugControls(){
        if(lan==null)return;
        if(lan.Busy || lan.Editing){debugOpen=false;return;}
        if(Input.GetKeyDown(KeyCode.F1))debugOpen=!debugOpen;
    }

    // Keep the player near their current position when changing the board.
    // Cancel all hazards and forced movement before reusing their tile indices.
    // Preserve the automatic-pattern toggle; only the user changes that choice.
    void PrepareDebugArena(int width){
        debugSession=true;
        debugBoardWidth=Mathf.Clamp(width,3,10);
        Vector3 position=first.Feet.position;
        arrow.Clear();potion.Clear();tide.Clear();lava.Clear();
        foreach(var patch in poison)patch.Clear();
        foreach(var tile in tiles)tile.ResetTile();
        livePoison.Clear();spikeOrder.Clear();onsets.Clear();spiral.Clear();
        System.Array.Clear(dangerous,0,dangerous.Length);
        System.Array.Clear(exits,0,exits.Length);
        budget=0;poisonCenter=-1;followPotion=globalSpikes=singleShelter=spiralWave=false;
        patternName="";result="";deathMessage="";
        if(board.Width!=debugBoardWidth)finalArenaSeconds=0;
        board.Resize(debugBoardWidth);
        first.Reset(board.Nearest(position),board);
        first.Feet.gameObject.SetActive(true);
        lighting.Follow(first.View);
        deathFocus.Reveal(1);transitionTime=0;
        sound.ResumeMusic();
        nextCollapse=elapsed+12;nextLift=elapsed+26;nextBurstAt=elapsed+28;
        phase=Phase.Rest;remaining=.1f;
    }

    public bool DebugSetMapSize(int width){
        if(!DebugAvailable)return false;
        PrepareDebugArena(width);
        debugMessage=$"맵 {board.Width}×{board.Width} · 기존 공격을 정리했습니다.";
        return true;
    }

    public bool DebugForcePattern(ChamberDebugPattern selected){
        if(!DebugAvailable || (int)selected<0 || (int)selected>=DebugPatternLabels.Length)return false;
        if(selected==ChamberDebugPattern.Collapse && board.Width<=3){
            debugMessage="3×3은 최소 크기입니다. 더 큰 맵에서 붕괴를 실행하세요.";return false;
        }
        PrepareDebugArena(board.Width);
        PatternKind kind;
        int variant=0,index=(int)selected;
        if(selected<=ChamberDebugPattern.SingleShelter){kind=PatternKind.Spike;variant=index;}
        else if(selected<=ChamberDebugPattern.ArrowEightWay){kind=PatternKind.Arrow;variant=index-(int)ChamberDebugPattern.ArrowSingle;}
        else if(selected<=ChamberDebugPattern.PoisonStar){kind=PatternKind.Poison;variant=index-(int)ChamberDebugPattern.PoisonCross;}
        else if(selected==ChamberDebugPattern.Wave)kind=PatternKind.Wave;
        else if(selected==ChamberDebugPattern.Lava)kind=PatternKind.Lava;
        else if(selected<=ChamberDebugPattern.LiftDiagonal){kind=PatternKind.Lift;variant=index-(int)ChamberDebugPattern.LiftColumns;}
        else kind=PatternKind.Collapse;
        Warn(kind,variant);
        debugMessage=DebugPatternLabels[index]+(debugAutomatic?" · 예고부터 실행 중 (이후 자동 진행)":" · 예고부터 실행 중 (이후 대기)");
        return true;
    }

    public bool DebugSetAutomaticPatterns(bool automatic){
        if(!DebugAvailable)return false;
        debugSession=true;debugBoardWidth=board.Width;debugAutomatic=automatic;
        if((phase==Phase.SoloReady || phase==Phase.AwaitStep) && !automatic){phase=Phase.Rest;remaining=.1f;}
        debugMessage=automatic?"현재 공격 종료 후 일반 패턴과 붕괴를 자동 진행합니다.":"현재 공격 종료 후 다음 패턴을 기다립니다. 이동은 가능합니다.";
        return true;
    }

    public bool DebugSetPressure(float pressure){
        if(!DebugAvailable || float.IsNaN(pressure) || float.IsInfinity(pressure))return false;
        debugSession=true;debugBoardWidth=board.Width;debugPressure=Mathf.Clamp01(pressure);
        debugMessage=$"다음 패턴의 난이도 {debugPressure*100:0}% · 진행 중인 공격에는 소급 적용하지 않습니다.";
        return true;
    }

    public bool DebugClearAttacks(){
        if(!DebugAvailable)return false;
        PrepareDebugArena(board.Width);
        debugMessage=debugAutomatic?"공격과 피격 상태를 정리했습니다. 자동 패턴을 계속 진행합니다.":"공격과 피격 상태를 정리했습니다. 다음 패턴을 선택하세요.";
        return true;
    }

    public bool DebugSetWarningSeconds(float seconds){
        if(!DebugAvailable || float.IsNaN(seconds) || float.IsInfinity(seconds))return false;
        debugSession=true;debugBoardWidth=board.Width;
        debugWarningSeconds=Mathf.Round(Mathf.Clamp(seconds,.1f,3f)*10)/10;
        debugMessage=$"다음 패턴 발동 대기 {debugWarningSeconds:0.0}초 · 진행 중인 예고는 유지합니다.";
        return true;
    }

    public bool DebugSetTempo(float tempo){
        if(!DebugAvailable || float.IsNaN(tempo) || float.IsInfinity(tempo))return false;
        debugSession=true;debugBoardWidth=board.Width;
        debugTempo=Mathf.Round(Mathf.Clamp(tempo,1,FinalArenaMaxTempo)*10)/10;
        debugMessage=$"다음 패턴부터 함정 배속 {debugTempo:0.0}배 · 예고 시간을 직접 지정하면 그 값이 우선합니다.";
        return true;
    }

    public bool DebugUseAutomaticTempo(){
        if(!DebugAvailable)return false;
        debugTempo=-1;
        debugMessage="다음 패턴부터 3×3 진입 시간에 따라 1~2배로 자동 가속합니다.";
        return true;
    }

    public bool DebugUseAutomaticWarning(){
        if(!DebugAvailable)return false;
        debugWarningSeconds=-1;
        debugMessage="다음 패턴부터 패턴별 기본 예고 시간에 현재 배속을 적용합니다.";
        return true;
    }

    public bool DebugRestart(){
        if(!DebugAvailable)return false;
        PrepareDebugArena(board.Width);
        ResetBoard();phase=Phase.Rest;remaining=.1f;
        debugMessage="선택한 맵에서 시간과 패턴 횟수를 초기화했습니다.";
        return true;
    }

    public bool DebugRestoreNormalGame(){
        if(!DebugAvailable)return false;
        debugSession=false;debugAutomatic=true;debugPressure=-1;debugWarningSeconds=-1;debugTempo=-1;debugBoardWidth=10;
        ResetBoard();deathFocus.Reveal(1);transitionTime=0;phase=Phase.SoloReady;sound.ResumeMusic();
        debugMessage="10×10 일반 게임으로 복원했습니다. 최고 기록을 다시 저장합니다.";
        return true;
    }

    void DrawDebugControls(){
        if(board==null || lan==null || lan.Busy || lan.Editing)return;
        float scale=Mathf.Clamp(Screen.height/640f,.65f,1.6f);
        GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
        float width=Screen.width/scale,height=Screen.height/scale;
        if(GUI.Button(new Rect(12,64,158,28),debugOpen?"디버그 닫기 [F1]":"디버그 열기 [F1]"))debugOpen=!debugOpen;
        if(!debugOpen)return;
        var panel=new Rect(12,98,Mathf.Min(370,width-24),Mathf.Max(80,Mathf.Min(542,height-110)));
        GUI.Box(panel,GUIContent.none);
        debugHint??=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=12};
        GUILayout.BeginArea(new Rect(panel.x+9,panel.y+7,panel.width-18,panel.height-14));
        debugScroll=GUILayout.BeginScrollView(debugScroll);
        GUILayout.Label($"실시간 디버그 · {board.Width}×{board.Width} · {elapsed:0.0}초");
        GUILayout.Label(debugSession?"테스트 중 · 최고 기록 저장 안 함":"싱글플레이 전용 · 현재는 일반 게임",debugHint);
        if(!DebugAvailable)GUILayout.Label("착지 연출이 끝나면 사용할 수 있습니다.",debugHint);
        bool wasEnabled=GUI.enabled;GUI.enabled=wasEnabled && DebugAvailable;
        GUILayout.Space(5);GUILayout.Label("맵 크기 (공격을 정리하고 즉시 변경)");
        for(int row=0;row<2;row++){
            GUILayout.BeginHorizontal();
            for(int column=0;column<4;column++){
                int size=3+row*4+column;
                if(GUILayout.Button($"{size}×{size}",GUILayout.Height(25)))DebugSetMapSize(size);
            }
            GUILayout.EndHorizontal();
        }
        bool automatic=GUILayout.Toggle(debugAutomatic,"자동 패턴 진행 (다음 공격과 붕괴)");
        if(automatic!=debugAutomatic)DebugSetAutomaticPatterns(automatic);
        GUILayout.Label($"다음 패턴 난이도 · {Pressure*100:0}%");
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("초반 0%"))DebugSetPressure(0);
        if(GUILayout.Button("중반 50%"))DebugSetPressure(.5f);
        if(GUILayout.Button("최대 100%"))DebugSetPressure(1);
        GUILayout.EndHorizontal();
        GUILayout.Space(5);
        GUILayout.Label(debugTempo<0?$"함정 배속 · 자동 {FinalArenaTempo:0.00}배 · 3×3 {finalArenaSeconds:0.0}/{FinalArenaRampSeconds:0}초":$"함정 배속 · 수동 {debugTempo:0.0}배 (다음 패턴)");
        float selectedTempo=debugTempo<0?FinalArenaTempo:debugTempo;
        float changedTempo=GUILayout.HorizontalSlider(selectedTempo,1,FinalArenaMaxTempo);
        if(Mathf.Abs(changedTempo-selectedTempo)>.001f)DebugSetTempo(changedTempo);
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("자동"))DebugUseAutomaticTempo();
        if(GUILayout.Button("1.0배"))DebugSetTempo(1);
        if(GUILayout.Button("1.5배"))DebugSetTempo(1.5f);
        if(GUILayout.Button("2.0배"))DebugSetTempo(2);
        GUILayout.EndHorizontal();
        GUILayout.Label("배속 예고 최소 0.6초 · 아래에서 초를 지정하면 우선 적용",debugHint);
        GUILayout.Label(debugWarningSeconds<0?"함정 발동 대기 · 자동":$"함정 발동 대기 · {debugWarningSeconds:0.0}초 (다음 패턴)");
        float selectedWarning=debugWarningSeconds<0?2f:debugWarningSeconds;
        float changedWarning=GUILayout.HorizontalSlider(selectedWarning,.1f,3f);
        if(Mathf.Abs(changedWarning-selectedWarning)>.001f)DebugSetWarningSeconds(changedWarning);
        GUILayout.Label("빠름 0.1초 ← 예고 후 발동까지 → 느림 3.0초",debugHint);
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("자동"))DebugUseAutomaticWarning();
        if(GUILayout.Button("1.0초"))DebugSetWarningSeconds(1f);
        if(GUILayout.Button("0.5초"))DebugSetWarningSeconds(.5f);
        if(GUILayout.Button("0.1초"))DebugSetWarningSeconds(.1f);
        GUILayout.EndHorizontal();
        GUILayout.Space(6);GUILayout.Label("강제 패턴 (기존 공격 교체 · 예고부터 실행)");
        for(int i=0;i<DebugPatternLabels.Length;i+=2){
            GUILayout.BeginHorizontal();
            for(int j=i;j<Mathf.Min(i+2,DebugPatternLabels.Length);j++){
                if(GUILayout.Button(DebugPatternLabels[j],GUILayout.Height(26)))DebugForcePattern((ChamberDebugPattern)j);
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(6);GUILayout.BeginHorizontal();
        if(GUILayout.Button("공격 지우기",GUILayout.Height(27)))DebugClearAttacks();
        if(GUILayout.Button("테스트 초기화",GUILayout.Height(27)))DebugRestart();
        GUILayout.EndHorizontal();
        if(GUILayout.Button("10×10 일반 게임 복원",GUILayout.Height(27)))DebugRestoreNormalGame();
        GUI.enabled=wasEnabled;
        GUILayout.Label(debugMessage,debugHint);
        GUILayout.EndScrollView();GUILayout.EndArea();
    }
}
#endif
