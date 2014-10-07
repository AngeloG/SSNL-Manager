using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SSNL_Manager
{
  public class Player
  {
    public string ply_strName;

    public int ply_iKills;
    public int ply_iDeaths;

    public int ply_iWarnings;

    public void ResetStats()
    {
      ply_iKills = 0;
      ply_iDeaths = 0;
    }
  }

  public class SeriousEngine3 : Server
  {
    public StreamReader srv_reader;
    public StreamWriter srv_writer;

    // info reported from <roundstart>
    public string srv_strCurrentGameMode = "Deathmatch";
    public int srv_iCurrentFragLimit = 20;
    public int srv_iCurrentTimeLimit = 10;
    public int srv_iCurrentGoalsLimit = 10;
    public int srv_iCurrentMinPlayers = 1;
    public int srv_iCurrentMaxPlayers = 16;
    public bool srv_bCurrentJoinInProgress = false;

    public string srv_strBans = "";
    public string[] srv_aPlayerIndices = new string[16];

    public Dictionary<string, Player> srv_dicPlayers;

    /// <summary>
    /// Creates a new server and immediately starts the dedicated server
    /// </summary>
    /// <param name="rowServer">MySQL data row for the server information</param>
    public SeriousEngine3(Dictionary<string, string> rowServer, Dictionary<string, string> rowGame)
    {
      srv_rowServer = rowServer;
      srv_rowGame = rowGame;
      srv_dicPlayers = new Dictionary<string, Player>();

      for (int i = 0; i < srv_aPlayerIndices.Length; i++) {
        srv_aPlayerIndices[i] = "";
      }

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

        SSNL.Log(this, "Re-hooking");

        // start rcon connection
        if (!srv_bRestarting) {
          new Thread(new ThreadStart(ConnectionThread)).Start();
          new Thread(new ThreadStart(WaitThread)).Start();
        }

        // save initial stats
        SaveStats();
      } else {
        // otherwise, just start a new process
        StartServer();
      }
    }

    /// <summary>
    /// Get the name
    /// </summary>
    public override string GetName()
    {
      return srv_rowServer["Name"];
    }

    /// <summary>
    /// Get the gamemode
    /// </summary>
    public string GetGameMode()
    {
      return srv_rowServer["Gamemode"];
    }

    /// <summary>
    /// Get whether the server is private or not (need to be whitelisted to join)
    /// </summary>
    public bool IsPrivate()
    {
      return srv_rowServer["Private"] == "1";
    }

    /// <summary>
    /// Get the server's whitelist
    /// </summary>
    public Whitelist GetWhitelist()
    {
      return new Whitelist(int.Parse(srv_rowServer["ID"]));
    }

    public override string GetFirstLevel()
    {
      // if it's defined in the row
      if (srv_rowServer["Level"] != "") {
        return srv_rowServer["Level"];
      }

      // we'll need to pick a random one from the list file
      string[] astrLevelLines = File.ReadAllLines(GetWorkingDirectory() + "..\\" + GetLevelListFile());
      return astrLevelLines[SSNL.Rnd.Next(astrLevelLines.Length)];
    }

    /// <summary>
    /// Get level rotation list filename
    /// </summary>
    public string GetLevelListFile()
    {
      return srv_rowServer["LevelList"];
    }

    /// <summary>
    /// Get rcon password
    /// </summary>
    public string GetRconPassword()
    {
      return srv_strRconPassword;
    }

    /// <summary>
    /// Get whether pups should be enabled
    /// </summary>
    public bool GetPups()
    {
      return srv_rowServer["Pups"] == "1";
    }

    /// <summary>
    /// Is this server strict?
    /// </summary>
    public bool IsServerStrict()
    {
      return srv_rowServer["Strict"] == "1";
    }

    /// <summary>
    /// Get the server flags (such as [SI] for Strict + Insta)
    /// </summary>
    public string GetServerFlags()
    {
      string strFlags = "";

      if (IsServerStrict()) { strFlags += "S"; }
      if (srv_rowServer["Duel"] != "0") { strFlags += "D"; }
      if (srv_rowServer["Pups"] != "1") { strFlags += "P"; }
      if (srv_rowServer["DisallowedVotes"] == "all") { strFlags += "L"; }
      if (srv_rowServer["DisallowedVotes"] == "changegamemode") { strFlags += "G"; }

      return strFlags;
    }

    /// <summary>
    /// Get the disallowed votes
    /// </summary>
    public string GetDisallowedVotes()
    {
      return srv_rowServer["DisallowedVotes"];
    }

    public DateTime FromUnixTime(long unixTime)
    {
      var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
      return epoch.AddSeconds(unixTime);
    }

    public string CalculateBanUTC(string strBanTime, string strTime)
    {
      DateTime date = FromUnixTime(long.Parse(strBanTime));
      date += new TimeSpan(0, int.Parse(strTime), 0);
      return date.ToString("yyyy/MM/dd hh:mm:ss");
    }

    public string HtmlDecode(string strText)
    {
      string ret = strText;
      ret = ret.Replace("&lt;", "<");
      ret = ret.Replace("&gt;", ">");
      ret = ret.Replace("&quot;", "\"");
      ret = ret.Replace("&apos;", "'");
      ret = ret.Replace("&amp;", "&");
      ret = ret.Replace("\\n", "\n"); // questionable
      return ret;
    }

    /// <summary>
    /// Start the dedicated server process
    /// </summary>
    public override void StartServer()
    {
      srv_bClosing = false;

      if (srv_proc != null) {
        SSNL.Log(this, "Warning: Killing still-existing server process with ID " + srv_proc.Id);
        srv_proc.Kill();
        srv_proc = null;
      }
      SSNL.Log(this, "Starting");

      // generate rcon password
      srv_strRconPassword = "";
      string strAllowedCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
      for (int i = 0; i < 16; i++) {
        srv_strRconPassword += strAllowedCharacters[SSNL.Rnd.Next(0, strAllowedCharacters.Length)];
      }

      // get server flags in advance
      string strServerFlags = GetServerFlags();
      if (strServerFlags != "") {
        strServerFlags = " [" + strServerFlags + "]";
      }

      // write config file
      string strConfigFilename = GetWorkingDirectory() + "..\\SSNL\\_Server_" + srv_rowServer["ID"] + ".lua";
      if (File.Exists(strConfigFilename)) {
        File.Delete(strConfigFilename);
      }
      using (StreamWriter writer = new StreamWriter(File.Create(strConfigFilename))) {
        writer.WriteLine("prj_strMultiplayerSessionName = \"" + GetName() + strServerFlags + "\"");
        writer.WriteLine("gam_bAllowJoinInProgress = " + (srv_rowServer["Duel"] == "1" ? "0" : "1"));
        writer.WriteLine("prj_strDisabledVoteTypes=\"" + GetDisallowedVotes() + "\"");

        srv_strBans = "";
        dynamic resBans = SSNL.DB.Query("SELECT * FROM `bans` WHERE `Server`=-1 OR `Server`=" + srv_rowServer["ID"]);
        // steamID;steamID;steamID#endingTimestamp;steamID#endingTimestamp
        foreach (dynamic ban in resBans) {
          string strBanListing = ban["SteamID"];
          if (ban["Time"] != "0") {
            strBanListing += "#" + CalculateBanUTC(ban["BanTime"], ban["Time"]) + "#";
          }
          srv_strBans += strBanListing + ";";
        }
        writer.WriteLine("ser_strBanList = \"" + srv_strBans + "\"");
        //writer.WriteLine("dofile \"SSNL/Global.lua\"");
      }

      // build start arguments
      string strStartArguments = "";
      strStartArguments += "+gamemode " + GetGameMode() + " ";
      //strStartArguments += "+sessionname \"" + GetName() + strServerFlags + "\" ";
      strStartArguments += "+exec \"" + strConfigFilename.Replace(GetWorkingDirectory() + "..\\", "") + "\" ";
      strStartArguments += "+maxplayers " + GetMaxPlayers() + " ";
      strStartArguments += "+port " + GetPort() + " ";
      strStartArguments += "+level " + GetFirstLevel() + " ";
      strStartArguments += "+maplistfile " + GetLevelListFile() + " ";
      strStartArguments += "+rconpass " + GetRconPassword() + " ";
      strStartArguments += "+fps 150 ";
      strStartArguments += "+gam_bAllowPowerupItems " + (GetPups() ? "1" : "0") + " ";
      strStartArguments += srv_rowServer["Extra"];

      // start process
      srv_proc = Process.Start(new ProcessStartInfo() {
        WorkingDirectory = srv_rowGame["WorkingDirectory"],
        FileName = srv_rowGame["Executable"],
        Arguments = strStartArguments.Trim(),
        WindowStyle = ProcessWindowStyle.Hidden,
      });

      // start rcon connection
      if (!srv_bRestarting) {
        new Thread(new ThreadStart(ConnectionThread)).Start();
        new Thread(new ThreadStart(WaitThread)).Start();
      }

      // save initial stats
      SaveStats();
    }

    /// <summary>
    /// Stop the dedicated server process
    /// </summary>
    public override void StopServer()
    {
      srv_bClosing = true;

      try {
        srv_proc.Kill();
      } catch { }
      srv_proc = null;
      SSNL.Log(this, "Server killed!");

      SaveStats();
    }

    public void RconSend(string strCommand)
    {
      srv_writer.WriteLine(strCommand);
      Thread.Sleep(4000); // this should do for now.. D: THIS IS HORRIBLE. POKE CROTEAM ABOUT BUG #26686
      //Console.WriteLine("4 SECOND RCON SEND LAG, POKE CROTEAM ABOUT BUG #26686");

      // NOTE: We should ACTUALLY be using something like this:
      /*string strMake = "";
      while (true) {
        char c = (char)srv_reader.Read();
        if (c == '\r') {
          strMake = "";
        } else if (c == '\n') {
          if (strMake.Contains(strCommand)) {
            return; // Ok, we can continue...
          }
        } else {
          strMake += c;
        }
      }*/
      // BUT we can't. First off, we have multiple threads running, so this needs to be some boolean and this method waiting for that boolean to pass.
      // Second, that boolean needs to be set in some thread. Either, this needs to be a new thread that passes off output lines to console, and also something similar to above code.
      // Above code otherwise would miss other messages that the server sends, so we can't use it in a while loop like that.
      // Hey future Angelo, if you don't understand this mess of comments up here, it's 2:54 AM right now, so sue me.
      // POKE CROTEAM ABOUT BUG #26686
    }

    public void UpdatePlayerCount()
    {
      dynamic res = SSNL.DB.Query("SELECT * FROM `activeplayers` WHERE `Server`=" + srv_rowServer["ID"]);
      SSNL.DB.Query("UPDATE `servers` SET `_Players`=" + res.Length + " WHERE `ID`=" + srv_rowServer["ID"]);
      srv_ctPlayers = res.Length;
    }

    /// <summary>
    /// Rcon thread connection
    /// </summary>
    public void ConnectionThread()
    {
      while (true) {
        TcpClient client = null;
        while (true) {
          try {
            client = new TcpClient();
            client.Connect("127.0.0.1", GetPort());
            break;
          } catch (Exception ex) {
            //SSNL.Log(this, "Failed to connect to rcon, retrying... (" + ex.Message + ")");
          }
        }
        srv_reader = new StreamReader(client.GetStream(), Encoding.UTF8);
        srv_writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { NewLine = "\r\n", AutoFlush = true };
        try {
          RconSend(GetRconPassword());

          SSNL.Log(this, "Rcon connected");

          RconSend("prj_bExitOnSessionEnd=true");
          RconSend("gam_bInfiniteAmmo=" + srv_rowServer["InfiniteAmmo"]);
        } catch { }

        // we won't get information about current players - we don't care much about that...
        SSNL.DB.Query("DELETE FROM `activeplayers` WHERE `Server`=" + srv_rowServer["ID"]);
        SSNL.DB.Query("UPDATE `Servers` SET `_Players`=0 WHERE `ID`=" + srv_rowServer["ID"]);
        srv_ctPlayers = 0;

        while (true) {
          string strLine = "";
          try {
            strLine = srv_reader.ReadLine();
          } catch {
            SSNL.Log(this, "Lost connection");
            break;
          }
          if (strLine == null) {
            SSNL.Log(this, "Line reading error");
            break;
          }
          if (strLine.StartsWith("Server accepted connection from IP: ")) {
            string[] parse = strLine.Split(new string[] { ": ", ", ", "." }, StringSplitOptions.None);
            string strSteamID = parse[1];
            int iPlayerIndex = int.Parse(parse[3]);
            srv_aPlayerIndices[iPlayerIndex] = strSteamID;

            if (SSNL.DB.Query("SELECT * FROM `activeplayers` WHERE `Server`=" + srv_rowServer["ID"] + " AND `SteamID`='" + strSteamID + "'").Length == 0) {
              SSNL.DB.Query("INSERT INTO `activeplayers` (`Server`,`Spectating`,`SteamID`,`Name`,`Frags`,`Deaths`) VALUES(" +
                srv_rowServer["ID"] + ",1,'" + strSteamID + "','',0,0)");
            }

            UpdatePlayerCount();

            if (IsPrivate()) {
              Whitelist whitelist = GetWhitelist();
              if (!whitelist.IsPlayerWhitelisted(strSteamID)) {
                SSNL.Log(this, "Player with ID " + strSteamID + " is not allowed to play in this server - kicking!");
                RconSend("Wait(Delay(4));gamKickByIP(\"" + strSteamID + "\");");
                break;
              }
            }

            SSNL.Log(this, "Player with ID " + strSteamID + " is connecting");
          } else if (
            strLine.StartsWith("Server received a disconnect message from ") ||
            strLine.StartsWith("Server sent a disconnect message to ") ||
            strLine.StartsWith("Server terminating client ")) {

            int iPlayerIndex = int.Parse(strLine.Split(new string[] { "from ", "to ", "client ", "." }, StringSplitOptions.None)[1]);
            SSNL.DB.Query("DELETE FROM `activeplayers` WHERE `SteamID`='" + srv_aPlayerIndices[iPlayerIndex] + "'");
            srv_aPlayerIndices[iPlayerIndex] = "";

            UpdatePlayerCount();
          } else if (strLine.StartsWith("<")) {
            XMLTag tag = SimpleXMLReader.Parse(strLine);

            string strPlayer = "";
            string strPlayerId = "";
            int iAdmin = 0;

            if (tag.Attributes.ContainsKey("player")) { strPlayer = tag["player"]; }
            if (tag.Attributes.ContainsKey("playerid")) { strPlayerId = tag["playerid"]; }

            switch (tag.Name) {
              case "playerjoined":
                if (!srv_dicPlayers.ContainsKey(strPlayerId)) {
                  srv_dicPlayers[strPlayerId] = new Player();
                } else {
                  srv_dicPlayers[strPlayerId].ResetStats();
                }
                srv_dicPlayers[strPlayerId].ply_strName = strPlayer;

                SSNL.DB.Query("UPDATE `activeplayers` SET `Spectating`=0,`Name`='" + SSNL.DB.Safe(strPlayer) + "' WHERE `SteamID`='" + strPlayerId + "'");

                if (IsServerStrict()) {
                  RconSend("chatSay(\"~  " + SSNL.LuaSafe(strPlayer) + ", this is a STRICT server. Please see: serioussam.nl/strict\")");
                }

                SSNL.Log(this, strPlayer + " joined");
                break;

              case "playerleft":
                if (srv_dicPlayers.ContainsKey(strPlayerId)) {
                  srv_dicPlayers.Remove(strPlayerId);
                }

                SSNL.Log(this, strPlayer + " left");
                break;

              case "roundstart":
                srv_strCurrentGameMode = tag["gamemode"];
                srv_iCurrentFragLimit = int.Parse(tag["fraglimit"]);
                srv_iCurrentTimeLimit = int.Parse(tag["timelimit"]);
                srv_iCurrentGoalsLimit = int.Parse(tag["goalslimit"]);
                srv_iCurrentMinPlayers = int.Parse(tag["minplayers"]);
                srv_iCurrentMaxPlayers = int.Parse(tag["maxplayers"]);
                srv_bCurrentJoinInProgress = tag["joininprogress"] == "1";

                SSNL.DB.Query("UPDATE `activeplayers` SET `Spectating`=1,`Frags`=0,`Deaths`=0 WHERE `Server`=" + srv_rowServer["ID"]);
                SSNL.Log(this, "Round starting");
                break;

              case "chat":
                if (strPlayer == "" || strPlayerId == "[admin]") {
                  // ignore messages from server
                  break;
                }

                if (tag.Content.StartsWith("/")) {
                  iAdmin = SSNL.IsPlayerAdmin(strPlayerId);
                  string[] strParse = tag.Content.Split(' ');
                  switch (strParse[0]) {
                    case "/stats":
                      RconSend("chatSay(\"~  We don't track stats yet. :)\")");
                      break;
                  }
                }

                SSNL.DB.Query("UPDATE `activeplayers` SET `Name`='" + SSNL.DB.Safe(strPlayer) + "' WHERE `SteamID`='" + strPlayerId + "'");

                // if player is using chatSay("\n"), kick! *giggles*
                if (tag.Content.Contains("\n")) {
                  RconSend("gamKickByIP(\"" + strPlayerId + "\")");
                }

                SSNL.Log(this, HtmlDecode(strPlayer) + ": " + tag.Content);
                break;

              case "playerkilled":
                int iLastWarning = 2;
                string strWeapon = tag["damageinflictorweapon"];

                string strKiller = tag["killerplayer"];
                string strKillerId = tag["killerplayerid"];

                if (strKiller == "") {
                  SSNL.Log(this, HtmlDecode(strPlayer) + " died");
                } else if (strKiller == strPlayer) {
                  SSNL.Log(this, HtmlDecode(strPlayer) + " killed himself");
                } else {
                  SSNL.Log(this, HtmlDecode(strKiller) + " killed " + HtmlDecode(strPlayer));
                }

                if (srv_dicPlayers.ContainsKey(strPlayerId)) {
                  srv_dicPlayers[strPlayerId].ply_iDeaths++;
                  SSNL.DB.Query("UPDATE `activeplayers` SET `Deaths`=`Deaths`+1 WHERE `SteamID`='" + strPlayerId + "'");
                }

                if (strKillerId != "") {
                  if (srv_dicPlayers.ContainsKey(strKillerId)) {
                    srv_dicPlayers[strKillerId].ply_iKills++;
                    SSNL.DB.Query("UPDATE `activeplayers` SET `Frags`=`Frags`+1 WHERE `SteamID`='" + strKillerId + "'");
                  }
                }

                // if strict server
                if (IsServerStrict()) {
                  // disallowing of certain weapons
                  if (srv_strCurrentGameMode != "InstantKill") {
                    string[] strDisallowedWeapons = { "Cannon", "Sniper", "Chainsaw" };
                    // if disallowed weapon used
                    if (strDisallowedWeapons.Contains(strWeapon)) {
                      // if this player already received the max amount of warnings about disallowed weapons
                      if (srv_dicPlayers.ContainsKey(strKillerId)) {
                        if (srv_dicPlayers[strKillerId].ply_iWarnings == iLastWarning) {
                          SSNL.Log("Player " + strKiller + " (" + strKillerId + ") got kicked for disallowed weapon usage: " + strWeapon);
                          RconSend("chatSay(\"~  Kicking " + SSNL.LuaSafe(strKiller) + " for disallowed weapon usage.\")");
                          RconSend("gamKickByIP(\"" + strKillerId + "\")");
                        } else {
                          SSNL.Log("Player " + strKiller + " (" + strKillerId + ") got a warning for disallowed weapon usage: " + strWeapon);
                          srv_dicPlayers[strKillerId].ply_iWarnings++;
                          RconSend("chatSay(\"~  " + (srv_dicPlayers[strKillerId].ply_iWarnings == iLastWarning ? "FINAL WARNING" : "WARNING") + ": " +
                            SSNL.LuaSafe(strKiller) + ", " + strWeapon + " is NOT ALLOWED!!\")");
                        }
                      }
                    }
                  }
                }
                break;
            }
          }
        }

        // if the server was supposed to close
        if (srv_bClosing) {
          // don't attempt to reconnect
          break;
        }
      }
    }

    public override void AdminAction(string strType, string strSteamID)
    {
      switch (strType) {
        case "cancel_vote":
          try {
            srv_writer.WriteLine("samVoteFail()");
          } catch { }
          break;

        case "kick":
        case "ban":
          try {
            srv_writer.WriteLine("gamKickByIP(\"" + strSteamID + "\")");
          } catch { }
          break;
      }
    }

    int iBanSecondChecks = 0;
    int iChatSecond = 0;

    public override void OnSecond()
    {
      if (iBanSecondChecks++ >= 5) {
        iBanSecondChecks = 0;

        if (srv_rowServer["ID"] == "25") {
          int a = 5;
        }

        dynamic resBans = SSNL.DB.Query("SELECT * FROM `bans` WHERE (`Server`=-1 OR `Server`=" + srv_rowServer["ID"] + ") AND `BanTime");
        foreach (dynamic ban in resBans) {
          if (!srv_strBans.ToLower().Contains(ban["SteamID"].ToLower())) {
            string strOldBans = srv_strBans;
            if (ban["Time"] != "0") {
              srv_strBans += ban["SteamID"] + "#" + CalculateBanUTC(ban["BanTime"], ban["Time"]) + "#;";
              try {
                RconSend("ser_strBanList=\"" + srv_strBans + "\"");
              } catch {
                srv_strBans = strOldBans;
              }
            } else {
              srv_strBans += ban["SteamID"] + ";";
              try {
                RconSend("ser_strBanList=\"" + srv_strBans + "\"");
              } catch {
                srv_strBans = strOldBans;
              }
            }
          }
        }
      }

      if (iChatSecond++ >= 240) {
        iChatSecond = 0;

        try {
          RconSend("chatSay(\"~ Looking for Server Admins! See www.SeriousSam.nl\")");
        } catch { }
      }

      base.OnSecond();
    }
  }
}
