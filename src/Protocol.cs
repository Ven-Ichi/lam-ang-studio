using System;
using System.Collections.Generic;
using System.Linq;

namespace LamAngStudio
{
    public static class Protocol
    {
        static void Mark(byte[] data,int offset) { data[offset]=0xaa;data[offset+1]=0x55; }
        public static byte[] Lighting(Profile p)
        {
            ProfileStore.Validate(p);
            var data=new byte[1152];
            if(!p.LightingOn){Mark(data,0x44e);return data;}
            for(int i=0;i<20;i++)
            {
                var s=p.Lights[i];int at=i*16;data[at]=(byte)(i==19?128:i+1);
                if(i!=19){data[at+1]=(byte)s.Red;data[at+2]=(byte)s.Green;data[at+3]=(byte)s.Blue;data[at+8]=(byte)(s.FullColor?1:0);data[at+10]=(byte)s.Speed;data[at+11]=(byte)s.Direction;}
                data[at+9]=(byte)s.Brightness;Mark(data,at+14);
            }
            for(int i=0;i<144;i++)data[0x200+i*4]=128;
            foreach(var c in p.Colors){int at=0x200+DeviceData.Indices[c.Usage]*4;data[at+1]=(byte)c.Red;data[at+2]=(byte)c.Green;data[at+3]=(byte)c.Blue;}
            Buffer.BlockCopy(data,(p.Effect-1)*16,data,0x440,16);
            // The vendor retains selected-mode parameters in the active record,
            // even though UserDefine's stored mode record omits color and speed.
            if(p.Effect==20){var active=p.Lights[19];data[0x441]=(byte)active.Red;data[0x442]=(byte)active.Green;data[0x443]=(byte)active.Blue;data[0x448]=(byte)(active.FullColor?1:0);data[0x44a]=(byte)active.Speed;data[0x44b]=(byte)active.Direction;}
            return data;
        }
        public static byte[] Mapping(Profile p)
        {
            ProfileStore.Validate(p);var data=new byte[640];
            foreach(var a in p.Assignments)
            {
                int at=DeviceData.Indices[a.Usage]*4;
                switch(a.Kind)
                {
                    case "Default":break;
                    case "Disabled":data[at]=5;data[at+1]=3;break;
                    case "Keyboard":data[at]=2;data[at+1]=(byte)a.Modifiers;data[at+2]=(byte)a.Value;break;
                    case "Chord":data[at]=7;for(int i=0;i<a.Chord.Length;i++)data[at+i+1]=(byte)a.Chord[i];break;
                    case "Macro":
                        var macro=p.Macros[a.Value];data[at]=6;data[at+1]=(byte)a.Value;
                        data[at+2]=(byte)(macro.Playback=="Toggle"?2:macro.Playback=="Repeat"&&macro.Repeats>1?1:0);
                        data[at+3]=(byte)(macro.Playback=="Toggle"?0:macro.Playback=="Repeat"?macro.Repeats:1);break;
                    case "Mouse":
                        data[at]=1;
                        if(a.Value==6||a.Value==7){data[at+1]=3;data[at+2]=(byte)(a.Value==6?1:255);}
                        else{data[at+1]=1;data[at+2]=(byte)new[]{0,1,4,2,8,16}[a.Value];}break;
                    case "Media":
                        data[at]=3;data[at+1]=(byte)new[]{0,0xcd,0xb7,0xb6,0xb5,0xe9,0xea,0xe2}[a.Value];break;
                    case "Windows":
                        var shortcuts=new Dictionary<int,byte[]>{{1,new byte[]{2,8,7,0}},{2,new byte[]{2,8,8,0}},{3,new byte[]{2,8,15,0}},{4,new byte[]{2,1,26,0}},{5,new byte[]{2,4,43,0}},{6,new byte[]{2,1,6,0}},{7,new byte[]{2,1,25,0}},{8,new byte[]{2,1,27,0}},{13,new byte[]{3,0x92,1,0}},{14,new byte[]{3,0x23,2,0}},{15,new byte[]{3,0x8a,1,0}},{16,new byte[]{3,0x83,1,0}}};
                        Buffer.BlockCopy(shortcuts[a.Value],0,data,at,4);break;
                    default:data[at]=5;data[at+1]=2;data[at+2]=(byte)DeviceData.Indices[a.Usage];break;
                }
            }
            Mark(data,638);return data;
        }
        public static byte[] Gaming(Profile p)
        {
            ProfileStore.Validate(p);var data=new byte[64];data[0]=10;
            if(p.GamingEnabled){data[1]=1;data[2]=(byte)(p.LockAltTab?1:0);data[3]=(byte)(p.LockAltF4?1:0);data[4]=(byte)(p.LockWindows?1:0);}
            Mark(data,62);return data;
        }
        static void Delay(List<byte[]> records,int value)
        {
            while(value>0){int part=Math.Min(65535,value);records.Add(new byte[]{(byte)part,(byte)(part>>8),0,0x50});value-=part;}
        }
        public static byte[] Macros(Profile p)
        {
            ProfileStore.Validate(p);var content=new List<byte>(new byte[400]);
            for(int slot=0;slot<p.Macros.Count;slot++)
            {
                var macro=p.Macros[slot];var records=new List<byte[]>();bool previousDelay=false;
                foreach(var e in macro.Events)
                {
                    if(e.Kind=="Delay") { if(macro.DelayMode=="Recorded"){Delay(records,e.Value);previousDelay=true;}continue; }
                    if(records.Count>0&&(!previousDelay||macro.DelayMode=="Fixed"))Delay(records,macro.DelayMode=="Fixed"?macro.FixedDelay:10);
                    byte key=(byte)e.Value,tag=0;
                    if(e.Kind=="Key down")tag=0xb0;else if(e.Kind=="Key up")tag=0x30;
                    else if(e.Kind=="Mouse down"||e.Kind=="Mouse up"){key=(byte)new[]{0,1,4,2,8,16}[e.Value];tag=(byte)(e.Kind=="Mouse down"?0x90:0x10);}
                    else if(e.Kind=="Wheel"){key=(byte)(e.Value>0?1:255);tag=0x20;}
                    records.Add(new byte[]{0,0,key,tag});previousDelay=false;
                }
                if(records.Count==0){for(int j=0;j<4;j++)content[slot*4+j]=255;continue;}
                int offset=content.Count;for(int j=0;j<4;j++)content[slot*4+j]=(byte)(offset>>(8*j));
                content.Add((byte)records.Count);content.Add((byte)(records.Count>>8));content.AddRange(new byte[6]);
                foreach(var record in records)content.AddRange(record);
            }
            int blocks=content.Count/64+(content.Count%64==0?1:2),size=blocks*64;
            if(size>3200)throw new ArgumentException("Macros exceed the keyboard's 3200-byte transfer buffer. Reduce events or macros.");
            var result=new byte[size];content.CopyTo(result);Mark(result,size-2);return result;
        }
    }
}
