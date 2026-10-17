using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LamAngStudio
{
    // One window owns Raw Input and the explicitly enabled macro recording hooks.
    public sealed class HostActions:IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]struct RawDevice{public ushort Page,Usage;public uint Flags;public IntPtr Window;}
        [StructLayout(LayoutKind.Sequential)]struct RawHeader{public uint Type,Size;public IntPtr Device,WParam;}
        [StructLayout(LayoutKind.Sequential)]struct KeyboardHook{public uint Vk,Scan,Flags,Time;public UIntPtr Extra;}
        [StructLayout(LayoutKind.Sequential)]struct MouseHook{public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
        [StructLayout(LayoutKind.Sequential)]struct KeyInput{public ushort Vk,Scan;public uint Flags,Time;public UIntPtr Extra;}
        [StructLayout(LayoutKind.Explicit,Size=40)]struct Input{[FieldOffset(0)]public uint Type;[FieldOffset(8)]public KeyInput Key;}
        delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
        [DllImport("user32.dll",SetLastError=true)]static extern bool RegisterRawInputDevices(RawDevice[] devices,uint count,uint size);
        [DllImport("user32.dll")]static extern uint GetRawInputData(IntPtr input,uint command,IntPtr data,ref uint size,uint header);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint GetRawInputDeviceInfoW(IntPtr device,uint command,IntPtr data,ref uint size);
        [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SetWindowsHookExW(int id,HookProc callback,IntPtr module,uint thread);
        [DllImport("user32.dll")]static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")]static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandleW(string module);
        [DllImport("user32.dll",SetLastError=true)]static extern uint SendInput(uint count,Input[] inputs,int size);
        readonly Window window;readonly HwndSource source;readonly HookProc keyboardCallback,mouseCallback;
        IntPtr keyboardHook,mouseHook;readonly Stopwatch clock=new Stopwatch();long lastRecorded,lastAction;int lastId=-1;
        public Action<string,int> DeviceAction;
        public Action<MacroEvent> Recorded;
        public Action RecordingStopped;
        public bool IsRecording {get;private set;}
        public HostActions(Window window)
        {
            this.window=window;source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);source.AddHook(WindowMessage);
            keyboardCallback=KeyboardEvent;mouseCallback=MouseEvent;
            var d=new RawDevice{Page=0xffff,Usage=1,Flags=0x100,Window=source.Handle};
            if(!RegisterRawInputDevices(new[]{d},1,(uint)Marshal.SizeOf(typeof(RawDevice))))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        IntPtr WindowMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
        {
            if(message!=0xff||IsRecording)return IntPtr.Zero;
            uint size=0,headerSize=(uint)Marshal.SizeOf(typeof(RawHeader));GetRawInputData(lParam,0x10000003,IntPtr.Zero,ref size,headerSize);
            if(size<headerSize+12||size>65536)return IntPtr.Zero;IntPtr data=Marshal.AllocHGlobal((int)size);
            try
            {
                if(GetRawInputData(lParam,0x10000003,data,ref size,headerSize)==uint.MaxValue)return IntPtr.Zero;
                var header=(RawHeader)Marshal.PtrToStructure(data,typeof(RawHeader));if(header.Type!=2)return IntPtr.Zero;
                uint length=0;string identity="";GetRawInputDeviceInfoW(header.Device,0x20000007,IntPtr.Zero,ref length);if(length==0||length>4096)return IntPtr.Zero;
                IntPtr name=Marshal.AllocHGlobal(((int)length+1)*2);
                try{GetRawInputDeviceInfoW(header.Device,0x20000007,name,ref length);string path=Marshal.PtrToStringUni(name,(int)length).TrimEnd('\0');if(path.IndexOf("vid_0c45&pid_8006",StringComparison.OrdinalIgnoreCase)<0)return IntPtr.Zero;identity=HidDevice.IdentityForPath(path);if(string.IsNullOrEmpty(identity))return IntPtr.Zero;}finally{Marshal.FreeHGlobal(name);}
                int at=(int)headerSize,reportSize=Marshal.ReadInt32(data,at),count=Marshal.ReadInt32(data,at+4);
                if(reportSize<4||count<1||at+8+(long)reportSize*count>size)return IntPtr.Zero;
                for(int i=0;i<count;i++){int offset=at+8+i*reportSize;if(Marshal.ReadByte(data,offset)!=5)continue;int id=Marshal.ReadByte(data,offset+2);long now=Stopwatch.GetTimestamp();if(id==lastId&&(now-lastAction)*1000/Stopwatch.Frequency<200)continue;lastId=id;lastAction=now;if(DeviceAction!=null)DeviceAction(identity,id);}
            }
            finally{Marshal.FreeHGlobal(data);}
            return IntPtr.Zero;
        }
        public void StartRecording()
        {
            StopRecording();clock.Restart();lastRecorded=0;IsRecording=true;
            keyboardHook=SetWindowsHookExW(13,keyboardCallback,GetModuleHandleW(null),0);mouseHook=SetWindowsHookExW(14,mouseCallback,GetModuleHandleW(null),0);
            if(keyboardHook==IntPtr.Zero||mouseHook==IntPtr.Zero){int error=Marshal.GetLastWin32Error();StopRecording();throw new System.ComponentModel.Win32Exception(error,"Cannot start macro recorder.");}
        }
        public void StopRecording()
        {bool was=IsRecording;IsRecording=false;if(keyboardHook!=IntPtr.Zero){UnhookWindowsHookEx(keyboardHook);keyboardHook=IntPtr.Zero;}if(mouseHook!=IntPtr.Zero){UnhookWindowsHookEx(mouseHook);mouseHook=IntPtr.Zero;}clock.Stop();if(was&&RecordingStopped!=null)RecordingStopped();}
        void Record(MacroEvent e)
        {
            long now=clock.ElapsedMilliseconds;int delay=(int)Math.Min(999999,now-lastRecorded);lastRecorded=now;
            window.Dispatcher.BeginInvoke(new Action(delegate{if(Recorded==null)return;if(delay>0&&delay<999999)Recorded(new MacroEvent{Kind="Delay",Value=delay});Recorded(e);}));
        }
        IntPtr KeyboardEvent(int code,IntPtr message,IntPtr data)
        {
            if(code>=0&&IsRecording)
            {
                var k=(KeyboardHook)Marshal.PtrToStructure(data,typeof(KeyboardHook));int kind=message.ToInt32();
                if((k.Flags&0x10)==0)
                {
                    if(k.Vk==27&&kind==0x100){window.Dispatcher.BeginInvoke(new Action(StopRecording));}
                    else{int usage=UsageFromVirtualKey((int)k.Vk);if(usage!=0&&(kind==0x100||kind==0x104||kind==0x101||kind==0x105))Record(new MacroEvent{Kind=kind==0x100||kind==0x104?"Key down":"Key up",Value=usage});}
                }
            }
            return CallNextHookEx(keyboardHook,code,message,data);
        }
        IntPtr MouseEvent(int code,IntPtr message,IntPtr data)
        {
            if(code>=0&&IsRecording)
            {
                var m=(MouseHook)Marshal.PtrToStructure(data,typeof(MouseHook));int kind=message.ToInt32();
                if((m.Flags&1)==0)
                {
                    int value=0;bool down=false;
                    switch(kind){case 0x201:value=1;down=true;break;case 0x202:value=1;break;case 0x207:value=2;down=true;break;case 0x208:value=2;break;case 0x204:value=3;down=true;break;case 0x205:value=3;break;case 0x20b:value=(m.Data>>16)==1?4:5;down=true;break;case 0x20c:value=(m.Data>>16)==1?4:5;break;case 0x20a:Record(new MacroEvent{Kind="Wheel",Value=(short)(m.Data>>16)>0?1:-1});break;}
                    if(value!=0)Record(new MacroEvent{Kind=down?"Mouse down":"Mouse up",Value=value});
                }
            }
            return CallNextHookEx(mouseHook,code,message,data);
        }
        public static int UsageFromVirtualKey(int vk)
        {
            if(vk>=65&&vk<=90)return vk-65+4;if(vk>=49&&vk<=57)return vk-49+30;if(vk==48)return 39;if(vk>=112&&vk<=123)return vk-112+58;
            var map=new Dictionary<int,int>{{13,40},{27,41},{8,42},{9,43},{32,44},{189,45},{187,46},{219,47},{221,48},{220,49},{186,51},{222,52},{192,53},{188,54},{190,55},{191,56},{20,57},{44,70},{145,71},{19,72},{45,73},{36,74},{33,75},{46,76},{35,77},{34,78},{39,79},{37,80},{40,81},{38,82},{162,224},{160,225},{164,226},{91,227},{163,228},{161,229},{165,230},{92,231},{93,101}};
            int result;return map.TryGetValue(vk,out result)?result:0;
        }
        public static void SendText(string text)
        {
            foreach(char c in text){var down=new Input{Type=1,Key=new KeyInput{Scan=c,Flags=4}};var up=down;up.Key.Flags=6;if(SendInput(2,new[]{down,up},Marshal.SizeOf(typeof(Input)))!=2)throw new InvalidOperationException("Windows could not send text to the focused application.");}
        }
        public void Dispose(){StopRecording();source.RemoveHook(WindowMessage);}
    }
}
