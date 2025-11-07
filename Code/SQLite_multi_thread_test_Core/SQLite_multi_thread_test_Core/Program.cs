using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

class Program
{
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

        return m_StrStartFileLine + " ~ " + m_StrEndFileLine + " consume time: " + elapsedTime +"~ FileName:" + m_StrTitle;
    }
    static void Pause()
    {
        Console.Write("Press any key to continue...");
        Console.ReadKey(true);
    }

    static void Main()
    {
        const string dbFile = "multi_thread_sqlite_test.db";

        // 若檔案不存在就初始化資料庫
        if (!File.Exists(dbFile))
        {
            InitializeDatabase(dbFile);
        }

        // 啟用 WAL 模式（多執行緒寫入推薦）
        using (var conn = new SQLiteConnection($"Data Source={dbFile};Version=3;"))
        {
            conn.Open();
            using var cmd = new SQLiteCommand("PRAGMA journal_mode=WAL;", conn);
            var result = cmd.ExecuteScalar();
            Console.WriteLine($"WAL 模式啟用結果: {result}");
        }

        Start("使用 20 個執行緒同時寫入500筆資料 = 共10000筆");
        Console.WriteLine("開始平行寫入...");

        string connStr = $"Data Source={dbFile};Version=3;Cache=Shared;Journal Mode=WAL;";

        // 使用 20 個執行緒同時寫入500筆資料 = 共10000筆
        Parallel.For(0, 20, i =>
        {
            for (int j = 0; j < 500; j++)
            {
                WriteWithRetry(connStr, i, j);
            }
        });

        Console.WriteLine("寫入完成。");

        Console.WriteLine(Stop());
        Pause();
    }

    static void InitializeDatabase(string dbFile)
    {
        SQLiteConnection.CreateFile(dbFile);

        using var conn = new SQLiteConnection($"Data Source={dbFile};Version=3;");
        conn.Open();

        using var cmd = new SQLiteCommand(@"
            CREATE TABLE log_table (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                thread_id INTEGER,
                msg TEXT,
                created_at TEXT
            );
        ", conn);
        cmd.ExecuteNonQuery();

        Console.WriteLine("資料庫已初始化。");
    }

    static void WriteWithRetry(string connStr, int threadId, int seq)
    {
        const int maxRetry = 5;

        for (int retry = 0; retry < maxRetry; retry++)
        {
            try
            {
                using var conn = new SQLiteConnection(connStr);
                conn.Open();

                using var cmd = new SQLiteCommand(
                    "INSERT INTO log_table (thread_id, msg, created_at) VALUES (@t, @m, datetime('now'))",
                    conn);
                cmd.Parameters.AddWithValue("@t", threadId);
                cmd.Parameters.AddWithValue("@m", $"Thread {threadId} - record {seq}");
                cmd.ExecuteNonQuery();

                return; // 成功就離開
            }
            catch (SQLiteException ex)
            {
                if (ex.ErrorCode == (int)SQLiteErrorCode.Busy) // 資料庫被鎖定
                {
                    int delay = 100 * (retry + 1);
                    Console.WriteLine($"[Thread {threadId}] 資料庫被鎖定，重試第 {retry + 1} 次 (延遲 {delay}ms)");
                    Thread.Sleep(delay);
                    continue;
                }
                Console.WriteLine($"[Thread {threadId}] SQLite 錯誤: {ex.Message}");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Thread {threadId}] 一般錯誤: {ex.Message}");
                return;
            }
        }
    }
}
