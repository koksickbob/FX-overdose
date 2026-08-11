using UnityEditor;
using UnityEngine;
using System.IO;

namespace FXOverdose.Editor
{
    public class ClearMemoryEditor
    {
        [MenuItem("FX Overdose/AI/Clear Memory DB (Fix Halucination)")]
        public static void ClearMemory()
        {
            string dir = Path.Combine(Application.persistentDataPath, "DatingSimMemories");
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
                Debug.Log("[FX Overdose] Memory DB successfully cleared! Previous AI hallucinations have been wiped out.");
            }
            else
            {
                Debug.Log("[FX Overdose] Memory DB is already empty or doesn't exist.");
            }
        }
    }
}
