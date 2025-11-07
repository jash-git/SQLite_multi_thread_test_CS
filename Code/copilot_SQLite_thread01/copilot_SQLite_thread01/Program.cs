// See https:using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

//---
//資料來源:https://copilot.microsoft.com/shares/jAVXrnk3EjFAWaqU2HEFh
//---

class Program
{
    private const string ConnectionString = "Data Source=sample.db;Version=3;Cache=Shared;Journal Mode=WAL;Synchronous=Normal;Pooling=True;";
    private static readonly SemaphoreSlim WriteLock = new SemaphoreSlim(1, 1);
    private static Stopwatch m_stopWatch = new Stopwatch();
    private static String m_StrTitle = "";
    private static String m_StrStartFileLine = "";
    private static String m_StrEndFileLine = "";
    public static void Start(String StrInfor)
    {
        StackFrame CallStack = new StackFrame(1, true);
        m_StrStartFileLine = String.Format("File : {0} , Line : {1}", CallStack.GetFileName(), CallStack.GetFileLineNumber());
        m_StrTitle = StrInfor;

        m_stopWatch.Start();
    }
    public static string Stop()
    {
        StackFrame CallStack = new StackFrame(1, true);

        m_stopWatch.Stop();

        // Get the elapsed time as a TimeSpan value.
        TimeSpan ts = m_stopWatch.Elapsed;
        // Format and display the TimeSpan value.
        string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);

        m_StrEndFileLine = String.Format("File : {0} , Line : {1}", CallStack.GetFileName(), CallStack.GetFileLineNumber());

        return m_StrStartFileLine + " ~ " + m_StrEndFileLine + " consume time: " + elapsedTime + "~ FileName:" + m_StrTitle;
    }
    static void Pause()
    {
        Console.Write("Press any key to continue...");
        Console.ReadKey(true);
    }
    static async Task Main(string[] args)
    {
        // 初始化資料庫
        InitializeDatabase();

        Start("使用 20 個執行緒同時寫入500筆資料 = 共10000筆");
        // 建立多個執行緒進行讀寫
        var tasks = new Task[20];
        for (int i = 0; i < tasks.Length; i++)
        {
            int threadId = i;
            tasks[i] = Task.Run(async () =>
            {
                await WriteDataAsync($"Thread-{threadId}", threadId);
                //ReadData();
            });
        }

        await Task.WhenAll(tasks);
        Console.WriteLine("所有執行緒完成。");
        Console.WriteLine(Stop());
        Pause();
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
            for(int i = 0;i<500;i++)
            {
                using var conn = new SQLiteConnection(ConnectionString);
                conn.Open();
                using var cmd = new SQLiteCommand("INSERT INTO Logs (ThreadName, Value) VALUES (@name, @val)", conn);
                cmd.Parameters.AddWithValue("@name", threadName);
                cmd.Parameters.AddWithValue("@val", i);
                cmd.ExecuteNonQuery();
                //Console.WriteLine($"寫入：{threadName} - {i}");
            }
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

