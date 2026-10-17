using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LamAngStudio;

class NativeUiChecks
{
    static int checks,exitCode=1;
    static readonly string Root=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".."));
    static T Get<T>(object o,string field){return(T)o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
    static void Invoke(object o,string method,params object[] args){try{o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    static void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);checks++;}
    static IEnumerable<DependencyObject> Tree(DependencyObject root){yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var node in Tree(child))yield return node;}
    static async Task Wait(Func<bool> ready,string name){for(int i=0;i<100;i++){if(ready())return;await Task.Delay(100);}throw new Exception("FAIL: timed out: "+name);}
    static void Snapshot(Window w,string name)
    {w.UpdateLayout();var image=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(w);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));Directory.CreateDirectory(Path.Combine(Root,"evidence"));using(var f=File.Create(Path.Combine(Root,"evidence",name)))encoder.Save(f);}
    [STAThread]static int Main(string[] args)
    {
        if(args.Length>0&&args[0]=="--worker")return DeviceWorker.WorkerMain();
        string sandbox=Path.Combine(Path.GetTempPath(),"LamAngUiCheck-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(sandbox);
        // Isolate real persistence in this test process, without a test-only production setting.
        typeof(DeviceWorker).GetField("DataFolder").SetValue(null,sandbox);
        var app=new Application();using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Theme.xaml"))app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(stream));
        var window=new MainWindow{WindowStartupLocation=WindowStartupLocation.Manual,Left=-3000,Top=-3000};
        window.Loaded+=async delegate
        {
            try
            {
                await Wait(()=>Get<ComboBox>(window,"devicePicker").Items.Count>0,"real device discovery in native UI");
                var tabs=Get<TabControl>(window,"tabs");Check(tabs.Items.Count==5,"five real native feature views");
                var effect=Get<ComboBox>(window,"effectPicker");Check(effect.Items.Count==20,"twenty native effect entries");
                for(int i=0;i<20;i++){effect.SelectedIndex=i;Check(Get<Slider>(window,"speed").IsEnabled!=(i==0||i==19),"native speed availability "+(i+1));}
                effect.SelectedIndex=9;var directions=Get<StackPanel>(window,"directionControls");Check(((WrapPanel)directions.Children[0]).Children.OfType<Button>().Select(b=>b.Content.ToString()).SequenceEqual(new[]{"Up","Down"}),"native Scrolling directions");
                effect.SelectedIndex=10;directions=Get<StackPanel>(window,"directionControls");Check(((WrapPanel)directions.Children[0]).Children.OfType<Button>().Select(b=>b.Content.ToString()).SequenceEqual(new[]{"Left","Right"}),"native Rolling directions");
                effect.SelectedIndex=0;
                tabs.SelectedIndex=1;Invoke(window,"ShowAssignment");Get<ComboBox>(window,"actionPicker").SelectedIndex=1;Invoke(window,"StageAssignment");
                var p=Get<Profile>(window,"profile");Check(p.Assignments[0].Kind=="Keyboard","native staging records a keyboard assignment");
                Get<ComboBox>(window,"actionPicker").SelectedIndex=7;
                Check(p.Assignments[0].Kind=="Keyboard","editing an unstaged category cannot mutate the active profile");
                p.Assignments[0]=new Assignment{Usage=57,Kind="Keyboard",Value=104};Invoke(window,"ShowAssignment");Check(((KeyDef)Get<ComboBox>(window,"destination").SelectedItem).Usage==104,"nonphysical accepted HID destination is preserved in the editor");
                string profileDir=Path.Combine(sandbox,"Profiles");ProfileStore.Save(Path.Combine(profileDir,"Alpha.json"),new Profile{Name="Alpha"});ProfileStore.Save(Path.Combine(profileDir,"Beta.json"),new Profile{Name="Beta"});p.Assignments[0]=new Assignment{Usage=57,Kind="Profile",Target="Beta"};Invoke(window,"ShowAssignment");Check(Get<ComboBox>(window,"destination").SelectedItem.ToString()=="Beta","stored profile target is not replaced by first list entry");
                p.Macros.Clear();foreach(string name in new[]{"A","B","C"}){var macro=new Macro{Name=name};macro.Events.Add(new MacroEvent{Kind="Key down",Value=4});macro.Events.Add(new MacroEvent{Kind="Key up",Value=4});p.Macros.Add(macro);}p.Assignments[0]=new Assignment{Usage=57,Kind="Macro",Value=1};Invoke(window,"ShowAssignment");Invoke(window,"RefreshMacroNames");Get<ComboBox>(window,"macroPicker").SelectedIndex=0;Invoke(window,"DeleteMacro");tabs.SelectedIndex=2;tabs.SelectedIndex=1;Check(Get<ComboBox>(window,"destination").SelectedItem.ToString()=="B"&&Get<ComboBox>(window,"destination").SelectedIndex==0,"mapping editor refreshes macro slots after deletion");
                Invoke(window,"Discard");tabs=Get<TabControl>(window,"tabs");tabs.SelectedIndex=0;
                Get<Slider>(window,"brightness").Value=7;var color=Get<TextBox>(window,"hex");color.Text="#bad";Get<Button>(window,"applyButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(Get<TextBlock>(window,"status").Text=="Could not apply settings","invalid native field blocks Apply");
                color.Text="#A8C7FA";
                var large=new Profile{Name="Editable large macro"};large.Lights[0].FullColor=false;large.Lights[0].Red=168;large.Lights[0].Green=199;large.Lights[0].Blue=250;large.Lights[0].Brightness=8;large.Macros.Add(new Macro());for(int i=0;i<400;i++)large.Macros[0].Events.Add(new MacroEvent{Kind=i%2==0?"Key down":"Key up",Value=4});
                string path=Path.Combine(sandbox,"large.json");ProfileStore.Save(path,large);Invoke(window,"LoadProfile",path);Check(Get<Profile>(window,"profile").Macros[0].Events.Count==400,"oversized-for-device macro remains editable after local load");
                var loaded=Get<Profile>(window,"profile").Macros[0];loaded.Events.Clear();for(int i=0;i<599;i++)loaded.Events.Add(new MacroEvent{Kind="Delay",Value=1});loaded.Events.Add(new MacroEvent{Kind="Key down",Value=4});bool blocked=false;try{Invoke(window,"ToggleRecording");}catch(ArgumentException){blocked=true;}Check(blocked&&!Get<HostActions>(window,"host").IsRecording&&loaded.Events.Count==600,"unsafe full recording is refused without attaching global hooks or altering work");
                Invoke(window,"Discard");tabs=Get<TabControl>(window,"tabs");tabs.SelectedIndex=0;var slider=Get<Slider>(window,"brightness");slider.Value=3;
                Get<Button>(window,"applyButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!Get<ComboBox>(window,"devicePicker").IsEnabled,"device switching disabled during actual Apply");tabs.SelectedIndex=4;var profiles=(TabItem)tabs.Items[4];Tree((DependencyObject)profiles.Content).OfType<Button>().First(b=>b.Content.ToString()=="New").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Wait(()=>!Get<bool>(window,"busy"),"real native Apply completes");
                Check(Get<TextBlock>(window,"status").Text=="Keyboard acknowledged settings","native Apply receives actual keyboard ACKs");
                Check(Get<Profile>(window,"profile").Name=="New profile"&&Get<HashSet<string>>(window,"pending").Count>0,"older Apply completion cannot clear new profile's pending settings");Invoke(window,"LoadProfile",path);Invoke(window,"Discard");tabs=Get<TabControl>(window,"tabs");tabs.SelectedIndex=0;
                Get<Button>(window,"applyButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Wait(()=>!Get<bool>(window,"busy"),"native brightness restore completes");Check(Get<TextBlock>(window,"status").Text=="Keyboard acknowledged settings","native restore receives actual keyboard ACKs");
                Snapshot(window,"native-lighting.png");tabs.SelectedIndex=2;Snapshot(window,"native-macros.png");window.Width=960;window.Height=720;window.UpdateLayout();tabs.SelectedIndex=0;Snapshot(window,"native-compact.png");
                Console.WriteLine("PASS: "+checks+" native UI checks, including real Apply and restore");exitCode=0;
            }
            catch(Exception e){Console.Error.WriteLine(e.ToString());Console.Error.WriteLine("Native status: "+Get<TextBlock>(window,"status").Text+" | "+Get<TextBlock>(window,"detail").Text);Console.Error.WriteLine("Test data: "+sandbox);Snapshot(window,"native-check-failure.png");}
            finally{window.Close();}
        };
        app.Run(window);return exitCode;
    }
}
