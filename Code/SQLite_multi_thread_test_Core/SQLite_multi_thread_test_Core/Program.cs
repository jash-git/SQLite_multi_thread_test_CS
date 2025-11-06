using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Data.SQLite;

class Program
{
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

        Console.WriteLine("開始平行寫入...");

        string connStr = $"Data Source={dbFile};Version=3;Cache=Shared;Journal Mode=WAL;";

        // 使用 20 個執行緒同時寫入資料
        Parallel.For(0, 20, i =>
        {
            for (int j = 0; j < 5; j++)
            {
                WriteWithRetry(connStr, i, j);
            }
        });

        Console.WriteLine("寫入完成。");
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
