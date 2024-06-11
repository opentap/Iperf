using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using OpenTap.Plugins.Ssh;

namespace OpenTap.Plugins.Iperf;

[Display("iPerf Measurement", Group: "iPerf", Description: "Configure and execute a iPerf client locally or remote")]
public class IperfMeasurementStep : TestStep
{
    [Display("Use Remote", Description: "Use remote iPerf over SSH", Group: "SSH")]
    public bool Remote { get; set; }

    [EnabledIf(nameof(Remote), HideIfDisabled = true)]
    [Display("SSH Resource", Group: "SSH")]
    public SshInstrument IperfInstrument { get; set; }

    [Display(Name: "Iperf Server", Group: "iPerf Test Settings")]
    public string IperfServer { get; set; }

    [Display(Name: "Port", Group: "iPerf Test Settings")]
    public int Port { get; set; }

    [Display(Name: "Protocol", Group: "iPerf Test Settings")]
    public Protocol Protocol { get; set; }

    [Display(Name: "Direction", Group: "iPerf Test Settings")]
    public Direction Direction { get; set; }

    [Display(Name: "Target Bandwidth", Description: "bits per second - 0 Unlimited for TCP and 1Mbps for UDP", Group: "iPerf Test Settings")]
    public int Bandwidth { get; set; }

    [Display(Name: "Test Duration", Description: "in seconds", Group: "iPerf Test Settings")]
    public int TestDuration { get; set; }
    
    

    /// <summary>
    /// Creates a new instance of <see cref="IperfMeasurementStep">IperfMeasurementStep</see> class.
    /// </summary>
    public IperfMeasurementStep()
    {
        IperfServer = "iperf.par2.as49434.net";
        Port = 5201;
        Protocol = Protocol.tcp;
        Direction = Direction.downlink;
        Bandwidth = 0;
        TestDuration = 10;
    }

    /// <summary>
    /// Run method is called when the step gets executed.
    /// </summary>
    public override void Run()
    {
        // Create iperf config
        StringBuilder command = new StringBuilder($"--client {IperfServer}");
        command.Append($" --port {Port}");
        command.Append($" --time {TestDuration}");
        if (Protocol == Protocol.udp)
            command.Append(" --udp");
        if (Bandwidth != 0)
            command.Append($" --bandwidth {Bandwidth}");
        if (Direction == Direction.uplink)
            command.Append(" --reverse");
        // get output as json
        command.Append(" --json");

        bool success = false;
        string result = "";
        if (Remote)
        {
        
            // Run remote
            var commandResult = IperfInstrument.SshClient.RunCommand("iperf3 " + command.ToString());
            if (commandResult.ExitStatus == 0)
            {
                success = true;
                result = commandResult.Result;
            }
            else
                result = commandResult.Error;
        }
        else
        {
            // Run locally
            var process = new LocalIperfHelper();
            (success, result) = process.RunCommand(command.ToString(), false);
        }
        
        if (success)
        {
            ParseIperfOutput(result);
            UpgradeVerdict(Verdict.Pass);
        }
        else
        {
            Log.Debug(result);

            JObject json = JObject.Parse(result);
            Log.Error(json["error"]?.Value<string>() ?? "Something went wrong calling Iperf3");
            
            UpgradeVerdict(Verdict.Fail);
        }
    }

    private void ParseIperfOutput(string result)
    {
        JObject json = JObject.Parse(result);
        string executionId = (string)json["start"]["cookie"];
        List<string> keys = new List<string>
        {
            "testexecid",
            "protocol",
            "duration",
            "reverse",
            "bits_per_second",
            "retransmits_lost_packets",
            "timesecs"
        };

        List<string> testexecid = new List<string>();
        List<string> protocol = new List<string>();
        List<string> duration = new List<string>();
        List<string> reverse = new List<string>();
        List<string> bits_per_second = new List<string>();
        List<string> retransmits_lost_packets = new List<string>();
        List<string> timestamp = new List<string>();

        testexecid.Add(executionId);
        protocol.Add((string)json["start"]["test_start"]["protocol"]);
        duration.Add((string)json["start"]["test_start"]["duration"]);
        reverse.Add((string)json["start"]["test_start"]["reverse"]);
        if (Protocol == Protocol.udp)
        {
            retransmits_lost_packets.Add((string)json["end"]["sum"]["lost_packets"]);
            bits_per_second.Add((string)json["end"]["sum"]["bits_per_second"]);
        }
        else
        {
            retransmits_lost_packets.Add((string)json["end"]["sum_sent"]["retransmits"]);
            bits_per_second.Add((string)json["end"]["streams"][0]["receiver"]["bits_per_second"]);
        }

        timestamp.Add((string)json["start"]["timestamp"]["timesecs"]);

        
        var measurements = ((JArray) json["intervals"])?.Select(x => x["sum"]).Select(x =>
            new {
                Start = (double)(x["start"] ?? 0.0),
                Duration = (double)(x["seconds"] ?? 0.0),
                Bytes = (long)(x["bytes"] ?? 0),
                BitsPerSecond = (double) (x["bits_per_second"]?? 0.0),
                Retransmits = (double)(x["retransmits"] ?? 0)
            }).ToArray();
        if (measurements != null)
        {
            foreach (var meas in measurements)
            {
                Results.Publish("Perf", meas);
            }
        }
        var end = json["end"];
        var sent = end["sum_sent"];
        var received = end["sum_received"];
        if (sent != null)
        {
            var sentResult = new
            {
                MegaBytes = ((double) (sent["bytes"] ?? 0)) / (1024 * 1024),
                MBitsPerSecond = ((double) (sent["bits_per_second"] ?? 0)) / 1_000_000,
                Duration = (double) (sent["seconds"] ?? 0.0),
                Protocol = protocol.FirstOrDefault()
            };
            Results.Publish("Sent", sentResult);
        }

        if (received != null)
        {
            var receivedResult = new
            {
                MegaBytes = ((double) (received["bytes"] ?? 0)) / (1024 * 1024),
                MBitsPerSecond = ((double) (received["bits_per_second"] ?? 0)) / 1_000_000,
                Duration = (double) (received["seconds"] ?? 0.0),
                Protocol = protocol.FirstOrDefault()
            };
            Results.Publish("Received", receivedResult);
        }

        
        
        
        Log.Info($"Transfer: {format(json["end"]?["sum_sent"]?["bytes"], true)}   Bandwidth: {format(json["end"]?["sum_sent"]?["bits_per_second"], false)}  Direction: sender");
        Log.Info($"Transfer: {format(json["end"]?["sum_received"]?["bytes"], true)}   Bandwidth: {format(json["end"]?["sum_received"]?["bits_per_second"], false)}  Direction: receiver");
        
        
    }
    static string format(object input, bool bytes)
    {
        if (input == null)
            return "";
        if (double.TryParse(input.ToString(), out var value) == false)
            return "";

        if (bytes)
        {
            return value switch
            {
                > 1_073_741_824 => $"{value / 1_073_741_824:F} GBytes",
                > 1_048_576 => $"{value / 1_048_576:F} MBytes",
                > 1024 => $"{value / 1024:F} KBytes",
                _ => $"{value} Bytes"
            };    
        }
        else
        {
            return value switch
            {
                > 1_000_000_000 => $"{value / 1_000_000_000:F} GBits/sec",
                > 1_000_000 => $"{value / 1_000_000:F} MBits/sec",
                > 1000 => $"{value / 1000:F} KBits/sec",
                _ => $"{value} bits/sec"
            };    
        }
    }
}
