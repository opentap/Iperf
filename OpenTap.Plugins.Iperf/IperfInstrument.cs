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

// Inherit instrument from SshPlugin
using OpenTap.Plugins.Ssh;

namespace OpenTap.Plugins.Iperf
{
    public enum Protocol
    {
        tcp,
        udp
    }

    public enum Direction
    {
        uplink,
        downlink
    }

    [Display("Iperf Instrument", Group: "Iperf", Description: "Create an instrument to control a Iperf3 client")]
    public class IperfInstrument : SshInstrument
    {
        #region Settings
        // No properties to add - inheriting from SshInstrument
        #endregion

        /// <summary>
        /// Initializes a new instance of this Ssh Intrument class.
        /// </summary>
        public IperfInstrument()
        {
            Name = "Iperf Instrument";
        }

        /// <summary>
        /// Open procedure for the Iperf instrument.
        /// Print version of Iperf3.
        /// </summary>
        public override void Open()
        {

            base.Open();
            var command = this.SshClient.RunCommand("iperf3 --version");
            if(command.ExitStatus == 0)
            {
                foreach(var line in command.Result.Trim().Split('\n'))
                {
                    Log.Info(line);
                }
            }
            else
            {
                Log.Warning(command.Error);
            }
        }

        /// <summary>
        /// Close procedure for the Iperf instrument.
        /// </summary>
        public override void Close()
        {
            base.Close();
        }
    }
}
