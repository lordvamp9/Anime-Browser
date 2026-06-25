using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace vamp9.AnimeDashboard
{
    public class AnimeEntry
    {
        public string Name { get; set; }
        public int LastEpisode { get; set; }
        public string LastWatched { get; set; }
        public bool IsWatchLater { get; set; }
        public string EpisodeText => LastEpisode == 0 ? "Sin ver" : $"Episodio {LastEpisode}";
    }

    public static class DatabaseManager
    {
        private static readonly string DbPath = "anime_db.json";

        public static List<AnimeEntry> Load()
        {
            if (!File.Exists(DbPath))
                return new List<AnimeEntry>();

            string json = File.ReadAllText(DbPath);
            return JsonSerializer.Deserialize<List<AnimeEntry>>(json) ?? new List<AnimeEntry>();
        }

        public static void Save(List<AnimeEntry> db)
        {
            string json = JsonSerializer.Serialize(db, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(DbPath, json);
        }

        public static void AddWatchLater(string name)
        {
            var db = Load();
            var existing = db.FirstOrDefault(x => x.Name == name);
            if (existing == null)
            {
                db.Add(new AnimeEntry { Name = name, LastEpisode = 0, LastWatched = "-", IsWatchLater = true });
            }
            else
            {
                existing.IsWatchLater = true;
            }
            Save(db);
        }

        public static void UpdateEpisode(string name)
        {
            var db = Load();
            var existing = db.FirstOrDefault(x => x.Name == name);
            if (existing == null)
            {
                db.Add(new AnimeEntry { Name = name, LastEpisode = 1, LastWatched = DateTime.Now.ToString("dd/MM/yyyy HH:mm"), IsWatchLater = false });
            }
            else
            {
                existing.LastEpisode++;
                existing.LastWatched = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                existing.IsWatchLater = false;
            }
            Save(db);
        }
    }
}
