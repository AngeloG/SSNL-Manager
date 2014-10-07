using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace SSNL_Manager
{
  public abstract class Server
  {
    public Dictionary<string, string> srv_rowGame;
    public Dictionary<string, string> srv_rowServer;
    public Process srv_proc;

    public bool srv_bClosing = false;
    public bool srv_bRestarting = false;

    public string srv_strRconPassword;
    public int srv_ctPlayers;

    public string GetExecutablePath()
    {
      return srv_rowGame["Executable"];
    }

    public string GetWorkingDirectory()
    {
      return srv_rowGame["WorkingDirectory"];
    }

    public ConsoleColor GetConsoleColor()
    {
      switch (srv_rowGame["Color"]) {
        case "1": return ConsoleColor.Red;
        case "2": return ConsoleColor.Green;
        case "3": return ConsoleColor.Cyan;
        case "4": return ConsoleColor.Magenta;
      }
      return ConsoleColor.Gray;
    }

    public bool IsServerRunning()
    {
      return srv_proc != null && !srv_proc.HasExited;
    }

    public int GetPort()
    {
      return int.Parse(srv_rowServer["Port"]);
    }

    public int GetMaxPlayers()
    {
      return int.Parse(srv_rowServer["Maxplayers"]);
    }

    public virtual string GetFirstLevel()
    {
      return srv_rowServer["Level"];
    }

    public void SaveStats()
    {
      if (IsServerRunning()) {
        SSNL.DB.Query("UPDATE `servers` SET " +
          "_PID=" + srv_proc.Id + "," +
          "_Rcon='" + srv_strRconPassword + "'," +
          "_Players=" + srv_ctPlayers +
          " WHERE `ID`=" + srv_rowServer["ID"]);
      } else {
        SSNL.DB.Query("UPDATE `servers` SET _PID=0 WHERE `ID`=" + srv_rowServer["ID"]);
      }
    }

    long _ctSeconds = 0;
    int _ctLastPlayers = 0;

    public void WaitThread()
    {
      while (true) {
        Thread.Sleep(1000);

        _ctSeconds++;

        if (_ctLastPlayers > 0) {
          dynamic resActions = SSNL.DB.Query("SELECT * FROM `adminactions` WHERE `Server`=" + srv_rowServer["ID"] + " AND `Handled`=0 ORDER BY `ID` DESC");
          for (int i = 0; i < resActions.Length; i++) {
            dynamic action = resActions[i];
            AdminAction(action["Type"], action["SteamID"]);
            SSNL.DB.Query("UPDATE `adminactions` SET `Handled`=1 WHERE `ID`=" + action["ID"]);
          }
        }
        _ctLastPlayers = srv_ctPlayers;

        OnSecond();

        // if server closed unexpectedly
        if (!IsServerRunning() && srv_proc != null && !srv_bClosing) {
          SSNL.Log(this, "Server crashed");

          // restart it
          srv_proc = null;
          srv_bRestarting = true;
          StartServer();
        }

        // if a minute passed
        if (_ctSeconds % 60 == 0) {
          // save stats
          SaveStats();
        }
      }
    }

    public abstract string GetName();

    public abstract void StartServer();
    public abstract void StopServer();

    public abstract void AdminAction(string strType, string strSteamID);

    public virtual void OnSecond() { }
  }
}
