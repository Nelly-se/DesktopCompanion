using System;
using System.Data;
using Microsoft.Data.Sqlite;
var path = @"d:\software_construction\program\src\SpiritDesk.Shell\bin\Debug\net9.0-windows\data\spiritdesk.db";
Console.WriteLine(System.IO.File.Exists(path) ? "EXISTS" : "NO_DB");
if (System.IO.File.Exists(path))
{
    using var conn = new SqliteConnection($"Data Source={path}");
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "PRAGMA table_info(Spirits);";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        Console.WriteLine($"{reader.GetValue(1)}|{reader.GetValue(2)}");
    }
}
