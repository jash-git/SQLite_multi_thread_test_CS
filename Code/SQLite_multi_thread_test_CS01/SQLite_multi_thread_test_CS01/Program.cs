using Microsoft.Data.Sqlite;
using SQLitePCL;
using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
//---
//資料來源: https://chatgpt.com/share/690c367d-3d78-8009-a07a-c930ad35f83d
//---
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

        return m_StrStartFileLine + " ~ " + m_StrEndFileLine + " consume time: " + elapsedTime + "~ FileName:" + m_StrTitle;
    }
    static void Pause()
    {
        Console.Write("Press any key to continue...");
        Console.ReadKey(true);
    }
    static void Main_00()
    {
        const string dbFile = "multi_thread_test.db";

        Batteries.Init();// 初始化 SQLitePCL 底層提供者

        // 若檔案不存在就初始化資料庫
        if (!File.Exists(dbFile))
        {
            InitializeDatabase(dbFile);
        }

        // 啟用 WAL 模式（支援多執行緒多讀單寫）
        using (var conn = new SqliteConnection($"Data Source={dbFile}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            var mode = cmd.ExecuteScalar();
            Console.WriteLine($"WAL 模式啟用結果: {mode}");
        }

        Start("使用 20 個執行緒同時寫入500筆資料 = 共10000筆");
        Console.WriteLine("開始平行寫入...");

        var connStr = new SqliteConnectionStringBuilder
        {
            DataSource = dbFile,
            Cache = SqliteCacheMode.Shared,
            Mode = SqliteOpenMode.ReadWrite
        }.ToString();

        // 使用 20 個執行緒平行寫入資料
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

    static void Main()
    {
        const string dbFile = "multi_thread_test.db";

        Batteries.Init();// 初始化 SQLitePCL 底層提供者

        // 若檔案不存在就初始化資料庫
        if (!File.Exists(dbFile))
        {
            InitializeDatabase(dbFile);
        }

        // 啟用 WAL 模式（支援多執行緒多讀單寫）
        using (var conn = new SqliteConnection($"Data Source={dbFile}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            var mode = cmd.ExecuteScalar();
            Console.WriteLine($"WAL 模式啟用結果: {mode}");
        }

        Start("使用 20 個執行緒同時寫入500筆資料 = 共10000筆");
        Console.WriteLine("開始平行寫入...");

        var connStr = new SqliteConnectionStringBuilder
        {
            DataSource = dbFile,
            Cache = SqliteCacheMode.Shared,
            Mode = SqliteOpenMode.ReadWrite
        }.ToString();

        // 使用 20 個執行緒平行寫入資料
        Thread[] Tall = new Thread[20];
        for (int i = 0; i < Tall.Length; i++)
        {
            Tall[i] = new Thread(() => WriteWithRetry(connStr, i));
        }

        for (int i = 0; i < Tall.Length; i++)
        {
            Tall[i].Start();
        }

        for (int i = 0; i < Tall.Length; i++)
        {
            Tall[i].Join();
        }

        Console.WriteLine("寫入完成。");
        Console.WriteLine(Stop());
        DataTable dataTable = GetDataFromSqlite(connStr, "log_table");
        Pause();
    }

    public static DataTable GetDataFromSqlite(string connStr, string tableName)
    {
        // 資料庫連線字串
        string connectionString = connStr;

        // 查詢單一資料表的 SQL 語句
        string selectQuery = $"SELECT * FROM {tableName}";

        // 建立一個空的 DataTable 變數
        DataTable dataTable = new DataTable(tableName);

        // 使用 try-catch-finally 確保資源正確釋放
        using (var connection = new SqliteConnection(connectionString))
        {
            try
            {
                // 1. 開啟資料庫連線
                connection.Open();

                // 2. 建立 SqliteCommand 物件
                using (var command = new SqliteCommand(selectQuery, connection))
                {
                    // 3. 建立 SqliteDataAdapter 物件
                    using (var reader = command.ExecuteReader())
                    {
                        // 2. 使用 DataTable 的 Load 方法填充資料
                        // DataTable.Load() 會從 IDataReader 讀取資料，並自動建立欄位結構
                        dataTable.Load(reader);
                    }
                }
            }
            catch (SqliteException ex)
            {
                // 處理 SQLite 錯誤
                Console.WriteLine($"SQLite 錯誤: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                // 處理其他錯誤
                Console.WriteLine($"發生錯誤: {ex.Message}");
                return null;
            }
        }

        return dataTable;
    }
    static void InitializeDatabase(string dbFile)
    {
        using var conn = new SqliteConnection($"Data Source={dbFile}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE log_table (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                thread_id INTEGER,
                msg TEXT,
                created_at TEXT
            );
        ";
        cmd.ExecuteNonQuery();
        conn.Close();

        Console.WriteLine("資料庫已初始化。");
    }

    static void WriteWithRetry(string connStr, int threadId)
    {
        const int maxRetry = 5;
        for (int retry = 0; retry < maxRetry; retry++)
        {
            try
            {
                for(int seq=0; seq<500; seq++)
                {
                    using var conn = new SqliteConnection(connStr);
                    conn.Open();
                    using var cmd = conn.CreateCommand();

                    cmd.CommandText = "INSERT INTO log_table (thread_id, msg, created_at) VALUES (@t, @m, datetime('now'))";
                    cmd.Parameters.AddWithValue("@t", threadId);
                    cmd.Parameters.AddWithValue("@m", $"Thread {threadId} - record {seq}");
                    cmd.ExecuteNonQuery();

                    conn.Close();
                }
                return; // 成功就跳出
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5) // database is locked
            {
                int delay = 100 * (retry + 1);
                Console.WriteLine($"[Thread {threadId}] 資料庫被鎖定，重試第 {retry + 1} 次 (延遲 {delay}ms)");
                Thread.Sleep(delay);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Thread {threadId}] 錯誤: {ex.Message}");
                return;
            }
        }
    }

    static void WriteWithRetry(string connStr, int threadId, int seq)
    {
        const int maxRetry = 5;
        for (int retry = 0; retry < maxRetry; retry++)
        {
            try
            {
                using var conn = new SqliteConnection(connStr);
                conn.Open();
                using var cmd = conn.CreateCommand();

                cmd.CommandText = "INSERT INTO log_table (thread_id, msg, created_at) VALUES (@t, @m, datetime('now'))";
                cmd.Parameters.AddWithValue("@t", threadId);
                cmd.Parameters.AddWithValue("@m", $"Thread {threadId} - record {seq}");
                cmd.ExecuteNonQuery();

                return; // 成功就跳出
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5) // database is locked
            {
                int delay = 100 * (retry + 1);
                Console.WriteLine($"[Thread {threadId}] 資料庫被鎖定，重試第 {retry + 1} 次 (延遲 {delay}ms)");
                Thread.Sleep(delay);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Thread {threadId}] 錯誤: {ex.Message}");
                return;
            }
        }
    }
}
