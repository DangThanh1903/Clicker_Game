using System;
using System.Collections.Generic;
using UnityEngine;

public static class CaseReelBuilder
{
    /// <summary>
    /// Creates a visual reel for a CS-style animation.
    /// The real result is inserted at a targetIndex close to the end.
    /// </summary>
    public static CaseRollPayload Build(Lootbox box, (Item item, int amount) roll, int totalSlots = 60, int visibleWindow = 7, System.Random rng = null)
    {
        if (rng == null) rng = new System.Random(Environment.TickCount);
        totalSlots = Mathf.Max(visibleWindow + 5, totalSlots);

        // 1) Make a decoy sampler using the same weights as Lootbox to feel consistent.
        //    We'll sample options to fill the reel (but they don't affect the *real* outcome).
        var decoys = new List<Item>(totalSlots);
        for (int i = 0; i < totalSlots; i++)
        {
            var (decoyItem, _) = box.RollOne(rng); // reuse weighted pick; amount ignored for decoy visuals
            decoys.Add(decoyItem ?? roll.item);
        }

        // 2) Decide landing index so the result appears centered in the viewport at the end.
        //    Put target a few items from the end, leaving some tail for bounce polish.
        int tail = Mathf.Max(2, visibleWindow / 2);
        int targetIndex = Mathf.Clamp(totalSlots - tail - 1, visibleWindow, totalSlots - 1);

        // 3) Insert the real result at targetIndex (replace whatever is there).
        decoys[targetIndex] = roll.item;

        return new CaseRollPayload
        {
            sourceBox = box,
            item = roll.item,
            amount = roll.amount,
            targetIndex = targetIndex,
            reel = decoys
        };
    }
}

public struct CaseRollPayload
{
    public Lootbox sourceBox;
    public Item item;       // the real result
    public int amount;      // stack result
    public int targetIndex; // where the reel should stop
    public List<Item> reel; // visual list to render/scroll
}