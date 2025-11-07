using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.Threading;

//---
//資料來源: https://www.perplexity.ai/search/c-duo-ge-zhi-xing-xu-cun-qu-to-5skSajwvSiyOTviWaq2LfA#1
//---
class Program
{
    // 鎖物件，確保同一時間只有一個執行緒操作資料庫
    private static readonly object dbLock = new object();

    private static string connectionString = "Data Source=mydatabase.db;Version=3;Pooling=True;";
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
    static void Main()
    {
        // 建立資料表（確保資料庫已存在並有表）
        InitializeDatabase();

        Start("使用 20 個執行緒同時寫入500筆資料 = 共10000筆");
        Console.WriteLine("開始平行寫入...");
        
        // 建立多個執行緒模擬同時寫入
        Thread[] Tall=new Thread[20];
        for (int i = 0; i < Tall.Length; i++)
        {
            Tall[i] = new Thread(() => InsertData($"Thread {i}"));
        }

        for (int i = 0; i < Tall.Length; i++)
        {
            Tall[i].Start();
        }

        for (int i = 0; i < Tall.Length; i++)
        {
            Tall[i].Join();
        }

        Console.WriteLine("Finished.");
        Console.WriteLine(Stop());
        Pause();
    }

    static void InitializeDatabase()
    {
        using (var conn = new SQLiteConnection(connectionString))
        {
            conn.Open();
            string createTableSql = "CREATE TABLE IF NOT EXISTS Logs (Id INTEGER PRIMARY KEY AUTOINCREMENT, Message TEXT, CreatedAt DATETIME)";
            using (var cmd = new SQLiteCommand(createTableSql, conn))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }

    static void InsertData(string message)
    {
        for (int i = 0; i < 500; i++)
        {
            lock (dbLock)
            {
                using (var conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();

                    using (var transaction = conn.BeginTransaction())
                    {
                        string insertSql = "INSERT INTO Logs (Message, CreatedAt) VALUES (@msg, @time)";
                        using (var cmd = new SQLiteCommand(insertSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@msg", $"{message} - Entry {i}");
                            cmd.Parameters.AddWithValue("@time", DateTime.Now);
                            cmd.ExecuteNonQuery();
                        }
                        transaction.Commit();
                    }
                }
            }
            //Thread.Sleep(100); // 模擬作業間隔
        }
    }
}
