using UnityEngine;
using System;

public class CaseRollController : MonoBehaviour
{
    public CaseRollerUI rollerUI;

    public Lootbox simpleLootBox;

    // Optional deterministic RNG (seed from server for anti-cheat)
    private System.Random rng;

    private void Awake()
    {
        rng = new System.Random(Environment.TickCount);
        rollerUI.OnLanded += OnLanded;
    }

    void Start()
    {
        UseLootbox(simpleLootBox);
    }
    // Call this from your “Use” button with the selected Lootbox asset
    public void UseLootbox(Lootbox box)
    {
        // 1) Real roll (this should be server-authoritative in production)
        var roll = box.RollOne(rng); // (Item item, int amount)

        // 2) Build CS-style reel (cosmetic) with the result placed near the end
        var payload = CaseReelBuilder.Build(box, roll, totalSlots: 60, visibleWindow: rollerUI.visibleWindow, rng: rng);

        // 3) Spin UI
        rollerUI.BuildAndSpin(payload);

        // NOTE: don't grant here; wait for OnLanded for perfect sync with the stop
        // If you must be secure, grant on server immediately and only mirror visuals here.
    }

    private void OnLanded(Item item, int amount)
    {
        // Grant to inventory (hook into your existing system)
        // InventoryController.Instance.AddItemToInventory(new InventoryItem(item, amount));

        Debug.Log($"🎉 Landed: {item.GetColoredName()} x{amount}");
        // TODO: Play rarity sting VFX/SFX here based on item.rarity
    }

    public void Skip()
    {
        rollerUI.Skip(); // instantly land and fire OnLanded
    }
}
