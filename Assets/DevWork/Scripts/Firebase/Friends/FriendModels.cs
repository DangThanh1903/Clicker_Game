using Firebase.Firestore;

public enum FriendOpStatus
{
    Success,
    NotReady,
    InvalidInput,
    TargetNotFound,
    AlreadyFriends,
    AlreadyRequested,
    IncomingRequestExists,
    RequestNotFound,
    NotFriends,
    GiftAlreadySentToday,
    GiftNotFound,
    GiftAlreadyClaimed,
    MissingRuntimeDependency,
    Error
}

public struct FriendOpResult
{
    public FriendOpStatus status;
    public string message;
    public int diamondsGranted;
}

public class FriendPublicProfile
{
    public string uid;
    public string displayName;
    public string avatarId;
    public float clicks;
    public float totalPlaytime;
    public string currentBlock;
    public string currentLocation;
}

[FirestoreData]
public class FriendLinkData
{
    [FirestoreProperty] public string uid { get; set; }
    [FirestoreProperty] public string displayName { get; set; }
    [FirestoreProperty] public Timestamp sinceAt { get; set; }
    [FirestoreProperty] public Timestamp updatedAt { get; set; }
}

[FirestoreData]
public class FriendRequestData
{
    [FirestoreProperty] public string uid { get; set; }
    [FirestoreProperty] public string displayName { get; set; }
    [FirestoreProperty] public Timestamp createdAt { get; set; }
}

[FirestoreData]
public class FriendGiftData
{
    [FirestoreProperty] public string giftId { get; set; }
    [FirestoreProperty] public string fromUid { get; set; }
    [FirestoreProperty] public string fromDisplayName { get; set; }
    [FirestoreProperty] public int diamonds { get; set; }
    [FirestoreProperty] public string dayKey { get; set; }
    [FirestoreProperty] public Timestamp createdAt { get; set; }
    [FirestoreProperty] public string status { get; set; }
    [FirestoreProperty] public Timestamp claimedAt { get; set; }
}

[FirestoreData]
public class FriendGiftStateData
{
    [FirestoreProperty] public string fromUid { get; set; }
    [FirestoreProperty] public string toUid { get; set; }
    [FirestoreProperty] public string dayKey { get; set; }
    [FirestoreProperty] public Timestamp createdAt { get; set; }
}
