using System;
using System.IO;
using TiltDrop.Core;
using UnityEngine;

namespace TiltDrop.Game
{
    /// <summary>Atomic JSON save in persistentDataPath with one backup generation.</summary>
    public sealed class SaveStore
    {
        readonly string path, tmp, bak;

        public SaveStore(string dir = null)
        {
            dir = dir ?? Application.persistentDataPath;
            path = Path.Combine(dir, "save.json");
            tmp = path + ".tmp";
            bak = path + ".bak";
        }

        public SaveData Load(IClock clock)
        {
            foreach (var p in new[] { path, bak })
            {
                try
                {
                    if (!File.Exists(p)) continue;
                    var s = SaveData.FromJson(File.ReadAllText(p));
                    if (s != null) return s;
                    Debug.LogWarning($"save file {p} rejected (corrupt or modified)");
                }
                catch (Exception e) { Debug.LogException(e); }
            }
            return new SaveData { CreatedUtcTicks = clock.UtcNow.Ticks };
        }

        public void Save(SaveData s)
        {
            try
            {
                File.WriteAllText(tmp, s.ToJson());
                if (File.Exists(path))
                {
                    if (File.Exists(bak)) File.Delete(bak);
                    File.Move(path, bak);
                }
                File.Move(tmp, path);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>"Delete my data" (COMPLIANCE §1).</summary>
        public void DeleteAll()
        {
            foreach (var p in new[] { path, tmp, bak }) { try { if (File.Exists(p)) File.Delete(p); } catch (Exception e) { Debug.LogException(e); } }
            PlayerPrefs.DeleteAll();
        }
    }
}
