//Copyright 2019-2020 Keysight Technologies
//
//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//
//http://www.apache.org/licenses/LICENSE-2.0
//
//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace OpenTap.Plugins.Iperf
{
    [Display("Iperf Measurement", Group: "Iperf", Description: "Configure and execute a test to be executed using the Iperf instrument")]
    public class IperfMeasurementStep : TestStep
    {
        #region Settings
        [Display("Iperf Instrument",Group:"Instrument")]
        public IperfInstrument IperfInstrument {get;set;}

        [Display(Name:"Iperf Server",Group:"Iperf Test Settings")]
        public string IperfServer { get; set; }
        [Display(Name: "Protocol", Group: "Iperf Test Settings")]
        public Protocol Protocol { get; set; }
        [Display(Name: "Direction", Group: "Iperf Test Settings")]
        public Direction Direction { get; set; }

        [Display(Name: "Target Bandwidth", Description: "bits per second - 0 Unlimited for TCP and 1Mbps for UDP", Group: "Iperf Test Settings")]
        public int Bandwidth { get; set; }
        [Display(Name: "Test Duration", Description:"in seconds", Group: "Iperf Test Settings")]
        public int TestDuration { get; set; }

        #endregion

        /// <summary>
        /// Creates a new instance of <see cref="IperfMeasurementStep">IperfMeasurementStep</see> class.
        /// </summary>
        public IperfMeasurementStep()
        {
            IperfServer = "192.168.11.111";
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
            StringBuilder sb = new StringBuilder($"iperf3 --client {IperfServer}");
            sb.Append($" --time {TestDuration}");
            if(Protocol == Protocol.udp)
            {
                sb.Append(" --udp");
            }
            if(Bandwidth != 0)
            {
                sb.Append($" --bandwidth {Bandwidth}");
            }
            if(Direction == Direction.uplink)
            {
                sb.Append(" --reverse");
            }

            // get output as json
            sb.Append(" --json");

            // Run the test
            var command = IperfInstrument.SshClient.RunCommand(sb.ToString());
            if(command.ExitStatus == 0)
            {
                // Print output in the log
                //foreach(var line in command.Result.Trim().Split('\n'))
                //{
                //    Log.Info(line);
                //}

                JObject result = JObject.Parse(command.Result);

                string executionId = (string)result["start"]["cookie"];

                List<string> keys = new List<string> {
                    "testexecid",
                    "protocol",
                    "duration",
                    "reverse",
                    "bits_per_second",
                    "retransmits_lost_packets",
                    "timesecs" };

                List<string> testexecid = new List<string>();
                List<string> protocol = new List<string>();
                List<string> duration = new List<string>();
                List<string> reverse = new List<string>();
                List<string> bits_per_second = new List<string>();
                List<string> retransmits_lost_packets = new List<string>();
                List<string> timestamp = new List<string>();

                testexecid.Add(executionId);
                protocol.Add((string)result["start"]["test_start"]["protocol"]);
                duration.Add((string)result["start"]["test_start"]["duration"]);
                reverse.Add((string)result["start"]["test_start"]["reverse"]);
                if(Protocol == Protocol.udp)
                {
                    retransmits_lost_packets.Add((string)result["end"]["sum"]["lost_packets"]);
                    bits_per_second.Add((string)result["end"]["sum"]["bits_per_second"]);
                    Log.Info($"Bitrate: {(string)result["end"]["sum"]["bits_per_second"]}");
                }
                else
                {
                    retransmits_lost_packets.Add((string)result["end"]["sum_sent"]["retransmits"]);
                    bits_per_second.Add((string)result["end"]["streams"][0]["receiver"]["bits_per_second"]);
                    Log.Info($"Bitrate: {(string)result["end"]["sum_received"]["bits_per_second"]}");
                }
                timestamp.Add((string)result["start"]["timestamp"]["timesecs"]);

                // Publish results for result listener
                Results.PublishTable(
                    executionId,
                    keys,
                    testexecid.ToArray(),
                    protocol.ToArray(),
                    duration.ToArray(),
                    reverse.ToArray(),
                    bits_per_second.ToArray(),
                    retransmits_lost_packets.ToArray(),
                    timestamp.ToArray() );

                UpgradeVerdict(Verdict.Pass);
            }
            else
            {
                Log.Warning(command.Error);
                UpgradeVerdict(Verdict.Fail);
            }
        }
    }
}
