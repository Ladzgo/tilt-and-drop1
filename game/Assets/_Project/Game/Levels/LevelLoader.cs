using TiltDrop.Core;
using UnityEngine;

namespace TiltDrop.Game
{
    /// <summary>Loads packs copied to Resources/Levels by the editor sync (shared/levels -> *.bytes).</summary>
    public static class LevelLoader
    {
        public const string Folder = "Levels";

        public static string ResourceName(string packFile)
        {
            int dot = packFile.IndexOf('.');
            return Folder + "/" + (dot < 0 ? packFile : packFile.Substring(0, dot));
        }

        public static LevelCatalog Load()
        {
            var manifest = Resources.Load<TextAsset>(Folder + "/manifest");
            if (manifest == null)
            {
                Debug.LogError("Level packs missing: run menu 'Tilt & Drop/Sync Level Packs' (copies shared/levels into Resources).");
                return null;
            }
            return new LevelCatalog(manifest.text, file =>
            {
                var ta = Resources.Load<TextAsset>(ResourceName(file));
                if (ta == null) throw new System.IO.FileNotFoundException(file);
                return ta.bytes;
            });
        }
    }
}
