using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32.SafeHandles;

namespace LamAngStudio
{
    public class DeviceInfo
    {
        public string Path="",Product="",Manufacturer="",ContainerId="";
        public int Vendor,ProductId,Version,UsagePage,Usage,FeatureLength,InputLength;
        public override string ToString() { return "Lam-Ang Lite  ·  "+Vendor.ToString("X4")+":"+ProductId.ToString("X4"); }
    }
    public static class HidDevice
    {
        [StructLayout(LayoutKind.Sequential)]struct InterfaceData{public int Size;public Guid Guid;public uint Flags;public IntPtr Reserved;}
        [StructLayout(LayoutKind.Sequential)]struct DeviceDataInfo{public int Size;public Guid ClassGuid;public uint DevInst;public IntPtr Reserved;}
        [StructLayout(LayoutKind.Sequential)]struct PropertyKey{public Guid Guid;public uint Id;}
        [StructLayout(LayoutKind.Sequential)]struct Attributes{public int Size;public ushort Vendor,Product,Version;}
        [StructLayout(LayoutKind.Sequential)]struct Caps
        { public ushort Usage,UsagePage,InputLength,OutputLength,FeatureLength;[MarshalAs(UnmanagedType.ByValArray,SizeConst=17)]public ushort[] Reserved;[MarshalAs(UnmanagedType.ByValArray,SizeConst=10)]public ushort[] Other; }
        [DllImport("hid.dll")]static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr SetupDiGetClassDevsW(ref Guid guid,string enumerator,IntPtr window,uint flags);
        [DllImport("setupapi.dll",SetLastError=true)]static extern bool SetupDiEnumDeviceInterfaces(IntPtr set,IntPtr info,ref Guid guid,uint index,ref InterfaceData data);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr set,ref InterfaceData data,IntPtr detail,int size,out int required,IntPtr info);
        [DllImport("setupapi.dll")]static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetupDiOpenDeviceInterfaceW(IntPtr set,string path,uint flags,ref InterfaceData data);
        [DllImport("setupapi.dll",EntryPoint="SetupDiGetDeviceInterfaceDetailW",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetDetailInfo(IntPtr set,ref InterfaceData data,IntPtr detail,int size,out int required,ref DeviceDataInfo info);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetupDiGetDevicePropertyW(IntPtr set,ref DeviceDataInfo info,ref PropertyKey key,out uint type,byte[] buffer,uint size,out uint required,uint flags);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFileW(string path,uint access,uint sharing,IntPtr security,uint creation,uint flags,IntPtr template);
        [DllImport("hid.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.U1)]static extern bool HidD_GetAttributes(SafeFileHandle handle,ref Attributes data);
        [DllImport("hid.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.U1)]static extern bool HidD_GetPreparsedData(SafeFileHandle handle,out IntPtr data);
        [DllImport("hid.dll")][return:MarshalAs(UnmanagedType.U1)]static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")]static extern int HidP_GetCaps(IntPtr data,out Caps caps);
        [DllImport("hid.dll",CharSet=CharSet.Unicode)][return:MarshalAs(UnmanagedType.U1)]static extern bool HidD_GetProductString(SafeFileHandle handle,StringBuilder buffer,int bytes);
        [DllImport("hid.dll",CharSet=CharSet.Unicode)][return:MarshalAs(UnmanagedType.U1)]static extern bool HidD_GetManufacturerString(SafeFileHandle handle,StringBuilder buffer,int bytes);
        [DllImport("hid.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.U1)]static extern bool HidD_SetFeature(SafeFileHandle handle,byte[] report,int bytes);
        [DllImport("hid.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.U1)]static extern bool HidD_GetFeature(SafeFileHandle handle,[In,Out]byte[] report,int bytes);
        public static bool Matches(DeviceInfo d)
        {return d!=null&&d.Vendor==0x0c45&&d.ProductId==0x8006&&d.Path!=null&&d.Path.IndexOf("&mi_00#",StringComparison.OrdinalIgnoreCase)>=0&&d.UsagePage==1&&d.Usage==6&&d.FeatureLength==65&&d.InputLength==9;}
        public static bool SamePhysicalDevice(string expected,string source){Guid a,b;return Guid.TryParse(expected,out a)&&Guid.TryParse(source,out b)&&a!=Guid.Empty&&a==b;}
        public static string IdentityForPath(string path)
        {
            Guid guid;HidD_GetHidGuid(out guid);IntPtr set=SetupDiGetClassDevsW(ref guid,null,IntPtr.Zero,0x12);if(set==new IntPtr(-1))return "";
            try
            {
                var data=new InterfaceData{Size=Marshal.SizeOf(typeof(InterfaceData))};if(!SetupDiOpenDeviceInterfaceW(set,path,0,ref data))return "";
                int required;SetupDiGetDeviceInterfaceDetailW(set,ref data,IntPtr.Zero,0,out required,IntPtr.Zero);if(required<8)return "";IntPtr detail=Marshal.AllocHGlobal(required);
                try{Marshal.WriteInt32(detail,IntPtr.Size==8?8:6);var info=new DeviceDataInfo{Size=Marshal.SizeOf(typeof(DeviceDataInfo))};if(!GetDetailInfo(set,ref data,detail,required,out required,ref info))return "";var key=new PropertyKey{Guid=new Guid("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"),Id=2};var buffer=new byte[16];uint type,bytes;if(!SetupDiGetDevicePropertyW(set,ref info,ref key,out type,buffer,16,out bytes,0)||type!=13||bytes!=16)return "";Guid identity=new Guid(buffer);return identity==Guid.Empty?"":identity.ToString("D");}finally{Marshal.FreeHGlobal(detail);}
            }
            finally{SetupDiDestroyDeviceInfoList(set);}
        }
        static SafeFileHandle Open(string path)
        {var h=CreateFileW(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero);if(h.IsInvalid){int error=Marshal.GetLastWin32Error();h.Dispose();throw new Win32Exception(error,"Cannot open keyboard: "+new Win32Exception(error).Message);}return h;}
        public static List<DeviceInfo> Discover()
        {
            var result=new List<DeviceInfo>();Guid guid;HidD_GetHidGuid(out guid);IntPtr set=SetupDiGetClassDevsW(ref guid,null,IntPtr.Zero,0x12);
            if(set==new IntPtr(-1))throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                for(uint index=0;;index++)
                {
                    var data=new InterfaceData{Size=Marshal.SizeOf(typeof(InterfaceData))};
                    if(!SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref guid,index,ref data)){int error=Marshal.GetLastWin32Error();if(error==259)break;throw new Win32Exception(error);}
                    int required;SetupDiGetDeviceInterfaceDetailW(set,ref data,IntPtr.Zero,0,out required,IntPtr.Zero);if(required<8)continue;
                    IntPtr detail=Marshal.AllocHGlobal(required);
                    try
                    {
                        Marshal.WriteInt32(detail,IntPtr.Size==8?8:6);
                        if(!SetupDiGetDeviceInterfaceDetailW(set,ref data,detail,required,out required,IntPtr.Zero))continue;
                        string path=Marshal.PtrToStringUni(IntPtr.Add(detail,4));
                        if(path.IndexOf("vid_0c45&pid_8006",StringComparison.OrdinalIgnoreCase)<0||path.IndexOf("&mi_00#",StringComparison.OrdinalIgnoreCase)<0)continue;
                        using(var handle=Open(path))
                        {
                            var attr=new Attributes{Size=Marshal.SizeOf(typeof(Attributes))};IntPtr prep;
                            if(!HidD_GetAttributes(handle,ref attr)||!HidD_GetPreparsedData(handle,out prep))continue;
                            Caps caps;try{if(HidP_GetCaps(prep,out caps)!=0x110000)continue;}finally{HidD_FreePreparsedData(prep);}
                            var info=new DeviceInfo{Path=path,Vendor=attr.Vendor,ProductId=attr.Product,Version=attr.Version,Usage=caps.Usage,UsagePage=caps.UsagePage,FeatureLength=caps.FeatureLength,InputLength=caps.InputLength,ContainerId=IdentityForPath(path)};
                            var text=new StringBuilder(256);if(HidD_GetProductString(handle,text,512))info.Product=text.ToString();text.Clear();if(HidD_GetManufacturerString(handle,text,512))info.Manufacturer=text.ToString();
                            if(Matches(info))result.Add(info);
                        }
                    }
                    finally{Marshal.FreeHGlobal(detail);}
                }
            }
            finally{SetupDiDestroyDeviceInfoList(set);}
            return result;
        }
        public static bool Acknowledged(byte[] response,byte[] payload)
        {return response!=null&&response.Length==65&&response[0]==0&&response[4]==1&&(payload[0]!=4||(response[1]==4&&response[2]==payload[1]));}
        public static byte[] Exchange(byte[] payload,Action<byte[]> write,Func<byte[]> read,Action pause)
        {
            byte[] response=null;
            for(int attempt=0;attempt<2;attempt++)
            {
                var report=new byte[65];Buffer.BlockCopy(payload,0,report,1,64);write(report);pause();response=read();
                if(Acknowledged(response,payload))return response;
                // This is the vendor's bounded recovery for an explicit rejection,
                // not a retry of a missing response or an entire partial transfer.
                bool rejected=response!=null&&response.Length==65&&response[0]==0&&response[4]==255&&(payload[0]!=4||(response[1]==4&&response[2]==payload[1]));
                if(attempt==0&&rejected)continue;
                break;
            }
            throw new InvalidOperationException("Keyboard did not acknowledge the command. Response: "+(response==null?"none":BitConverter.ToString(response)));
        }
        static byte[] Command(int command,int count)
        {var b=new byte[64];b[0]=4;b[1]=(byte)command;b[8]=(byte)count;return b;}
        class Transaction{public string Name;public byte[] Data;public int Start,Setup;public bool Finish;}
        public static WorkerResult Apply(WorkerRequest request)
        {
            ProfileStore.Validate(request.Profile);var p=request.Profile;var transactions=new List<Transaction>();
            var parts=(request.Parts??new string[0]).Distinct().ToArray();
            if(parts.Length==0||parts.Any(s=>!new[]{"lighting","mapping","macros","gaming"}.Contains(s)))throw new ArgumentException("Choose a valid settings section to apply.");
            // Serialize and validate the entire request before opening a device.
            if(parts.Contains("macros"))transactions.Add(new Transaction{Name="Macros",Data=Protocol.Macros(p),Start=0x19,Setup=0x15});
            if(parts.Contains("mapping"))transactions.Add(new Transaction{Name="Key mapping",Data=Protocol.Mapping(p),Start=0x18,Setup=0x11,Finish=true});
            if(parts.Contains("lighting"))transactions.Add(new Transaction{Name="Lighting",Data=Protocol.Lighting(p),Start=0x18,Setup=0x13,Finish=true});
            if(parts.Contains("gaming"))transactions.Add(new Transaction{Name="Gaming mode",Data=Protocol.Gaming(p),Start=0x18,Setup=0x17});
            var devices=Discover();var device=devices.FirstOrDefault(d=>d.Path==request.DevicePath);
            if(string.IsNullOrEmpty(request.DevicePath)&&devices.Count==1)device=devices[0];
            if(device==null)throw new ArgumentException(devices.Count==0?"Lam-Ang Lite is not connected. Connect it by USB and refresh.":"Select the connected keyboard again; its device path may have changed.");
            var result=new WorkerResult{Device=device,Devices=devices};
            using(var gate=new Mutex(false,@"Local\LamAngStudio-HID-0C45-8006"))
            {
                bool acquired=false;
                try
                {
                    try{acquired=gate.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
                    if(!acquired)throw new InvalidOperationException("Another Lam-Ang Studio instance is configuring the keyboard. Wait for it to finish.");
                    using(var handle=Open(device.Path))foreach(var t in transactions)
                    {
                        Send(handle,Command(t.Start,0),true,t.Name+" begin",result);
                        Send(handle,Command(t.Setup,t.Data.Length/64),true,t.Name+" transfer setup",result);
                        if(t.Name=="Macros")Thread.Sleep(30);
                        for(int i=0;i<t.Data.Length;i+=64){var block=new byte[64];Buffer.BlockCopy(t.Data,i,block,0,64);Send(handle,block,t.Data.Length==64,t.Name+" data "+(i/64+1),result);}
                        if(t.Name=="Macros")Thread.Sleep(30);
                        Send(handle,Command(2,0),true,t.Name+" commit",result);
                        if(t.Finish)Send(handle,Command(0xf0,0),true,t.Name+" finish",result);
                    }
                }
                catch(Exception e){result.Error=e.Message+(result.Writes>0?" A partial transfer may have occurred; reapply the profile.":"");return result;}
                finally{if(acquired)gate.ReleaseMutex();}
            }
            result.Success=true;return result;
        }
        static void Send(SafeFileHandle handle,byte[] payload,bool acknowledge,string stage,WorkerResult result)
        {
            Action<byte[]> write=delegate(byte[] report){if(!HidD_SetFeature(handle,report,65))throw new Win32Exception(Marshal.GetLastWin32Error(),stage+": HID write failed.");result.Writes++;};
            if(!acknowledge){var report=new byte[65];Buffer.BlockCopy(payload,0,report,1,64);write(report);Thread.Sleep(6);return;}
            try
            {
                var response=Exchange(payload,write,delegate{var read=new byte[65];if(!HidD_GetFeature(handle,read,65))throw new Win32Exception(Marshal.GetLastWin32Error(),stage+": acknowledgement read failed.");if(read[4]==255)result.Receipts.Add(stage+": rejected "+BitConverter.ToString(read));return read;},delegate{Thread.Sleep(6);});
                result.Acknowledgements++;result.Receipts.Add(stage+": "+BitConverter.ToString(response));
            }
            catch(InvalidOperationException e){throw new InvalidOperationException(stage+": "+e.Message,e);}
        }
    }
    public class WorkerRequest{public string Operation="discover",DevicePath="";public string[] Parts;public Profile Profile;}
    public class WorkerResult
    {public bool Success;public string Error="";public DeviceInfo Device;public List<DeviceInfo> Devices=new List<DeviceInfo>();public int Writes,Acknowledgements;public List<string> Receipts=new List<string>();public string LogPath="";}
    public static class DeviceWorker
    {
        public static readonly string DataFolder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LamAngStudio");
        public static Task<WorkerResult> Run(WorkerRequest request)
        {
            string input=new JavaScriptSerializer{MaxJsonLength=1024*1024}.Serialize(request);
            return Task.Run(delegate
            {
                WorkerResult result;
                var start=new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--worker"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
                using(var process=new Process{StartInfo=start})
                {
                    process.Start();var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
                    // Include a potentially blocked stdin pipe in the same deadline.
                    Task.Run(delegate{try{process.StandardInput.Write(input);process.StandardInput.Close();}catch(IOException){}catch(InvalidOperationException){}});
                    if(!process.WaitForExit(15000)){bool stopped=false;try{process.Kill();stopped=process.WaitForExit(3000);}catch(InvalidOperationException){stopped=process.HasExited;}result=new WorkerResult{Error="Keyboard I/O timed out. "+(stopped?"The isolated worker was stopped.":"Worker termination could not be confirmed.")+" A partial transfer may have occurred. Reconnect the keyboard and reapply the profile."};}
                    else
                    {
                        string text=output.Result;
                        try{result=new JavaScriptSerializer{MaxJsonLength=1024*1024}.Deserialize<WorkerResult>(text);if(result==null)throw new Exception();}
                        catch{result=new WorkerResult{Error="Device worker failed: "+error.Result+" "+text};}
                    }
                }
                if(request.Operation=="apply")
                {
                    try{string folder=Path.Combine(DataFolder,"Logs");Directory.CreateDirectory(folder);result.LogPath=Path.Combine(folder,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".json");File.WriteAllText(result.LogPath,new JavaScriptSerializer().Serialize(result),Encoding.UTF8);}catch(IOException){}catch(UnauthorizedAccessException){}
                }
                return result;
            });
        }
        public static int WorkerMain()
        {
            WorkerResult result;
            try
            {
                string json=Console.In.ReadToEnd();if(json.Length>1024*1024)throw new ArgumentException("Settings request too large.");
                var request=new JavaScriptSerializer{MaxJsonLength=1024*1024}.Deserialize<WorkerRequest>(json);
                if(request==null)throw new ArgumentException("Missing worker request.");
                if(request.Operation=="discover")result=new WorkerResult{Success=true,Devices=HidDevice.Discover()};
                else if(request.Operation=="apply")result=HidDevice.Apply(request);
                else throw new ArgumentException("Unknown worker operation.");
            }
            catch(Exception e){result=new WorkerResult{Error=e.Message};}
            Console.Write(new JavaScriptSerializer{MaxJsonLength=1024*1024}.Serialize(result));return result.Success?0:1;
        }
    }
}
