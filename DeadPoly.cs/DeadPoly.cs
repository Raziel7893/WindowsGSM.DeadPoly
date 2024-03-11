﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using WindowsGSM.GameServer.Engine;
using System.IO;
using System.Text;
using System.Linq;

namespace WindowsGSM.Plugins
{
    public class DeadPoly : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.DeadPoly", // WindowsGSM.XXXX
            author = "raziel7893",
            description = "WindowsGSM plugin for supporting DeadPoly Dedicated Server",
            version = "1.0.0",
            url = "https://github.com/Raziel7893/WindowsGSM.DeadPoly", // Github repository link (Best practice) TODO
            color = "#34FFeb" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "2208380"; // Game server appId Steam

        // - Standard Constructor and properties
        public DeadPoly(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error, Notice;


        // - Game server Fixed variables
        //public override string StartPath => "DeadPolyServer.exe"; // Game server start path
        public override string StartPath => "DeadPoly\\Binaries\\Win64\\DeadPolyServer.exe";
        public string FullName = "DeadPoly Dedicated Server"; // Game server FullName
        public string ConfigFile = "DeadPoly\\Saved\\Config\\WindowsServer\\Game.ini"; // Game server FullName
        public string ConfigTemplate = "DeadPoly\\Saved\\1 RENAME Config\\WindowsServer\\Game.ini"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation

        // - Game server default values
        public string Port = "7783"; // Default port

        public string Additional = " -batchmode -nographics -log -nosteam"; // Additional server start parameter

        // TODO: Following options are not supported yet, as ther is no documentation of available options
        public string Maxplayers = "16"; // Default maxplayers        
        public string QueryPort = "7784"; // Default query port. This is the port specified in the Server Manager in the client UI to establish a server connection.
        // TODO: Unsupported option
        public string Defaultmap = "Dedicated"; // Default map name
        // TODO: Undisclosed method
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        // - Create a default cfg or changes the config for the game server after installation
        public async void CreateServerCFG()
        {
            // Specify the file path
            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, ConfigFile);
            string templatePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, ConfigTemplate);

            var srcFile = File.Exists(configPath) ? configPath : templatePath;

            if(!File.Exists(srcFile))
            {
                Error = $"Neither Configfile({configPath}) nor Templatefile{templatePath} could be loaded. not possible to create or modify config";
                return;
            }
            var content = File.ReadAllLines(srcFile);
            StringBuilder sb = new StringBuilder();
            foreach (var line in content)
            {
                if (line.StartsWith("ServerName"))
                {
                    sb.Append($"ServerName={serverData.ServerName}");
                }
                else if (line.StartsWith("PlayerSlots"))
                {
                    sb.Append($"PlayerSlots={serverData.ServerMaxPlayer}");
                }
                else
                {
                    sb.Append(line);
                }
            }
            if (!content.Contains("ServerName="))
                sb.Append($"ServerName={serverData.ServerName}");
            if (!content.Contains("PlayerSlots="))
                sb.Append($"PlayerSlots={serverData.ServerMaxPlayer}");

            // Write the JSON content to the file
            File.WriteAllText(configPath, sb.ToString());
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string shipExePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} not found ({shipExePath})";
                return null;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append($"?ServerName={serverData.ServerName}?Port={serverData.ServerPort}?QueryPort={serverData.ServerQueryPort}?MaxPlayers={serverData.ServerMaxPlayer}?PlayerSlots={serverData.ServerMaxPlayer}");

            if (serverData.ServerParam.StartsWith("-"))
                sb.Append($" {serverData.ServerParam}");
            else
                sb.Append($"{serverData.ServerParam}");

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    CreateNoWindow = true,  //wird komplett ignoriert?!?
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = sb.ToString(),
                    WindowStyle = ProcessWindowStyle.Hidden,  //wird komplett ignoriert?!?
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            if (AllowsEmbedConsole)
            {
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                var serverConsole = new ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
            }

            // Start Process
            try
            {
                p.Start();
                if (AllowsEmbedConsole)
                {
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                }
                return p;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null; // return null if fail to start
            }
        }

        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                Functions.ServerConsole.SendWaitToMainWindow("^c");
                p.WaitForExit(10000);
                if (!p.HasExited)
                    p.Kill();
            });
        }
    }
}
