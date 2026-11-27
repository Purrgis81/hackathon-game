using UnityEngine;

public enum TrapKind { Spike, Arrow, Poison, Fall, Wave, Lava }
public enum CountdownSoundStyle {
    [InspectorName("여성 음성")] Voice,
    [InspectorName("영화 카운트다운")] Film,
    [InspectorName("8비트")] EightBit,
    [InspectorName("짧은 비프음")] ShortBeep,
    [InspectorName("아케이드")] Arcade
}

public sealed class ChamberAudio : MonoBehaviour
{
    AudioSource music, room, mechanism, effects, impact, movement;
    AudioLowPassFilter musicFilter;
    AudioClip latch, lift, block, startWhistle;
    AudioSource countdown;
    readonly AudioClip[,] countdownClips=new AudioClip[5,3];
    AudioSource collisions;
    AudioSource hazardWarnings;
    AudioClip waveWarning,lavaWarning;
    AudioClip[] traps, hits;
    float targetVolume;
    float targetCutoff = 8500f;
    const float MusicVolume = .33f;

    void Awake()
    {
        var score = new GameObject("Chase score");
        score.transform.SetParent(transform,false);
        music = score.AddComponent<AudioSource>();
        music.playOnAwake = false;
        musicFilter = score.AddComponent<AudioLowPassFilter>();
        musicFilter.cutoffFrequency = targetCutoff;
        musicFilter.lowpassResonanceQ = 1f;
        collisions=Source(); block=Clip("BodyBlock");
        hazardWarnings=Source();waveWarning=Clip("WaveWarning");lavaWarning=Clip("LavaWarning");
        effects = Source();
        movement = Source();
        impact = Source();
        mechanism = Source();
        room = Source();
        room.loop = true;
        room.volume = .13f;
        room.clip = Clip("ChamberAir");
        room.Play();
        music.loop = true;
        music.volume = 0;
        music.clip = Clip("PartyTheme");
        lift = Clip("JumpStep");
        traps = new[] { Clip("SpikeRise"), Clip("ArrowLoose"), Clip("PoisonSpill"),block,Clip("WaveSurge"),Clip("LavaImpact") };
        hits = new[] { Clip("SpikeHit"), Clip("ArrowHit"), Clip("PoisonHit"),block,Clip("WaveHit"),Clip("LavaImpact") };
        latch = Clip("PlateLatch");
        startWhistle = Clip("StartWhistle");
        countdown = Source();
        var styles=new[]{"Voice","Film","EightBit","ShortBeep","Arcade"};
        for(int style=0;style<styles.Length;style++)
            for(int number=1;number<=3;number++)
                countdownClips[style,number-1]=Clip("Countdown"+styles[style]+number);
    }

    AudioSource Source()
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;
        return source;
    }

    static AudioClip Clip(string name) => Resources.Load<AudioClip>("Sound/" + name);

    public void Countdown(int number,CountdownSoundStyle style,float volume)
    {
        if(number<1||number>3)return;
        countdown.Stop();
        countdown.clip=countdownClips[Mathf.Clamp((int)style,0,countdownClips.GetLength(0)-1),number-1];
        countdown.volume=Mathf.Clamp01(volume);
        if(countdown.clip)countdown.Play();
    }

    public void StartWhistle()
    {
        countdown.Stop();
        effects.PlayOneShot(startWhistle,.65f);
    }

    public void PauseMusic() => music.Pause();

    public void ResumeMusic()
    {
        countdown.Stop();
        hazardWarnings.Stop();
        music.UnPause();
        if (!music.isPlaying) music.Play();
        targetVolume = MusicVolume * .58f;
        targetCutoff = 8500f;
    }

    void Update()
    {
        music.volume = Mathf.MoveTowards(music.volume, targetVolume, Time.unscaledDeltaTime * .8f);
        musicFilter.cutoffFrequency = Mathf.Lerp(musicFilter.cutoffFrequency,targetCutoff,
            1-Mathf.Exp(-Time.unscaledDeltaTime*10));
    }

    public void PushOff(bool isLocalPlayer)
    {
        targetVolume = MusicVolume;
        targetCutoff = 15000f;
        if (isLocalPlayer) movement.PlayOneShot(lift,.36f);
    }

    public void Block(float pan)
    {
        collisions.pitch=1;
        collisions.panStereo=Mathf.Clamp(pan,-.65f,.65f);
        collisions.PlayOneShot(block,.88f);
    }

    public void Latch(float pan, bool urgent = false)
    {
        mechanism.panStereo = Mathf.Clamp(pan, -.58f,.58f);
        mechanism.pitch = urgent ? 1.20f : .86f;
        mechanism.PlayOneShot(latch,urgent ? .48f : .33f);
    }

    public void Activate(TrapKind trap) => effects.PlayOneShot(traps[(int)trap], .37f);
    public void WaveWarning()=>hazardWarnings.PlayOneShot(waveWarning,.52f);
    public void LavaWarning()=>hazardWarnings.PlayOneShot(lavaWarning,.78f);
    public void WaveCaught(){
        collisions.pitch=.95f;collisions.PlayOneShot(hits[(int)TrapKind.Wave],.5f);
    }
    public void StunTap(int count){
        collisions.pitch=1+count*.08f;collisions.PlayOneShot(latch,.22f);
    }

    public void Eliminated(TrapKind trap)=>impact.PlayOneShot(trap==TrapKind.Fall?block:hits[(int)trap],.78f);

    public void Hit(TrapKind trap)
    {
        effects.Stop();
        mechanism.Stop();
        movement.Stop();
        hazardWarnings.Stop();
        impact.clip = trap==TrapKind.Fall ? block : hits[(int)trap];
        impact.volume = .80f;
        impact.Play();
        music.volume = targetVolume = MusicVolume * .22f;
        targetCutoff = 820f;
    }
}
