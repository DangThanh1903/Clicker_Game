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
    private const string RedeemedStatusPending = "pending";
    private const string RedeemedStatusApplied = "applied";

    private enum RedeemFlowState
    {
        Error,
        Invalid,
        AlreadyRedeemed,
        PendingApply
    }

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
        RedeemFlowState flowState = RedeemFlowState.Error;

        try
        {
            await db.RunTransactionAsync(async tx =>
            {
                var redeemedSnap = await tx.GetSnapshotAsync(redeemedDoc);
                if (redeemedSnap.Exists)
                {
                    string redeemedStatus = GetRedeemedStatus(redeemedSnap);
                    if (redeemedStatus == RedeemedStatusApplied)
                    {
                        flowState = RedeemFlowState.AlreadyRedeemed;
                        return;
                    }

                    if (TryParsePayload(redeemedSnap, out var pendingPayload) && pendingPayload != null)
                        payload = pendingPayload;

                    flowState = RedeemFlowState.PendingApply;
                    return;
                }

                var codeSnap = await tx.GetSnapshotAsync(codeDoc);
                if (!codeSnap.Exists)
                {
                    flowState = RedeemFlowState.Invalid;
                    return;
                }

                if (!TryParsePayload(codeSnap, out var codePayload) || codePayload == null)
                {
                    flowState = RedeemFlowState.Invalid;
                    return;
                }

                if (!codePayload.enabled)
                {
                    flowState = RedeemFlowState.Invalid;
                    return;
                }

                payload = codePayload;
                tx.Set(redeemedDoc, BuildPendingRedeemedRecord(payload));
                flowState = RedeemFlowState.PendingApply;
            });
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GiftCode] Redeem failed: {ex.Message}");
            return new GiftCodeRedeemResult { status = GiftCodeRedeemStatus.Error, message = "Redeem failed." };
        }

        if (flowState == RedeemFlowState.Invalid)
            return new GiftCodeRedeemResult { status = GiftCodeRedeemStatus.Invalid, message = "Invalid code." };

        if (flowState == RedeemFlowState.AlreadyRedeemed)
            return new GiftCodeRedeemResult { status = GiftCodeRedeemStatus.AlreadyRedeemed, message = "Already redeemed." };

        if (flowState != RedeemFlowState.PendingApply || payload == null)
            return new GiftCodeRedeemResult { status = GiftCodeRedeemStatus.Error, message = "Redeem failed." };

        bool alreadyAppliedLocally = IsRewardAppliedLocally(code);
        bool applied = alreadyAppliedLocally || await TryApplyRewardsAsync(payload);
        if (!applied)
            return new GiftCodeRedeemResult { status = GiftCodeRedeemStatus.Error, message = "Redeem is pending. Please try again." };

        if (!alreadyAppliedLocally)
            MarkRewardAppliedLocally(code);

        try
        {
            await redeemedDoc.SetAsync(new Dictionary<string, object>
            {
                ["status"] = RedeemedStatusApplied,
                ["appliedAt"] = Timestamp.GetCurrentTimestamp()
            }, SetOptions.MergeAll);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GiftCode] Finalize redeem state failed: {ex.Message}");
            return new GiftCodeRedeemResult { status = GiftCodeRedeemStatus.Error, message = "Redeemed locally. Retry to sync." };
        }

        return new GiftCodeRedeemResult { status = GiftCodeRedeemStatus.Success, message = "Redeemed." };
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

    private static async Task<bool> TryApplyRewardsAsync(GiftCodePayload payload)
    {
        if (payload == null) return false;

        if (payload.gems > 0 && StatsManager.Ins == null)
        {
            Debug.LogWarning("[GiftCode] StatsManager is missing. Cannot grant gems now.");
            return false;
        }

        if (payload.items != null && payload.items.Count > 0 && InventoryController.Instance == null)
        {
            Debug.LogWarning("[GiftCode] InventoryController is missing. Cannot grant items now.");
            return false;
        }

        var inventory = InventoryController.Instance;
        List<(AsyncOperationHandle<Item> handle, int quantity)> loadedItems = new List<(AsyncOperationHandle<Item>, int)>();

        try
        {
            if (payload.items != null && payload.items.Count > 0)
            {
                foreach (var it in payload.items)
                {
                    if (string.IsNullOrEmpty(it.itemAddress) || it.quantity <= 0) continue;

                    AsyncOperationHandle<Item> handle = Addressables.LoadAssetAsync<Item>(it.itemAddress);
                    await handle.Task;
                    if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                    {
                        loadedItems.Add((handle, it.quantity));
                    }
                    else
                    {
                        Debug.LogWarning($"[GiftCode] Item not found: {it.itemAddress}");
                        Addressables.Release(handle);
                        return false;
                    }
                }
            }

            var itemsToGrant = new List<InventoryItem>(loadedItems.Count);
            for (int i = 0; i < loadedItems.Count; i++)
            {
                var entry = loadedItems[i];
                var grant = new InventoryItem(entry.handle.Result, entry.quantity);
                itemsToGrant.Add(grant);
            }

            if (itemsToGrant.Count > 0 && (inventory == null || !inventory.CanFullyAddItems(itemsToGrant)))
            {
                Debug.LogWarning("[GiftCode] Inventory has no space for full reward package.");
                return false;
            }

            if (payload.gems > 0)
                StatsManager.Ins.Add(StatType.Diamond, payload.gems);

            for (int i = 0; i < itemsToGrant.Count; i++)
            {
                bool added = inventory.TryAddItemToInventory(itemsToGrant[i], requireFullAdd: true);
                if (!added)
                {
                    Debug.LogWarning("[GiftCode] Failed to add reward item to inventory.");
                    return false;
                }
            }

            return true;
        }
        finally
        {
            foreach (var entry in loadedItems)
            {
                Addressables.Release(entry.handle);
            }
        }
    }

    private static Dictionary<string, object> BuildPendingRedeemedRecord(GiftCodePayload payload)
    {
        List<Dictionary<string, object>> items = new List<Dictionary<string, object>>();
        if (payload?.items != null)
        {
            foreach (var it in payload.items)
            {
                if (string.IsNullOrEmpty(it.itemAddress) || it.quantity <= 0) continue;
                items.Add(new Dictionary<string, object>
                {
                    ["itemAddress"] = it.itemAddress,
                    ["quantity"] = it.quantity
                });
            }
        }

        return new Dictionary<string, object>
        {
            ["status"] = RedeemedStatusPending,
            ["createdAt"] = Timestamp.GetCurrentTimestamp(),
            ["gems"] = Mathf.Max(0, payload?.gems ?? 0),
            ["items"] = items
        };
    }

    private static string GetRedeemedStatus(DocumentSnapshot snap)
    {
        if (snap != null && snap.TryGetValue("status", out string status) && !string.IsNullOrWhiteSpace(status))
            return status.Trim().ToLowerInvariant();
        return RedeemedStatusPending;
    }

    private static bool IsRewardAppliedLocally(string code)
    {
        if (string.IsNullOrEmpty(code)) return false;
        return PlayerPrefs.GetInt(GetLocalReceiptKey(code), 0) == 1;
    }

    private static void MarkRewardAppliedLocally(string code)
    {
        if (string.IsNullOrEmpty(code)) return;
        PlayerPrefs.SetInt(GetLocalReceiptKey(code), 1);
        PlayerPrefs.Save();
    }

    private static string GetLocalReceiptKey(string code)
    {
        return $"GIFT_CODE_APPLIED_{code}";
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
