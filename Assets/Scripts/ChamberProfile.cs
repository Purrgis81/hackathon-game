using System.Text;
using UnityEngine;

public struct ChamberProfile
{
    static readonly Color[] Colors={new Color(1,.65f,.27f),new Color(.16f,.82f,1),new Color(.81f,.46f,1),new Color(.50f,.94f,.36f),new Color(1,.38f,.50f),new Color(1,.91f,.46f)};
    public string Name;
    public int Swatch;
    public int Animal;
    public static readonly string[] AnimalLabels={"자동","곰","토끼","개구리","고양이"};
    public string Label(int slot)=>string.IsNullOrWhiteSpace(Name)?$"{slot+1}P":Name;
    // Negative values carry a 24-bit RGB color in the existing save and network swatch field.
    public static int CustomSwatch(Color32 color)=>-1-((color.r<<16)|(color.g<<8)|color.b);
    public static int PresetCount=>Colors.Length;
    public Color Tint(int slot){
        if(Swatch>=0)return Colors[Swatch==0?slot%Colors.Length:Mathf.Clamp(Swatch-1,0,Colors.Length-1)];
        int rgb=-(Swatch+1);
        return new Color32((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb,255);
    }
    public Color ModelTint(int slot)=>Swatch<0?Tint(slot):Color.Lerp(Color.white,Tint(slot),Swatch==0?.12f:.60f);
    public static ChamberProfile Create(string name,int swatch,int animal=0){
        var b=new StringBuilder();
        foreach(char ch in (name??"").Trim()){
            if(b.Length>=12)break;
            if(char.IsLetterOrDigit(ch)||ch==' '||ch=='_'||ch=='-')b.Append(ch);
        }
        return new ChamberProfile{Name=b.ToString().Trim(),Swatch=swatch < -0x1000000 ? 0 : Mathf.Min(swatch,Colors.Length),Animal=Mathf.Clamp(animal,0,AnimalLabels.Length-1)};
    }
    public static ChamberProfile Load()=>Create(PlayerPrefs.GetString("Chamber.Name",""),PlayerPrefs.GetInt("Chamber.Color",0),PlayerPrefs.GetInt("Chamber.Animal",0));
    public void Save(){PlayerPrefs.SetString("Chamber.Name",Name);PlayerPrefs.SetInt("Chamber.Color",Swatch);PlayerPrefs.SetInt("Chamber.Animal",Animal);PlayerPrefs.Save();}
}
