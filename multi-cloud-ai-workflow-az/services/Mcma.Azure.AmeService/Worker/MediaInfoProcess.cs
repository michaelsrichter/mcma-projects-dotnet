using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Mcma.Core.Context;
using Mcma.Core.Logging;

namespace Mcma.Azure.AmeService.Worker
{
    internal class MediaInfoProcess
    {
        private const string MediaInfoFolder = "exe";

        public static string HostRootDir { get; set; }

        public static async Task<MediaInfoProcess> RunAsync(IContextVariableProvider contextVariableProvider, ILogger logger, params string[] args)
        {
            var mediaInfoProcess = new MediaInfoProcess(contextVariableProvider, logger, args);
            await mediaInfoProcess.RunAsync();
            return mediaInfoProcess;
        }

        private MediaInfoProcess(IContextVariableProvider contextVariableProvider, ILogger logger, params string[] args)
        {
            ContextVariableProvider = contextVariableProvider;
            Logger = logger;
            ProcessStartInfo = 
                new ProcessStartInfo(Path.Combine(HostRootDir, MediaInfoFolder, "MediaInfo.exe"), string.Join(" ", args))
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
        }

        private IContextVariableProvider ContextVariableProvider { get; }

        private ILogger Logger { get; }

        private ProcessStartInfo ProcessStartInfo { get; }

        public string StdOut { get; private set; }

        public string StdErr { get; private set; }

        public async Task RunAsync()
        {
            using (var process = Process.Start(ProcessStartInfo))
            {
                Logger.Debug("MediaInfo process started. Reading stdout and stderr...");
                StdOut = await process.StandardOutput.ReadToEndAsync();
                StdErr = await process.StandardError.ReadToEndAsync();

                Logger.Debug("Waiting for MediaInfo process to exit...");
                process.WaitForExit();
                Logger.Debug($"MediaInfo process exited with code {process.ExitCode}.");

                if (process.ExitCode != 0)
                    throw new Exception($"MediaInfo process exited with code {process.ExitCode}:\r\nStdOut:\r\n{StdOut}StdErr:\r\n{StdErr}");
            }
        }
    }
}