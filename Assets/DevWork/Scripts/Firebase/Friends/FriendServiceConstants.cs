internal static class FriendServiceConstants
{
    public const string UsersCollection = "users";
    public const string LeaderboardsCollection = "leaderboards";

    public const string FriendsCollection = "friends";
    public const string RequestsInCollection = "friend_requests_in";
    public const string RequestsOutCollection = "friend_requests_out";
    public const string GiftsInCollection = "friend_gifts_in";
    public const string GiftStateCollection = "friend_gift_state";

    public const string GiftStatusPending = "pending";
    public const string GiftStatusClaimed = "claimed";

    public const int DefaultGiftDiamonds = 5;
    public const int DefaultQueryLimit = 50;
    public const int DefaultAddFriendListLimit = 7;
    public const int MaxQueryLimit = 200;
}
