/*
	The MIT License (MIT)

	Copyright (c) 2014 Angelo Geels (angelog.nl, spansjh@gmail.com)

	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in all
	copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
	SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.Globalization;

namespace SSNL_Manager
{
  public class SSNL
  {
    public static Random Rnd = new Random();

    public static Database DB;
    public static List<Server> Servers = new List<Server>();

    /// <summary>
    /// Add an entry to the log, in console and in database
    /// </summary>
    /// <param name="srv">The server</param>
    /// <param name="str">The log line</param>
    public static void Log(Server srv, string str)
    {
      Console.ForegroundColor = srv.GetConsoleColor();
      Console.WriteLine("[" + DateTime.Now.ToString() + "] [" + srv.GetName().Split('~').Last().Trim() + "] " + str);
    }

    /// <summary>
    /// Add an entry to the log, in console and in database
    /// </summary>
    /// <param name="str">The log line</param>
    public static void Log(string str)
    {
      Console.ForegroundColor = ConsoleColor.Gray;
      Console.WriteLine("[" + DateTime.Now.ToString() + "] " + str);
    }

    /// <summary>
    /// Add an entry to the log, only in the console (skip adding it to the database)
    /// </summary>
    /// <param name="srv">The server</param>
    /// <param name="str">The log line</param>
    public static void LogNoDatabase(Server srv, string str)
    {
      Console.ForegroundColor = srv.GetConsoleColor();
      Console.WriteLine("[" + DateTime.Now.ToString() + "] [" + srv.GetName().Split('~').Last().Trim() + "] " + str);
    }

    /// <summary>
    /// Add an entry to the log, only in the console (skip adding it to the database)
    /// </summary>
    /// <param name="str">The log line</param>
    public static void LogNoDatabase(string str)
    {
      Console.ForegroundColor = ConsoleColor.Gray;
      Console.WriteLine("[" + DateTime.Now.ToString() + "] " + str);
    }

    /// <summary>
    /// Check servers in database against active servers and starts/stops new servers if needed
    /// </summary>
    public static void CheckServers()
    {
      LogNoDatabase("Checking servers");

      dynamic rowsServers = DB.Query("SELECT * FROM `servers`");

      for (int i = 0; i < rowsServers.Length; i++) {
        // if the server needs to be active
        if (rowsServers[i]["Active"] == "1") {
          // check if it's already on in this session
          bool bFound = false;
          for (int j = 0; j < Servers.Count; j++) {
            if (Servers[j].srv_rowServer["ID"] == rowsServers[i]["ID"]) {
              bFound = true;
              break;
            }
          }
          // if the server is not active in the current session, start it
          if (!bFound) {
            // new server based on which game
            Server srv = null;
            dynamic rowGame = DB.Query("SELECT * FROM `games` WHERE `ID`=" + rowsServers[i]["Game"])[0];
            switch ((string)rowsServers[i]["Game"]) {
              case "1": srv = new SeriousEngine3(rowsServers[i], rowGame); break;
              case "2": srv = new SeriousSamRevolution(rowsServers[i], rowGame); break;
              case "3": srv = new SeriousEngine3(rowsServers[i], rowGame); break;
              case "4": srv = new SeriousSamHDFE(rowsServers[i], rowGame); break;
            }
            if (srv != null) {
              Servers.Add(srv);
            }
          }
        } else { // if the server needs to be inactive
          for (int j = 0; j < Servers.Count; j++) {
            if (Servers[j].srv_rowServer["ID"] == rowsServers[i]["ID"]) {
              Servers[j].StopServer();
              Servers.RemoveAt(j);
              break;
            }
          }
        }
      }
    }

    /// <summary>
    /// Convert Croteam's Steam ID hex format to a normal 64 bit unsigned integer.
    /// </summary>
    public static ulong CroteamTo64(string strId)
    {
      return ulong.Parse(strId, NumberStyles.HexNumber);
    }

    /// <summary>
    /// Is given Steam ID an admin on this server?
    /// </summary>
    /// <param name="strId">The Steam ID in Croteam hex format</param>
    public static int IsPlayerAdmin(string strId)
    {
      dynamic resAdmin = DB.Query("SELECT * FROM `admins` WHERE `SteamID`=" + CroteamTo64(strId));
      if (resAdmin.Length == 0) {
        return 0;
      } else {
        return int.Parse(resAdmin[0]["Rights"]);
      }
    }

    /// <summary>
    /// Make a string safe for inclusion in lua code evaluation
    /// </summary>
    public static string LuaSafe(string strInput)
    {
      return strInput.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    /// <summary>
    /// Entry point
    /// </summary>
    static void Main(string[] args)
    {
      DB = new Database("localhost", "ssnl", "ssnl", "");

      Log("SeriousSam.nl server manager starting");

      // initial check for servers (this will start all servers first)
      CheckServers();

      long ctTotalSeconds = 0;
      while (true) {
        Thread.Sleep(1000);
        ctTotalSeconds++;

        // every minute
        if (ctTotalSeconds % 60 == 0) {
          // check servers
          CheckServers();
        }
      }
    }
  }
}
