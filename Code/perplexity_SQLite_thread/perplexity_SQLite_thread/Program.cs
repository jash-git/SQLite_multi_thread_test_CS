using System;
using System.Data.SQLite;
using System.Threading;

//---
//資料來源: https://www.perplexity.ai/search/c-duo-ge-zhi-xing-xu-cun-qu-to-5skSajwvSiyOTviWaq2LfA#1
//---
class Program
{
    // 鎖物件，確保同一時間只有一個執行緒操作資料庫
    private static readonly object dbLock = new object();

    private static string connectionString = "Data Source=mydatabase.db;Version=3;Pooling=True;";

    static void Main()
    {
        // 建立資料表（確保資料庫已存在並有表）
        InitializeDatabase();

        // 建立多個執行緒模擬同時寫入
        Thread t1 = new Thread(() => InsertData("Thread 1"));
        Thread t2 = new Thread(() => InsertData("Thread 2"));
        Thread t3 = new Thread(() => InsertData("Thread 3"));

        t1.Start();
        t2.Start();
        t3.Start();

        t1.Join();
        t2.Join();
        t3.Join();

        Console.WriteLine("Finished.");
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
        for (int i = 0; i < 5; i++)
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
            Thread.Sleep(100); // 模擬作業間隔
        }
    }
}
