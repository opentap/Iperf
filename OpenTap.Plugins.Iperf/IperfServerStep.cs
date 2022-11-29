using System.Text;

namespace OpenTap.Plugins.Iperf;

[Display("iPerf Server", Group: "iPerf", Description: "Configure and execute a iPerf server")]
public class IperfServerStep : TestStep
{
    [Display("Port", Description: "Server port to listen on.")]
    public string Port { get; set; } = "5201";

    [Display("Bind", Description: "Bind to a specific interface.")]
    public string Bind { get; set; }
    
    [Display("One Off", Description: "Handle one client connection then exit.")]
    public bool OneOff { get; set; }

    public override void Run()
    {
        // Create iperf config
        StringBuilder command = new StringBuilder("-s");
        if (string.IsNullOrEmpty(Port) == false)
            command.Append($" --port {Port}");
        if (string.IsNullOrEmpty(Bind) == false)
            command.Append($" --bind {Bind}");
        if (OneOff)
            command.Append(" --one-off");
        
        var process = new LocalIperfHelper();
        var (success, result) = process.RunCommand(command.ToString());
        
        if (success)
            UpgradeVerdict(Verdict.Pass);
        else
        {
            Log.Error(result);
            UpgradeVerdict(Verdict.Fail);
        }
    }
}
