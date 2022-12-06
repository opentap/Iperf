using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace OpenTap.Plugins.Iperf;

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

public class LocalIperfHelper
{
    private ManualResetEvent outputWaitHandle;
    private ManualResetEvent errorWaitHandle;
    private StringBuilder output;
    private bool printToLog;
    private TraceSource log = Log.CreateSource(nameof(LocalIperfHelper));
    
    string getIperfLocation()
    {
        var location = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            location += RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "/Packages/Iperf/iperf-3.1.3-win64/iperf3.exe" : "/Packages/Iperf/iperf-3.1.3-win32/iperf3.exe";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            location += "/Packages/Iperf/MacOS/iperf3";
        else
            location = "iperf3";

        return location;
    }
    void OutputDataRecv(object sender, DataReceivedEventArgs e)
    {
        try
        {
            if (e.Data == null)
            {
                outputWaitHandle.Set();
            }
            else
            {
                if (printToLog)
                    log.Info(e.Data);
                
                lock(output)
                    output.AppendLine(e.Data);
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }
    void ErrorDataRecv(object sender, DataReceivedEventArgs e)
    {
        try
        {
            if (e.Data == null)
            {
                errorWaitHandle.Set();
            }
            else
            {
                if (printToLog)
                    log.Error(e.Data);
                
                lock(output)
                    output.AppendLine(e.Data);
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }
    
    public (bool success, string result) RunCommand(string command, bool printToLog)
    {
        // Setup local iperf process
        output = new StringBuilder();
        this.printToLog = printToLog;
        var app = getIperfLocation();
        var process = new Process
        {
            StartInfo =
            {
                FileName = app,
                Arguments = command,
                WorkingDirectory = Directory.GetCurrentDirectory(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            }
        };
        
        // Subscribe to abort token, and kill iperf
        TapThread.Current.AbortToken.Register(() =>
        {
            log.Debug("Stopping Iperf process");
            try
            {
                try
                {
                    process.StandardInput.Close();
                }
                catch
                {
                    // ignored
                }

                if (!process.WaitForExit(500))
                    process.Kill();
            }
            catch
            {
                // ignored
            }
        });
        
        // Start iperf
        using (outputWaitHandle = new ManualResetEvent(false))
        using (errorWaitHandle = new ManualResetEvent(false))
        using(process)
        {
            process.OutputDataReceived += OutputDataRecv;
            process.ErrorDataReceived += ErrorDataRecv;

            try
            {
                process.Start();
            }
            catch (Exception e)
            {
                log.Error(e);
                Console.WriteLine(e);
            }
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            if (process.WaitForExit(-1) &&
                outputWaitHandle.WaitOne(-1) &&
                errorWaitHandle.WaitOne(-1))
            {
                lock (output)
                    return (process.ExitCode == 0, output.ToString());
            }
            else
            {
                process.OutputDataReceived -= OutputDataRecv;
                process.ErrorDataReceived -= ErrorDataRecv;

                process.Kill();

                lock (output)
                    return (false, output.ToString());
            }
        }
    }
}