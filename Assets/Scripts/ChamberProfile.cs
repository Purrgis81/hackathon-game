using System.Text;
using UnityEngine;

public struct ChamberProfile
{
    static readonly Color[] Colors={new Color(1,.65f,.27f),new Color(.16f,.82f,1),new Color(.81f,.46f,1),new Color(.50f,.94f,.36f),new Color(1,.38f,.50f),new Color(1,.91f,.46f)};
    static readonly string[] Labels={"자동","호박","하늘","보라","연두","장미","레몬"};
    public string Name;
    public int Swatch;
    public string Label(int slot)=>string.IsNullOrWhiteSpace(Name)?$"{slot+1}P":Name;
    public Color Tint(int slot)=>Colors[Swatch==0?slot%Colors.Length:Mathf.Clamp(Swatch-1,0,Colors.Length-1)];
    public static ChamberProfile Create(string name,int swatch){
        var b=new StringBuilder();
        foreach(char ch in (name??"").Trim()){
            if(b.Length>=12)break;
            if(char.IsLetterOrDigit(ch)||ch==' '||ch=='_'||ch=='-')b.Append(ch);
        }
        return new ChamberProfile{Name=b.ToString().Trim(),Swatch=Mathf.Clamp(swatch,0,Colors.Length)};
    }
    public static ChamberProfile Load()=>Create(PlayerPrefs.GetString("Chamber.Name",""),PlayerPrefs.GetInt("Chamber.Color",0));
    public void Save(){PlayerPrefs.SetString("Chamber.Name",Name);PlayerPrefs.SetInt("Chamber.Color",Swatch);PlayerPrefs.Save();}
    public static int ChooseColor(int current)=>GUILayout.SelectionGrid(current,Labels,4);
}
