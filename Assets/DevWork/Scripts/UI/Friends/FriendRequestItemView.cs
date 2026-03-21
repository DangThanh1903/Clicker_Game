using System;

public sealed class FriendRequestItemView : FriendEntryView
{
    public enum RequestItemMode
    {
        Incoming,
        Outgoing
    }

    public void Bind(
        FriendRequestData data,
        RequestItemMode mode,
        Action<string> onAccept,
        Action<string> onReject,
        Action<string> onCancel,
        Action<string> onProfile)
    {
        bool incoming = mode == RequestItemMode.Incoming;
        BindRequestRow(data, incoming, onAccept, onReject, onCancel, onProfile);
    }
}
