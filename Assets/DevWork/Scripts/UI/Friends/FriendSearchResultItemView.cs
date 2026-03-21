using System;
using UnityEngine;

public sealed class FriendSearchResultItemView : FriendEntryView
{
    public void Bind(FriendPublicProfile profile, Sprite avatarSprite, bool canAdd, Action<string> onAdd, Action<string> onProfile)
    {
        BindSearchRow(profile, avatarSprite, canAdd, onAdd, onProfile);
    }
}
