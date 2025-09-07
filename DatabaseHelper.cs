using System;
using Microsoft.Data.Sqlite;

class DatabaseHelper: IDisposable
{
    private readonly SqliteConnection _conn;
    private static readonly Random rand = new Random();

    public DatabaseHelper(string dbPath)
    {
        _conn = new SqliteConnection($"Data Source={dbPath}");
        _conn.Open();

        string createTable = @"
            CREATE TABLE IF NOT EXISTS transitions (
                curr TEXT NOT NULL,
                next TEXT NOT NULL,
                count INTEGER NOT NULL,
                PRIMARY KEY (curr, next)
            );
        ";
        using var cmd = new SqliteCommand(createTable, _conn);
        cmd.ExecuteNonQuery();
    }

     public void InsertOrIncrement(string curr, string next)
    {
        string sql = @"
            INSERT INTO transitions (curr, next, count)
            VALUES (@curr, @next, 1)
            ON CONFLICT(curr, next) DO UPDATE SET count = count + 1;
        ";

        using var cmd = new SqliteCommand(sql, _conn);
        cmd.Parameters.AddWithValue("@curr", curr);
        cmd.Parameters.AddWithValue("@next", next);
        cmd.ExecuteNonQuery();
    }

   public void InsertBatch(List<(string curr, string next)> batch)
{
    using var transaction = _conn.BeginTransaction();
    using var cmd = _conn.CreateCommand();
    cmd.CommandText = @"
        INSERT INTO transitions (curr, next, count)
        VALUES ($curr, $next, 1)
        ON CONFLICT(curr, next) DO UPDATE SET count = count + 1;
    ";

    var currParam = cmd.CreateParameter();
    currParam.ParameterName = "$curr";
    cmd.Parameters.Add(currParam);

    var nextParam = cmd.CreateParameter();
    nextParam.ParameterName = "$next";
    cmd.Parameters.Add(nextParam);

    foreach (var (curr, next) in batch)
    {
        currParam.Value = curr;
        nextParam.Value = next;
        cmd.ExecuteNonQuery();
    }

    transaction.Commit();
}


    public (string nextWord, int count)[] GetFollowers(string curr)
    {
        string sql = "SELECT next, count FROM transitions WHERE curr = @curr";
        using var cmd = new SqliteCommand(sql, _conn);
        cmd.Parameters.AddWithValue("@curr", curr);

        using var reader = cmd.ExecuteReader();
        var results = new System.Collections.Generic.List<(string, int)>();
        while (reader.Read())
        {
            results.Add((reader.GetString(0), reader.GetInt32(1)));
        }
        return results.ToArray();
    }

   public string GetRandomWord()
{
    string word = _conn != null 
        ? new SqliteCommand("SELECT curr FROM transitions ORDER BY RANDOM() LIMIT 1", _conn)
            .ExecuteScalar() as string
        : null;

    if (word == null)
    {

        throw new InvalidOperationException("null no word in database.");
    }

    return word;
}



public string GetWeightedNextWord(string curr)
{
    var followers = GetFollowers(curr);
    if (followers.Length == 0)
    {
        throw new InvalidOperationException($"null, no followers found for '{curr}'.");
    }

    int total = followers.Sum(f => f.count);
    int choice = rand.Next(total);

    foreach (var (word, count) in followers)
    {
        if (choice < count) return word;
        choice -= count;
    }

    return followers[0].Item1;
}

public void Dispose()   
    {
        _conn?.Close();
        _conn?.Dispose();
    }

 public void ClearDatabase()
{
    string sql = "DROP TABLE IF EXISTS transitions;";
    using var cmd = new SqliteCommand(sql, _conn);
    cmd.ExecuteNonQuery();

    string createTable = @"
        CREATE TABLE IF NOT EXISTS transitions (
            curr TEXT NOT NULL,
            next TEXT NOT NULL,
            count INTEGER NOT NULL,
            PRIMARY KEY (curr, next)
        );";
    using var cmd2 = new SqliteCommand(createTable, _conn);
    cmd2.ExecuteNonQuery();
}


}
