// See https:using System;
using System.Data.SQLite;
using System.Threading;
using System.Threading.Tasks;

//---
//資料來源:https://copilot.microsoft.com/shares/jAVXrnk3EjFAWaqU2HEFh
//---

class Program
{
    private const string ConnectionString = "Data Source=sample.db;Version=3;Cache=Shared;Journal Mode=WAL;Synchronous=Normal;";
    private static readonly SemaphoreSlim WriteLock = new SemaphoreSlim(1, 1);

    static async Task Main(string[] args)
    {
        // 初始化資料庫
        InitializeDatabase();

        // 建立多個執行緒進行讀寫
        var tasks = new Task[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            int threadId = i;
            tasks[i] = Task.Run(async () =>
            {
                await WriteDataAsync($"Thread-{threadId}", threadId);
                ReadData();
            });
        }

        await Task.WhenAll(tasks);
        Console.WriteLine("所有執行緒完成。");
    }

    static void InitializeDatabase()
    {
        using var conn = new SQLiteConnection(ConnectionString);
        conn.Open();
        using var cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Logs (Id INTEGER PRIMARY KEY AUTOINCREMENT, ThreadName TEXT, Value INTEGER)", conn);
        cmd.ExecuteNonQuery();
    }

    static async Task WriteDataAsync(string threadName, int value)
    {
        await WriteLock.WaitAsync();
        try
        {
            using var conn = new SQLiteConnection(ConnectionString);
            conn.Open();
            using var cmd = new SQLiteCommand("INSERT INTO Logs (ThreadName, Value) VALUES (@name, @val)", conn);
            cmd.Parameters.AddWithValue("@name", threadName);
            cmd.Parameters.AddWithValue("@val", value);
            cmd.ExecuteNonQuery();
            Console.WriteLine($"寫入：{threadName} - {value}");
        }
        finally
        {
            WriteLock.Release();
        }
    }

    static void ReadData()
    {
        using var conn = new SQLiteConnection(ConnectionString);
        conn.Open();
        using var cmd = new SQLiteCommand("SELECT * FROM Logs", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            Console.WriteLine($"讀取：{reader["ThreadName"]} - {reader["Value"]}");
        }
    }
}

