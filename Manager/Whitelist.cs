using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SSNL_Manager
{
  public class Whitelist
  {
    public List<long> Players = new List<long>();

    public Whitelist(int iServer)
    {
      dynamic res = SSNL.DB.Query("SELECT * FROM `whitelist` WHERE `Server`=" + iServer);
      for (int i = 0; i < res.Length; i++) {
        //Console.WriteLine("Whitelisted player: " + res[i]["Note"]);
        Players.Add(long.Parse(res[i]["SteamID"]));
      }
    }

    public bool IsPlayerWhitelisted(string strSteamID)
    {
      return Players.Contains(Int64.Parse(strSteamID, System.Globalization.NumberStyles.HexNumber));
    }
  }
}
