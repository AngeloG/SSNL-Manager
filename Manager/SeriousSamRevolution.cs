using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace SSNL_Manager
{
  public class SeriousSamRevolution : Server
  {
    public long srv_iLastLogLocation;

    public SeriousSamRevolution(Dictionary<string, string> rowServer, Dictionary<string, string> rowGame)
    {
      srv_rowServer = rowServer;
      srv_rowGame = rowGame;

      // if the last used process ID is still valid
      Process proc = null;
      try {
        proc = Process.GetProcessById(int.Parse(rowServer["_PID"]));
      } catch { }
      if (proc != null && proc.ProcessName == srv_rowGame["Executable"].Replace(".exe", "")) {
        // take the existing process
        srv_bClosing = false;
        srv_proc = proc;
        srv_strRconPassword = rowServer["_Rcon"];
        srv_ctPlayers = int.Parse(rowServer["_Players"]);
        srv_iLastLogLocation = int.Parse(rowServer["_LogLocation"]);

        SSNL.Log(this, "Re-hooking");

        // start rcon connection
        if (!srv_bRestarting) {
          new Thread(new ThreadStart(WaitThread)).Start();
        }

        // save initial stats
        SaveStats();
      } else {
        // otherwise, just start a new process
        StartServer();
      }
    }

    public override string GetName()
    {
      return srv_rowServer["Name"];
    }

    public string GetStartMode()
    {
      switch ((string)srv_rowServer["Gamemode"]) {
        case "Cooperative":
          return "0";

        case "Scorematch":
          return "1";

        case "Deathmatch":
        case "Fragmatch":
          return "2";

        case "TeamDeathmatch":
          return "3";

        case "CaptureTheFlag":
          return "4";

        case "Survival":
          return "5";

        case "InstantKill":
          return "6";

        case "ControlZone":
        case "ControlZones":
          return "7";
      }
      return "2"; // fragmatch
    }

    public override void StartServer()
    {
      srv_bClosing = false;

      if (srv_proc != null) {
        SSNL.Log(this, "Warning: Killing still-existing server process with ID " + srv_proc.Id);
        srv_proc.Kill();
        srv_proc = null;
      }
      SSNL.Log(this, "Starting");
      SSNL.DB.Query("UPDATE `servers` SET `_LogLocation`=0 WHERE `ID`=" + srv_rowServer["ID"]);
      srv_iLastLogLocation = 0;

      // generate rcon password
      srv_strRconPassword = "";
      string strAllowedCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
      for (int i = 0; i < 16; i++) {
        srv_strRconPassword += strAllowedCharacters[SSNL.Rnd.Next(0, strAllowedCharacters.Length)];
      }

      // create dedicated scripts directory
      string strDirectory = GetWorkingDirectory() + "../Scripts/Dedicated/SSNL_" + srv_rowServer["ID"];
      if (!Directory.Exists(strDirectory)) {
        Directory.CreateDirectory(strDirectory);
      }

      // init.ini
      if (File.Exists(strDirectory + "/init.ini")) {
        File.Delete(strDirectory + "/init.ini");
      }
      using (StreamWriter writer = new StreamWriter(File.Create(strDirectory + "/init.ini"))) {
        writer.WriteLine("gam_strSessionName = \"" + GetName() + "\";");
        writer.WriteLine("net_iPort = " + GetPort() + ";");
        writer.WriteLine("gam_iStartMode = " + GetStartMode() + ";");
        writer.WriteLine("gam_iStartDifficulty = 1;"); // normal diff only for now :I
        writer.WriteLine("gam_ctMaxPlayers = " + GetMaxPlayers() + ";");
        writer.WriteLine("net_strAdminPassword = \"" + srv_strRconPassword + "\";");
        writer.WriteLine("gam_bWaitAllPlayers = 0;");
        writer.WriteLine("gam_iCredits = 0;");
        writer.WriteLine("gam_bWeaponsStay = 0;");
        writer.WriteLine("gam_iTimeLimit = 15;");
        writer.WriteLine("gam_iFragLimit = 20;");
        writer.WriteLine("ded_tmTimeout = 5;");
        writer.WriteLine("ser_bWaitFirstPlayer = 1;");

        /*string strBans = "";

        writer.WriteLine("ser_strBanList = \"" + strBans + "\"");
        writer.WriteLine("dofile \"SSNL/Global.lua\"");*/
      }

      // 1_begin.ini
      if (File.Exists(strDirectory + "/1_begin.ini")) {
        File.Delete(strDirectory + "/1_begin.ini");
      }
      using (StreamWriter writer = new StreamWriter(File.Create(strDirectory + "/1_begin.ini"))) {
        writer.WriteLine("ded_strLevel = \"" + GetFirstLevel() + "\";");
      }

      // 1_end.ini
      if (File.Exists(strDirectory + "/1_end.ini")) {
        File.Delete(strDirectory + "/1_end.ini");
      }
      using (StreamWriter writer = new StreamWriter(File.Create(strDirectory + "/1_end.ini"))) {
        writer.WriteLine("Say(\"Level finished.\");");
      }

      // start process
      srv_proc = Process.Start(new ProcessStartInfo() {
        WorkingDirectory = srv_rowGame["WorkingDirectory"],
        FileName = srv_rowGame["Executable"],
        Arguments = "SSNL_" + srv_rowServer["ID"],
        WindowStyle = ProcessWindowStyle.Hidden,
      });

      // start rcon connection
      if (!srv_bRestarting) {
        new Thread(new ThreadStart(WaitThread)).Start();
      }

      // save initial stats
      SaveStats();
    }

    public override void StopServer()
    {

    }

    public override void AdminAction(string strType, string strSteamID)
    {
      Console.WriteLine("NOT IMPLEMENTED ADMIN ACTION FOR REV");
    }

    public override void OnSecond()
    {
      using (StreamReader reader = new StreamReader(File.Open(GetWorkingDirectory() + "../Dedicated_SSNL_" + srv_rowServer["ID"] + ".log", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))) {
        reader.BaseStream.Seek(srv_iLastLogLocation, SeekOrigin.Begin);

        while (!reader.EndOfStream) {
          string strLine = reader.ReadLine();
          if (strLine.Length > 0) {
            if (strLine[0] == '<') {
              XMLTag tag = SimpleXMLReader.Parse(strLine);

              if (tag.Name == "server_running") {
                SSNL.Log(this, "Running.");
              }
            }
          }
        }

        if (srv_iLastLogLocation != reader.BaseStream.Position) {
          srv_iLastLogLocation = reader.BaseStream.Position;
          SSNL.DB.Query("UPDATE `servers` SET `_LogLocation`=" + srv_iLastLogLocation + " WHERE `ID`=" + srv_rowServer["ID"]);
        }
      }
    }
  }
}
