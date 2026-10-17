using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace LamAngStudio
{
    public class LightSetting
    {
        public int Red=0, Green=0, Blue=255, Brightness=15, Speed=10, Direction=0;
        public bool FullColor=true;
    }
    public class KeyColor { public int Usage; public int Red; public int Green; public int Blue; }
    public class Assignment
    {
        public int Usage;
        public string Kind="Default";
        public int Value;
        public int Modifiers;
        public int[] Chord=new int[0];
        public string Target="";
        public Assignment Copy(){return new Assignment{Usage=Usage,Kind=Kind,Value=Value,Modifiers=Modifiers,Chord=Chord==null?null:(int[])Chord.Clone(),Target=Target};}
    }
    public class MacroEvent
    {
        public string Kind="Key down";
        public int Value=4;
        public override string ToString() { return Kind+"  "+(Kind=="Delay"?Value+" ms":Kind=="Key down"||Kind=="Key up"?DeviceData.KeyName(Value):Value.ToString()); }
    }
    public class Macro
    {
        public string Name="New macro", Playback="Once", DelayMode="Recorded";
        public int Repeats=1, FixedDelay=100;
        public List<MacroEvent> Events=new List<MacroEvent>();
    }
    public class Profile
    {
        public int Version=1;
        public string Name="My keyboard";
        public int Effect=1;
        public bool LightingOn=true, GamingEnabled=false, LockAltTab=false, LockAltF4=false, LockWindows=false;
        public LightSetting[] Lights=new LightSetting[20];
        public List<KeyColor> Colors=new List<KeyColor>();
        public List<Assignment> Assignments=new List<Assignment>();
        public List<Macro> Macros=new List<Macro>();
        public Profile()
        {
            for(int i=0;i<20;i++) Lights[i]=new LightSetting();
            Lights[9].Direction=3;
            Lights[19]=new LightSetting{FullColor=false,Red=0,Green=0,Blue=0,Brightness=16,Speed=0};
        }
    }
    public class KeyDef
    {
        public int Usage; public string Name; public double X,Y,Width;
        public KeyDef(int usage,string name,double x,double y,double width)
        { Usage=usage;Name=name;X=x;Y=y;Width=width; }
        public override string ToString() { return Name; }
    }
    public static class MacroEditing
    {
        static List<MacroEvent> Releases(IEnumerable<MacroEvent> events)
        {var keys=new HashSet<int>();var buttons=new HashSet<int>();foreach(var e in events){if(e.Kind=="Key down")keys.Add(e.Value);else if(e.Kind=="Key up")keys.Remove(e.Value);else if(e.Kind=="Mouse down")buttons.Add(e.Value);else if(e.Kind=="Mouse up")buttons.Remove(e.Value);}return keys.Select(k=>new MacroEvent{Kind="Key up",Value=k}).Concat(buttons.Select(b=>new MacroEvent{Kind="Mouse up",Value=b})).ToList();}
        public static bool CanStartRecording(Macro macro){return macro.Events.Count<600&&macro.Events.Count+Releases(macro.Events).Count<=600;}
        public static bool TryAppendRecording(Macro macro,MacroEvent e){if(macro.Events.Count+1+Releases(macro.Events.Concat(new[]{e})).Count>600)return false;macro.Events.Add(e);return true;}
        public static void FinishRecording(Macro macro)
        {var releases=Releases(macro.Events);if(macro.Events.Count+releases.Count>600)throw new ArgumentException("This macro has no room for required release events. Remove events before recording.");macro.Events.AddRange(releases);}
    }
    public sealed class DeviceState
    {
        readonly Dictionary<string,byte[]> acknowledged=new Dictionary<string,byte[]>();
        static byte[] Bytes(string part,Profile p){switch(part){case "lighting":return Protocol.Lighting(p);case "mapping":return Protocol.Mapping(p);case "macros":return Protocol.Macros(p);case "gaming":return Protocol.Gaming(p);default:throw new ArgumentException("Unknown settings section.");}}
        public void Remember(Profile profile,IEnumerable<string> parts) { foreach(string part in parts)acknowledged[part]=Bytes(part,profile); }
        public string[] ChangedParts(Profile profile) { var changed=new List<string>();foreach(var pair in acknowledged){try{if(!pair.Value.SequenceEqual(Bytes(pair.Key,profile)))changed.Add(pair.Key);}catch(ArgumentException){changed.Add(pair.Key);}}return changed.ToArray(); }
        public void Clear() { acknowledged.Clear(); }
        public void Invalidate(IEnumerable<string> parts) { foreach(string part in parts)acknowledged.Remove(part); }
    }
    public static class DeviceData
    {
        public static readonly string[] Effects={"Static","SingleOn","SingleOff","Glittering","Falling","Colourful","Breath","Spectrum","Outward","Scrolling","Rolling","Rotating","Explode","Launch","Ripples","Flowing","Pulsating","Tilt","Shuttle","UserDefine"};
        public static readonly int[] Indices=ReadIndices();
        static int[] ReadIndices()
        { using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("KeyIndices.json"))using(var reader=new StreamReader(stream))return new JavaScriptSerializer().Deserialize<int[]>(reader.ReadToEnd()); }
        public static readonly List<KeyDef> Keys=MakeKeys();
        public static List<KeyDef> Destinations(){var keys=new List<KeyDef>(Keys);for(int usage=1;usage<=255;usage++)if(!keys.Any(k=>k.Usage==usage))keys.Add(new KeyDef(usage,"USB HID 0x"+usage.ToString("X2"),0,0,1));return keys;}
        static void Add(List<KeyDef> keys,int usage,string name,double x,double y,double width)
        { keys.Add(new KeyDef(usage,name,x,y,width)); }
        static List<KeyDef> MakeKeys()
        {
            var k=new List<KeyDef>();Add(k,41,"Esc",0,0,1);
            for(int i=0;i<12;i++) Add(k,58+i,"F"+(i+1),i<4?i+2:i<8?i+2.5:i+3,0,1);
            Add(k,70,"PrtSc",15.5,0,1);Add(k,71,"ScrLk",16.5,0,1);Add(k,72,"Pause",17.5,0,1);
            Add(k,53,"`",0,1.45,1);for(int i=0;i<10;i++)Add(k,30+i,i==9?"0":(i+1).ToString(),i+1,1.45,1);
            Add(k,45,"-",11,1.45,1);Add(k,46,"=",12,1.45,1);Add(k,42,"Backspace",13,1.45,2);
            Add(k,73,"Ins",15.5,1.45,1);Add(k,74,"Home",16.5,1.45,1);Add(k,75,"PgUp",17.5,1.45,1);
            Add(k,43,"Tab",0,2.45,1.5);
            int[] top={20,26,8,21,23,28,24,12,18,19,47,48};string[] tn={"Q","W","E","R","T","Y","U","I","O","P","[","]"};
            for(int i=0;i<top.Length;i++)Add(k,top[i],tn[i],1.5+i,2.45,1);
            Add(k,49,"\\",13.5,2.45,1.5);Add(k,76,"Del",15.5,2.45,1);Add(k,77,"End",16.5,2.45,1);Add(k,78,"PgDn",17.5,2.45,1);
            Add(k,57,"Caps",0,3.45,1.75);int[] mid={4,22,7,9,10,11,13,14,15,51,52};string[] mn={"A","S","D","F","G","H","J","K","L",";","'"};
            for(int i=0;i<mid.Length;i++)Add(k,mid[i],mn[i],1.75+i,3.45,1);Add(k,40,"Enter",12.75,3.45,2.25);
            Add(k,225,"Shift",0,4.45,2.25);int[] low={29,27,6,25,5,17,16,54,55,56};string[] ln={"Z","X","C","V","B","N","M",",",".","/"};
            for(int i=0;i<low.Length;i++)Add(k,low[i],ln[i],2.25+i,4.45,1);Add(k,229,"RShift",12.25,4.45,2.75);Add(k,82,"↑",16.5,4.45,1);
            Add(k,224,"Ctrl",0,5.45,1.25);Add(k,227,"Win",1.25,5.45,1.25);Add(k,226,"Alt",2.5,5.45,1.25);Add(k,44,"Space",3.75,5.45,6.25);Add(k,230,"RAlt",10,5.45,1.25);Add(k,175,"Fn",11.25,5.45,1.25);Add(k,101,"Menu",12.5,5.45,1.25);Add(k,228,"RCtrl",13.75,5.45,1.25);Add(k,80,"←",15.5,5.45,1);Add(k,81,"↓",16.5,5.45,1);Add(k,79,"→",17.5,5.45,1);
            return k;
        }
        public static bool HasColor(int effect) { return effect!=6&&effect!=8&&effect!=20; }
        public static bool HasSpeed(int effect) { return effect!=1&&effect!=20; }
        public static int[] Directions(int effect) { return effect==10?new[]{2,3}:new[]{11,16,18}.Contains(effect)?new[]{1,0}:new int[0]; }
        public static string KeyName(int usage) { var key=Keys.FirstOrDefault(k=>k.Usage==usage);return key==null?"HID "+usage:key.Name; }
    }
    public static class ProfileStore
    {
        public static Profile Load(string path) { if(new FileInfo(path).Length>1024*1024)throw new ArgumentException("Profile file exceeds the 1 MiB limit.");return Decode(File.ReadAllText(path)); }
        public static string Encode(Profile p) { return new JavaScriptSerializer{MaxJsonLength=1024*1024}.Serialize(p); }
        public static Profile Decode(string json)
        {
            if(json==null||json.Length>1024*1024)throw new ArgumentException("Profile is missing or too large.");
            Profile p;try{var serializer=new JavaScriptSerializer{MaxJsonLength=1024*1024};var root=Require(serializer.DeserializeObject(json),"Version","Name","Effect","LightingOn","GamingEnabled","LockAltTab","LockAltF4","LockWindows","Lights","Colors","Assignments","Macros");foreach(object s in Items(root["Lights"]))Require(s,"Red","Green","Blue","Brightness","Speed","Direction","FullColor");foreach(object c in Items(root["Colors"]))Require(c,"Usage","Red","Green","Blue");foreach(object a in Items(root["Assignments"]))Require(a,"Usage","Kind","Value","Modifiers","Chord","Target");foreach(object m in Items(root["Macros"])){var data=Require(m,"Name","Playback","DelayMode","Repeats","FixedDelay","Events");foreach(object e in Items(data["Events"]))Require(e,"Kind","Value");}p=serializer.Deserialize<Profile>(json);}catch(Exception e){throw new ArgumentException("Invalid profile JSON: "+e.Message);}
            Validate(p);return p;
        }
        static Dictionary<string,object> Require(object value,params string[] members){var data=value as Dictionary<string,object>;if(data==null||members.Any(m=>!data.ContainsKey(m)||data[m]==null))throw new ArgumentException("Profile is missing required data: "+string.Join(", ",members));return data;}
        static object[] Items(object value){var array=value as object[];if(array==null)throw new ArgumentException("Expected a profile collection.");return array;}
        static void Range(int value,int min,int max,string name){if(value<min||value>max)throw new ArgumentException(name+" must be "+min+"–"+max+".");}
        public static void Validate(Profile p)
        {
            if(p==null||p.Version!=1)throw new ArgumentException("Unsupported or missing profile version.");
            if(string.IsNullOrWhiteSpace(p.Name)||p.Name.Length>120)throw new ArgumentException("Profile name must contain 1–120 characters.");
            Range(p.Effect,1,20,"Effect");
            if(p.Lights==null||p.Lights.Length!=20||p.Colors==null||p.Assignments==null||p.Macros==null)throw new ArgumentException("Profile must include all lighting, color, assignment and macro collections.");
            for(int i=0;i<20;i++){var s=p.Lights[i];if(s==null)throw new ArgumentException("Missing effect settings.");Range(s.Red,0,255,"Red");Range(s.Green,0,255,"Green");Range(s.Blue,0,255,"Blue");Range(s.Brightness,0,i==19?16:15,"Brightness");Range(s.Speed,0,10,"Speed");Range(s.Direction,0,3,"Direction");}
            var usages=new HashSet<int>();
            foreach(var c in p.Colors){if(c==null||!DeviceData.Keys.Any(k=>k.Usage==c.Usage)||!usages.Add(c.Usage))throw new ArgumentException("Invalid or duplicate custom key color.");Range(c.Red,0,255,"Red");Range(c.Green,0,255,"Green");Range(c.Blue,0,255,"Blue");}
            if(p.Macros.Count>100)throw new ArgumentException("The transfer format supports at most 100 macro slots.");
            foreach(var m in p.Macros)
            {
                if(m==null||string.IsNullOrWhiteSpace(m.Name)||m.Name.Length>120||m.Events==null||m.Events.Count>600)throw new ArgumentException("Invalid macro name or event collection.");
                if(!new[]{"Once","Repeat","Toggle"}.Contains(m.Playback)||!new[]{"Recorded","Fixed"}.Contains(m.DelayMode))throw new ArgumentException("Invalid macro playback or delay mode.");
                Range(m.Repeats,1,255,"Macro repeats");Range(m.FixedDelay,1,999999,"Fixed delay");
                foreach(var e in m.Events){if(e==null)throw new ArgumentException("Missing macro event.");switch(e.Kind){case "Delay":Range(e.Value,1,999999,"Delay");break;case "Key down":case "Key up":Range(e.Value,4,231,"HID key");break;case "Mouse down":case "Mouse up":Range(e.Value,1,5,"Mouse button");break;case "Wheel":if(e.Value!=1&&e.Value!=-1)throw new ArgumentException("Wheel direction must be 1 or -1.");break;default:throw new ArgumentException("Unsupported macro event.");}}
            }
            usages.Clear();
            foreach(var a in p.Assignments)
            {
                if(a==null||!DeviceData.Keys.Any(k=>k.Usage==a.Usage)||!usages.Add(a.Usage))throw new ArgumentException("Invalid or duplicate physical key assignment.");
                switch(a.Kind)
                {
                    case "Default":case "Disabled":break;
                    case "Keyboard":Range(a.Value,1,255,"Destination HID key");Range(a.Modifiers,0,255,"Modifier mask");break;
                    case "Chord":if(a.Chord==null||a.Chord.Length<1||a.Chord.Length>3||a.Chord.Any(k=>k<1||k>255))throw new ArgumentException("A chord needs 1–3 HID key codes.");break;
                    case "Macro":Range(a.Value,0,p.Macros.Count-1,"Macro slot");if(p.Macros[a.Value].Events.Count==0)throw new ArgumentException("An assigned macro needs at least one event.");break;
                    case "Mouse":Range(a.Value,1,7,"Mouse action");break;
                    case "Media":Range(a.Value,1,7,"Media action");break;
                    case "Windows":if(!new[]{1,2,3,4,5,6,7,8,13,14,15,16}.Contains(a.Value))throw new ArgumentException("Unknown Windows shortcut.");break;
                    case "Program":if(string.IsNullOrWhiteSpace(a.Target)||!Path.IsPathRooted(a.Target))throw new ArgumentException("Choose an absolute program path.");break;
                    case "Website":Uri uri;if(!Uri.TryCreate(a.Target,UriKind.Absolute,out uri)||(uri.Scheme!="http"&&uri.Scheme!="https"))throw new ArgumentException("Use an http:// or https:// website address.");break;
                    case "Text":if(a.Target==null||a.Target.Length>10000)throw new ArgumentException("Text action is too long.");break;
                    case "Profile":if(string.IsNullOrWhiteSpace(a.Target)||a.Target.Length>120)throw new ArgumentException("Choose a local profile name.");break;
                    default:throw new ArgumentException("Unsupported assignment type.");
                }
            }
        }
        public static void Save(string path,Profile p)
        {
            Validate(p);string json=Encode(p);if(Encoding.UTF8.GetByteCount(json)>1024*1024)throw new ArgumentException("Encoded profile exceeds the 1 MiB file limit.");string full=Path.GetFullPath(path),parent=Path.GetDirectoryName(full);
            if(!Directory.Exists(parent))Directory.CreateDirectory(parent);
            string temp=full+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllText(temp,json,new UTF8Encoding(false));if(File.Exists(full))File.Replace(temp,full,full+".backup");else File.Move(temp,full);}finally{if(File.Exists(temp))File.Delete(temp);}
        }
        public static Profile Clone(Profile p) { return new JavaScriptSerializer().Deserialize<Profile>(Encode(p)); }
    }
}
