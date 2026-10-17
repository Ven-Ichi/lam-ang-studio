using System;
using System.IO;
using System.Web.Script.Serialization;
using LamAngStudio;

class DeviceCheck
{
    static int Main(string[] args)
    {
        if(args.Length>0&&args[0]=="--worker")return DeviceWorker.WorkerMain();
        try
        {
            var request=new WorkerRequest();
            if(args.Length>0&&args[0]=="--light")
            {
                var p=new Profile{Name="Live verification",Effect=int.Parse(args[1])};
                var s=p.Lights[p.Effect-1];s.FullColor=false;s.Red=168;s.Green=199;s.Blue=250;s.Brightness=8;s.Speed=5;
                request.Operation="apply";request.Profile=p;request.Parts=new[]{"lighting"};
            }
            var result=DeviceWorker.Run(request).Result;
            string json=new JavaScriptSerializer().Serialize(result);Console.WriteLine(json);
            if(args.Length>2)File.WriteAllText(args[2],json);
            return result.Success?0:1;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
