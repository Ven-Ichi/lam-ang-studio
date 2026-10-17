using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using LamAngStudio;

class PayloadFixture { public int PayloadLength {get;set;} public string PayloadHex {get;set;} }
class ProtocolChecks
{
    static readonly string Root=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".."));
    static int count;
    static void Check(bool condition,string name) { if(!condition)throw new Exception("FAIL: "+name);count++; }
    static byte[] Hex(string text) { return Enumerable.Range(0,text.Length/2).Select(i=>Convert.ToByte(text.Substring(i*2,2),16)).ToArray(); }
    static byte[] Fixture(string name,int size)
    {
        var fixture=new JavaScriptSerializer().Deserialize<PayloadFixture>(File.ReadAllText(Path.Combine(Root,"tests","fixtures",name+".json")));
        var bytes=Hex(fixture.PayloadHex);int expected=size>65?size-64:64;
        if(fixture.PayloadLength!=expected||bytes.Length!=expected)throw new Exception("Invalid fixture length: "+name);
        return bytes;
    }
    static void Reject(Action action,string name) { bool threw=false;try{action();}catch(ArgumentException){threw=true;}Check(threw,name); }
    static int Main()
    {
        try
        {
            var p=new Profile();
            for(int effect=1;effect<=20;effect++)
            {
                p.Effect=effect;var s=p.Lights[effect-1];s.Red=17;s.Green=34;s.Blue=51;s.FullColor=false;s.Brightness=9;s.Speed=4;s.Direction=effect==10?3:new[]{11,16,18}.Contains(effect)?1:0;
                Check(Protocol.Lighting(p).SequenceEqual(Fixture("effect-"+effect,1216)),"lighting matches blocked vendor capture, effect "+effect);
            }
            p.Colors.Add(new KeyColor{Usage=57,Red=17,Green=34,Blue=51});
            Check(Protocol.Lighting(p).SequenceEqual(Fixture("custom-caps-color",1216)),"custom Caps LED uses recovered matrix index and RGB order");
            Check(DeviceData.Keys.Count==87&&DeviceData.Keys.Select(k=>k.Usage).Distinct().Count()==87,"87 distinct physical keys");
            Check(DeviceData.Indices[57]==55&&DeviceData.Indices[41]==1,"Caps and Esc matrix indices");
            Check(DeviceData.Indices.SequenceEqual(new JavaScriptSerializer().Deserialize<int[]>(File.ReadAllText(Path.Combine(Root,"tests/fixtures/indices.json")))),"complete matrix lookup matches original runtime function");
            p=new Profile();p.Assignments.Add(new Assignment{Usage=57,Kind="Disabled"});
            Check(Protocol.Mapping(p).SequenceEqual(Fixture("mapping-type-1",704)),"disabled key packet matches vendor");
            p.Assignments[0]=new Assignment{Usage=57,Kind="Keyboard",Value=41,Modifiers=59};
            Check(Protocol.Mapping(p).SequenceEqual(Fixture("mapping-type-2",704)),"key usage and modifier bytes match vendor");
            p.Assignments[0]=new Assignment{Usage=57,Kind="Chord",Chord=new[]{41,58,59}};
            Check(Protocol.Mapping(p).SequenceEqual(Fixture("mapping-type-12",704)),"three-key chord matches vendor");
            p=new Profile();p.Macros.Add(new Macro{Name="Fixture macro",Playback="Repeat",Repeats=3});
            p.Macros[0].Events.AddRange(new[]{new MacroEvent{Kind="Delay",Value=100},new MacroEvent{Kind="Key down",Value=4},new MacroEvent{Kind="Key up",Value=4},new MacroEvent{Kind="Mouse down",Value=1},new MacroEvent{Kind="Mouse up",Value=1}});
            Check(Protocol.Macros(p).SequenceEqual(Fixture("macro-events",576)),"macro header, implicit delays, event tags and trailer match vendor");
            p.Assignments.Add(new Assignment{Usage=57,Kind="Macro",Value=0});
            Check(Protocol.Mapping(p).SequenceEqual(Fixture("mapping-type-3",704)),"macro binding slot and repeat flags match vendor");
            p=new Profile();p.GamingEnabled=true;p.LockWindows=true;p.LockAltTab=true;p.LockAltF4=true;
            var game=Protocol.Gaming(p);Check(game.Length==64&&game[0]==10&&game[1]==1&&game[2]==1&&game[3]==1&&game[4]==1&&game[62]==170&&game[63]==85,"gaming flags and trailer follow original handler");
            p.Effect=21;Reject(()=>Protocol.Lighting(p),"invalid effect rejected before serialization");
            p=new Profile();p.Assignments.Add(new Assignment{Usage=999,Kind="Keyboard",Value=41});Reject(()=>Protocol.Mapping(p),"invalid physical key rejected");
            p=new Profile();p.Lights[0].Red=256;Reject(()=>Protocol.Lighting(p),"invalid RGB rejected");
            Reject(()=>ProfileStore.Decode("{\"Version\":99}"),"unknown profile version rejected");
            Reject(()=>ProfileStore.Decode("{}"),"omitted profile sections cannot silently become factory defaults");
            Reject(()=>ProfileStore.Decode("{\"Lights\":null}"),"missing light data rejected");
            var device=new DeviceInfo{Vendor=0x0c45,ProductId=0x8006,Path="hid#vid_0c45&pid_8006&mi_00#test",UsagePage=1,Usage=6,FeatureLength=65,InputLength=9};
            Check(HidDevice.Matches(device),"matching keyboard control collection accepted");
            device.ProductId=0x8007;Check(!HidDevice.Matches(device),"different product rejected");device.ProductId=0x8006;
            device.Path="hid#vid_0c45&pid_8006&mi_01#test";Check(!HidDevice.Matches(device),"different collection rejected");device.Path="hid#vid_0c45&pid_8006&mi_00#test";
            device.FeatureLength=64;Check(!HidDevice.Matches(device),"wrong report length rejected");
            var recording=new Macro();recording.Events.AddRange(new[]{new MacroEvent{Kind="Key down",Value=4},new MacroEvent{Kind="Mouse down",Value=1}});MacroEditing.FinishRecording(recording);
            Check(recording.Events.Any(e=>e.Kind=="Key up"&&e.Value==4)&&recording.Events.Any(e=>e.Kind=="Mouse up"&&e.Value==1),"stopping a recording releases held keyboard and mouse buttons in the macro");
            var full=new Macro();for(int i=0;i<599;i++)full.Events.Add(new MacroEvent{Kind="Delay",Value=1});full.Events.Add(new MacroEvent{Kind="Key down",Value=4});Check(!MacroEditing.CanStartRecording(full),"full macro cannot start a recording that needs release space");Check(!MacroEditing.TryAppendRecording(full,new MacroEvent{Kind="Key down",Value=5})&&full.Events.Count==600,"rejected recording event preserves the valid existing macro");
            var room=new Macro();room.Events.Add(new MacroEvent{Kind="Key down",Value=4});Check(MacroEditing.CanStartRecording(room),"normal recording can start with release room");Check(MacroEditing.TryAppendRecording(room,new MacroEvent{Kind="Key up",Value=4}),"recorded release is accepted");
            Check(HidDevice.SamePhysicalDevice("11111111-1111-1111-1111-111111111111","11111111-1111-1111-1111-111111111111"),"same physical container accepted");Check(!HidDevice.SamePhysicalDevice("11111111-1111-1111-1111-111111111111","22222222-2222-2222-2222-222222222222"),"another identical keyboard's container rejected");Check(!HidDevice.SamePhysicalDevice("",""),"missing container identity never dispatches actions");
            var state=new DeviceState();p=new Profile();state.Remember(p,new[]{"lighting"});p.Lights[0].Brightness=8;
            Check(state.ChangedParts(p).SequenceEqual(new[]{"lighting"}),"reverting a draft can still differ from acknowledged device state");p.Lights[0].Brightness=15;
            Check(state.ChangedParts(p).Length==0,"matching acknowledged bytes have no pending changes");
            var ack=new byte[65];ack[1]=4;ack[2]=24;ack[4]=1;var command=new byte[64];command[0]=4;command[1]=24;Check(HidDevice.Acknowledged(ack,command),"real acknowledgement shape accepted");ack[2]=2;Check(!HidDevice.Acknowledged(ack,command),"stale acknowledgement for another command rejected");Check(!HidDevice.Acknowledged(new byte[65],command),"zero-filled read never treated as an acknowledgement");
            int writes=0,reads=0;var negative=new byte[65];negative[1]=4;negative[2]=24;negative[4]=255;var positive=(byte[])negative.Clone();positive[4]=1;
            var recovered=HidDevice.Exchange(command,r=>{writes++;Check(r.Length==65&&r[0]==0&&r[1]==4&&r[2]==24,"retry uses the same framed configuration command");},()=>++reads==1?negative:positive,()=>{});
            Check(writes==2&&HidDevice.Acknowledged(recovered,command),"explicit matching negative ACK is retried once and then accepted");
            writes=0;bool failed=false;try{HidDevice.Exchange(command,r=>writes++,()=>new byte[65],()=>{});}catch(InvalidOperationException){failed=true;}Check(failed&&writes==1,"missing or zero acknowledgement is not blindly retried");
            writes=0;failed=false;try{HidDevice.Exchange(command,r=>writes++,()=>negative,()=>{});}catch(InvalidOperationException){failed=true;}Check(failed&&writes==2,"repeated negative acknowledgements stop after one retry");
            string path=Path.Combine(Path.GetTempPath(),"lamang-check-"+Guid.NewGuid().ToString("N")+".json");
            try{p=new Profile();p.Name="Test \u2605";ProfileStore.Save(path,p);Check(ProfileStore.Load(path).Name==p.Name,"Unicode profile file round trip");p.Name="Updated";ProfileStore.Save(path,p);Check(ProfileStore.Load(path).Name=="Updated","atomic replacement preserves usable profile");string previous=File.ReadAllText(path);var big=new Profile();foreach(var key in DeviceData.Keys.Take(36))big.Assignments.Add(new Assignment{Usage=key.Usage,Kind="Text",Target=new string('\u754c',10000)});Reject(()=>ProfileStore.Save(path,big),"UTF-8 byte limit enforced before saving Unicode-heavy profile");Check(File.ReadAllText(path)==previous,"oversized save preserves the previously usable profile");File.WriteAllText(path,new string(' ',1024*1024+1));Reject(()=>ProfileStore.Load(path),"oversized profile file rejected");}finally{if(File.Exists(path))File.Delete(path);if(File.Exists(path+".backup"))File.Delete(path+".backup");}
            Console.WriteLine("PASS: "+count+" protocol and profile checks");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}
    }
}
