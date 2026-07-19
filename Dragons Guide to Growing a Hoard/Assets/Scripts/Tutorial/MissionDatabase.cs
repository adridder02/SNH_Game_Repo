using System.Collections.Generic;
using UnityEngine;

// =============================================================
// MissionDatabase.cs
// -------------------------------------------------------------
// The full ordered list of missions shown as buttons down the left
// side of the Journal's Guide page. One asset for the whole game —
// drag every MissionData asset into `missions`, in the order you
// want the buttons to appear.
//
// SETUP:
//   Create > Guide > Mission Database
//   Assign it on JournalUIController's Guide Page section.
// =============================================================
[CreateAssetMenu(fileName = "MissionDatabase", menuName = "Guide/Mission Database")]
public class MissionDatabase : ScriptableObject
{
    public List<MissionData> missions = new List<MissionData>();
}
