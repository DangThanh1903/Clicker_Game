using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public enum GiftCodeRedeemStatus
{
    Success,
    Invalid,
    AlreadyRedeemed,
    NotReady,
    Error
}

public struct GiftCodeRedeemResult
{
    public GiftCodeRedeemStatus status;
    public string message;
}

public static class GiftCodeService
{
    // Firestore schema:
    // giftcodes/{CODE}:
    //   enabled: bool
    //   gems: number
    //   items: [ { itemAddress: string, quantity: number } ]
    //
    // users/{uid}/giftcodes/{CODE}:
    //   redeemedAt: server timestamp

    public static async Task<GiftCodeRedeemResult> RedeemAsync(string codeRaw)
    {
        string code = Normalize(codeRaw);
        if (string.IsNullOrEmpty(code))
            return new GiftCodeRedeemResult { status = GiftCodeRedeemStatus.Invalid, message = "Invalid code." };

        var bootstrap = FirebaseBootstrap.Ins;
        if (bootstrap == null)
            return new GiftCodeRedeemResult { status = GiftCodeRedeemStatus.NotReady, message = "No connection." };

        try
        {
            await bootstrap.ReadyTask;
        }
        catch
        {
            return new GiftCodeRedeemResult { status = GiftCodeRedeemStatus.NotReady, message = "No connection." };
        }

        if (!bootstrap.IsReady || bootstrap.Db == null || string.IsNullOrEmpty(bootstrap.Uid))
            return new GiftCodeRedeemResult { status = GiftCodeRedeemStatus.NotReady, message = "No connection." };

        var db = bootstrap.Db;
        var codeDoc = db.Collection("giftcodes").Document(code);
        var redeemedDoc = db.Collection("users").Document(bootstrap.Uid).Collection("giftcodes").Document(code);

        GiftCodePayload payload = null;
        GiftCodeRedeemStatus status = GiftCodeRedeemStatus.Error;

        try
        {
            await db.RunTransactionAsync(async tx =>
            {
                var codeSnap = await tx.GetSnapshotAsync(codeDoc);
                if (!codeSnap.Exists)
                {
                    status = GiftCodeRedeemStatus.Invalid;
                    return;
                }

                if (!TryParsePayload(codeSnap, out payload) || payload == null)
                {
                    status = GiftCodeRedeemStatus.Invalid;
                    return;
                }

                if (!payload.enabled)
                {
                    status = GiftCodeRedeemStatus.Invalid;
                    return;
                }

                var redeemedSnap = await tx.GetSnapshotAsync(redeemedDoc);
                if (redeemedSnap.Exists)
                {
                    status = GiftCodeRedeemStatus.AlreadyRedeemed;
                    return;
                }

                tx.Set(redeemedDoc, new Dictionary<string, object>
                {
                    ["redeemedAt"] = Timestamp.GetCurrentTimestamp()
                });

                status = GiftCodeRedeemStatus.Success;
            });
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GiftCode] Redeem failed: {ex.Message}");
            return new GiftCodeRedeemResult { status = GiftCodeRedeemStatus.Error, message = "Redeem failed." };
        }

        if (status == GiftCodeRedeemStatus.Success)
        {
            await ApplyRewardsAsync(payload);
            return new GiftCodeRedeemResult { status = status, message = "Redeemed." };
        }

        return status switch
        {
            GiftCodeRedeemStatus.AlreadyRedeemed => new GiftCodeRedeemResult { status = status, message = "Already redeemed." },
            GiftCodeRedeemStatus.Invalid => new GiftCodeRedeemResult { status = status, message = "Invalid code." },
            _ => new GiftCodeRedeemResult { status = status, message = "Redeem failed." }
        };
    }

    private static string Normalize(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        return code.Trim().Replace(" ", "").ToUpperInvariant();
    }

    private static bool TryParsePayload(DocumentSnapshot snap, out GiftCodePayload payload)
    {
        payload = null;
        if (snap == null || !snap.Exists) return false;

        bool enabled = true;
        if (snap.TryGetValue("enabled", out bool enabledVal))
            enabled = enabledVal;

        int gems = 0;
        if (snap.TryGetValue("gems", out long gemsLong))
            gems = Convert.ToInt32(gemsLong);
        else if (snap.TryGetValue("gems", out int gemsInt))
            gems = gemsInt;

        var items = new List<GiftCodeItemReward>();
        if (snap.TryGetValue("items", out IEnumerable<object> rawItems))
        {
            foreach (var raw in rawItems)
            {
                if (raw is Dictionary<string, object> dict)
                {
                    string address = null;
                    if (dict.TryGetValue("itemAddress", out var addrObj))
                        address = addrObj?.ToString();
                    else if (dict.TryGetValue("itemId", out var idObj))
                        address = idObj?.ToString();

                    int qty = 1;
                    if (dict.TryGetValue("quantity", out var qObj))
                    {
                        if (qObj is long qLong) qty = Convert.ToInt32(qLong);
                        else if (qObj is int qInt) qty = qInt;
                    }

                    if (!string.IsNullOrEmpty(address) && qty > 0)
                        items.Add(new GiftCodeItemReward { itemAddress = address, quantity = qty });
                }
            }
        }

        payload = new GiftCodePayload
        {
            enabled = enabled,
            gems = gems,
            items = items
        };

        return true;
    }

    private static async Task ApplyRewardsAsync(GiftCodePayload payload)
    {
        if (payload == null) return;

        if (payload.gems > 0)
            StatsManager.Ins?.Add(StatType.Diamond, payload.gems);

        if (payload.items == null || payload.items.Count == 0)
            return;

        foreach (var it in payload.items)
        {
            if (string.IsNullOrEmpty(it.itemAddress) || it.quantity <= 0) continue;

            AsyncOperationHandle<Item> handle = Addressables.LoadAssetAsync<Item>(it.itemAddress);
            await handle.Task;
            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                var invItem = new InventoryItem(handle.Result, it.quantity);
                InventoryController.Instance?.AddItemToInventory(invItem);
            }
            else
            {
                Debug.LogWarning($"[GiftCode] Item not found: {it.itemAddress}");
            }

            Addressables.Release(handle);
        }
    }

    private class GiftCodePayload
    {
        public bool enabled;
        public int gems;
        public List<GiftCodeItemReward> items;
    }

    private struct GiftCodeItemReward
    {
        public string itemAddress;
        public int quantity;
    }
}
