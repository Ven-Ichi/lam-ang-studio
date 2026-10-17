using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Web.Script.Serialization;

namespace LamAngStudio
{
    public static class Program
    {
        const string ResidentName=@"Local\LamAngStudio-Resident";
        [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr handle);
        [DllImport("user32.dll")]static extern bool ShowWindowAsync(IntPtr handle,int command);
        static bool ResidentRunning()
        {Mutex mutex;if(!Mutex.TryOpenExisting(ResidentName,out mutex))return false;using(mutex){try{if(!mutex.WaitOne(0))return true;mutex.ReleaseMutex();return false;}catch(AbandonedMutexException){mutex.ReleaseMutex();return false;}}}
        [STAThread]public static int Main(string[] args)
        {
            if(args.Length>0&&args[0]=="--worker")return DeviceWorker.WorkerMain();
            if(args.Length>0&&(args[0]=="--diagnose"||args[0]=="--apply"))
            {
                string report=args[0]=="--diagnose"&&args.Length==2?args[1]:args[0]=="--apply"&&args.Length==5?args[4]:null;
                if(args[0]=="--apply"&&report!=null){try{if(string.Equals(Path.GetFullPath(args[1]),Path.GetFullPath(report),StringComparison.OrdinalIgnoreCase))report=null;}catch(ArgumentException){report=null;}}
                try
                {
                    if(report==null)throw new ArgumentException("Specify a distinct output report path and the documented command arguments.");
                    var request=new WorkerRequest();string output;
                    if(args[0]=="--apply"){if(args.Length!=5)throw new ArgumentException("Usage: --apply profile.json lighting,mapping,macros,gaming device-path-or-auto report.json");request.Operation="apply";request.Profile=ProfileStore.Load(args[1]);request.Parts=args[2].Split(',');request.DevicePath=args[3]=="auto"?"":args[3];output=args[4];}
                    else{if(args.Length!=2)throw new ArgumentException("Usage: --diagnose report.json");output=args[1];}
                    if(request.Operation=="apply"&&request.Parts.Any(p=>p=="mapping"||p=="macros")&&ResidentRunning())throw new ArgumentException("Close the resident app before changing key maps or macros through the command line.");
                    var result=DeviceWorker.Run(request).Result;File.WriteAllText(output,new JavaScriptSerializer().Serialize(result));return result.Success?0:1;
                }
                catch(Exception e){if(report!=null){try{File.WriteAllText(report,new JavaScriptSerializer().Serialize(new WorkerResult{Error=e.Message}));}catch(Exception){}}Console.Error.WriteLine(e.Message);return 1;}
            }
            try
            {
                using(var resident=new Mutex(false,ResidentName))
                {
                    bool acquired=false;try{acquired=resident.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
                    if(!acquired){int current=Process.GetCurrentProcess().Id;foreach(var p in Process.GetProcessesByName("LamAngStudio")){using(p)if(p.Id!=current&&p.MainWindowHandle!=IntPtr.Zero){ShowWindowAsync(p.MainWindowHandle,9);SetForegroundWindow(p.MainWindowHandle);break;}}return 0;}
                    try
                    {
                        var app=new Application();
                        using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Theme.xaml"))app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(stream));
                        app.DispatcherUnhandledException+=delegate(object sender,System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e){MessageBox.Show(e.Exception.Message,"Lam-Ang Studio");e.Handled=true;};
                        app.Run(new MainWindow());return 0;
                    }
                    finally{resident.ReleaseMutex();}
                }
            }
            catch(Exception e){MessageBox.Show(e.ToString(),"Lam-Ang Studio could not start");return 1;}
        }
    }
}
