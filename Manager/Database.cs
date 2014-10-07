using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySql.Data;
using MySql.Data.MySqlClient;
using System.Data;
using System.Threading;

namespace SSNL_Manager
{
  public class Database
  {
    Mutex ThreadLock = new Mutex();

    MySqlConnection connection;
    string connectionString;

    public Database(string host, string database, string username, string password)
    {
      this.connectionString = "Server=" + host + ";" +
                              "Database=" + database + ";" +
                              "User ID=" + username + ";" +
                              "Password=" + password + ";" +
                              "Pooling=false;CharSet=utf8;";
      this.connection = new MySqlConnection(this.connectionString);
      while (true) {
        try {
          this.connection.Open();
          break;
        } catch {
          Console.WriteLine("MySQL seems dead. Retrying...");
        }
      }
    }

    public Dictionary<string, string>[] Query(string qry)
    {
      ThreadLock.WaitOne();

      IDbCommand dbcmd = this.connection.CreateCommand();
      dbcmd.CommandText = qry;
      IDataReader dr = null;
      try {
        dr = dbcmd.ExecuteReader();
      } catch (Exception ex) {
        Console.WriteLine("\n********************************************\nQUERY ERROR:\n" + ex.Message + "\n\nQUERY WAS:\n" + qry + "\n********************************************");
      }

      if (dr == null && this.connection.State != ConnectionState.Connecting) {
        this.connection.Close();
        this.connection = new MySqlConnection(this.connectionString);
        while (true) {
          try {
            this.connection.Open();
            break;
          } catch { }
        }

        ThreadLock.ReleaseMutex();
        return Query(qry);
      }

      return Rows(dr);
    }

    public Dictionary<string, string>[] Rows(IDataReader rowdata)
    {
      List<Dictionary<string, string>> rows = new List<Dictionary<string, string>>();

      while (rowdata.Read()) {
        Dictionary<string, string> nr = new Dictionary<string, string>();

        for (int i = 0; i < rowdata.FieldCount; i++)
          nr.Add(rowdata.GetName(i), rowdata.GetValue(i).ToString());

        rows.Add(nr);
      }

      rowdata.Close();
      rowdata.Dispose();
      rowdata = null;

      ThreadLock.ReleaseMutex();

      return rows.ToArray();
    }

    public string Safe(string str)
    {
      return str.Replace("\\", "\\\\").Replace("'", "\\'").Replace("`", "\\`");
    }
  }
}
