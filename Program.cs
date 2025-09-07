using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO.Compression;

class MarkovTextGenerator
{
    private readonly DatabaseHelper db;
    private readonly Random rand = new Random();

    public MarkovTextGenerator(DatabaseHelper databaseHelper)
    {
        db = databaseHelper;
    }

    private void TrainFromDirectory(string path)
{
    foreach (string file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
    {
        try
        {
            Console.WriteLine($"Found file: {file}");

        
            if (file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var archive = ZipFile.OpenRead(file);
                Console.WriteLine($"Processing ZIP: {file} ({archive.Entries.Count} entries)");

                foreach (var entry in archive.Entries.Where(e => e.FullName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)))
                {
            

                    using var stream = entry.Open();
                    using var reader = new StreamReader(stream);

                    var batch = new List<(string curr, string next)>();
                    string prevWord = null;
                    int lineCount = 0;

                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] words = Regex.Split(line.ToLower(), @"\W+")
                                              .Where(w => !string.IsNullOrWhiteSpace(w))
                                              .Where(w => w.Length >= 1 && w.Length < 22)
                                              .ToArray();

                        for (int i = 0; i < words.Length - 1; i++)
                        {
                            batch.Add((words[i], words[i + 1]));
                            if (batch.Count >= 500)
                            {
                                db.InsertBatch(batch);
                                batch.Clear();
                            }
                        }

                        lineCount += words.Length;
                        if (lineCount % 1000 == 0)
                            Console.WriteLine($"    {lineCount} words processed...");
                    }

                    if (batch.Count > 0)
                        db.InsertBatch(batch);

                    Console.WriteLine($" -> Finished {entry.FullName}, {lineCount} words processed");
                }
            }
           
            else if (file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                string text = File.ReadAllText(file);
                Train(text); 
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Skipping {file}: {ex.Message}");
        }
    }
}

    private void Train(string text)
    {
        string[] words = Regex.Split(text.ToLower(), @"\W+")
                              .Where(w => !string.IsNullOrWhiteSpace(w))
                              .Where(w => w.Length >= 1 && w.Length < 22)
                              .ToArray();

        if (words.Length < 2) return;

        for (int i = 0; i < words.Length - 1; i++)
        {
            string curr = words[i];
            string next = words[i + 1];

            db.InsertOrIncrement(curr, next);  
        }
    }

    public string Generate(int length = 50)
    {
        string currentWord = db.GetRandomWord(); 
        if (currentWord == null)
            throw new InvalidOperationException("No training data in database!");

        StringBuilder result = new StringBuilder(currentWord);

        for (int i = 1; i < length; i++)
        {
            string nextWord = db.GetWeightedNextWord(currentWord); 
            if (nextWord == null) break;

            result.Append(" ").Append(nextWord);
            currentWord = nextWord;
        }

        return result.ToString();
    }

    static void Main(string[] args)
    {
        string gutenbergPath = @"D:/";
        string connectionString = "Server=localhost;Database=MarkovDB;Trusted_Connection=True;";

        using var db = new DatabaseHelper("MarkovDB.sqlite");
        
        if (args.Contains("--clear-db"))
    {
        Console.WriteLine("Clearing database...");
        db.ClearDatabase();
        Console.WriteLine("Database cleared.");
        return; 
    }

        
        var generator = new MarkovTextGenerator(db);

        generator.TrainFromDirectory(gutenbergPath);

        Console.WriteLine("Training complete! Word pairs saved to SQL.\n");

        Console.WriteLine("Generated text:\n");
        Console.WriteLine(generator.Generate(100));
    }
}
