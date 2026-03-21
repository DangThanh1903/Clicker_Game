using System;

public sealed class FriendListItemView : FriendEntryView
{
    public void Bind(
        FriendLinkData data,
        Action<string> onProfile,
        Action<string> onGift,
        Action<string> onRemove)
    {
        BindFriendRow(data, onProfile, onGift, onRemove);
    }
}
