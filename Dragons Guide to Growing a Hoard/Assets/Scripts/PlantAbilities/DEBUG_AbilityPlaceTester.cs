using UnityEngine;
using UnityEngine.InputSystem;

/* Press 1, place four leaves forming a closed ring 
(e.g. the four cells surrounding a single empty cell) — each leaf placement calls Recalculate(), 
o the circuit updates live as you place each one. */
/* 
Waterbell — same setup we already worked through: press 2, hover a valid cell adjacent to a planted pot, 
left-click to place.
Lower that pot's water below targetWaterLevel and watch it climb back up every tickInterval seconds.
 */
public class DEBUG_AbilityPlaceTester : MonoBehaviour
{
    [SerializeField] private AbilityPlacementSystem abilityPlacementSystem;
    [SerializeField] private PlayerAbilityInventory abilityInventory;
    [SerializeField] private AbilityItemData sparkmintLeafData;
    [SerializeField] private AbilityItemData waterbellData;

    private void Start()
    {
        // Give yourself stock for testing — remove once real harvest-grant flow exists
        abilityInventory.Add(sparkmintLeafData, 25);
        abilityInventory.Add(waterbellData, 3);
    }

    private void Update()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            abilityPlacementSystem.BeginPlacing(sparkmintLeafData);
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            abilityPlacementSystem.BeginPlacing(waterbellData);
    }
}