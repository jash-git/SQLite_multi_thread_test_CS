# C# 多個執行續存取 同一個 SQLITE 檔案(SQLite_multi_thread_test_CS)

## 資料來源: chatgpt [https://chatgpt.com/share/690c367d-3d78-8009-a07a-c930ad35f83d]

## 開發工具 : Visual Studio 2022 C#8.0

## 相依套件
- ** Microsoft.Data.Sqlite.Core(9.0.10) **
- ** SQLitePCLRaw.bundle_e_sqlite3(3.0.2) **

## Code
.cs
```
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SQLitePCL;
//---
//資料來源: https://chatgpt.com/share/690c367d-3d78-8009-a07a-c930ad35f83d
//---
class Program
{
    static void Pause()
    {
        Console.Write("Press any key to continue...");
        Console.ReadKey(true);
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

```