using Firebase.Firestore;

internal static class FriendFirestoreRefs
{
    public static DocumentReference UserDoc(FirebaseFirestore db, string uid)
    {
        return db.Collection(FriendServiceConstants.UsersCollection).Document(uid);
    }

    public static CollectionReference FriendsCol(FirebaseFirestore db, string ownerUid)
    {
        return UserDoc(db, ownerUid).Collection(FriendServiceConstants.FriendsCollection);
    }

    public static DocumentReference FriendDoc(FirebaseFirestore db, string ownerUid, string friendUid)
    {
        return FriendsCol(db, ownerUid).Document(friendUid);
    }

    public static DocumentReference IncomingRequestDoc(FirebaseFirestore db, string ownerUid, string fromUid)
    {
        return UserDoc(db, ownerUid).Collection(FriendServiceConstants.RequestsInCollection).Document(fromUid);
    }

    public static DocumentReference OutgoingRequestDoc(FirebaseFirestore db, string ownerUid, string toUid)
    {
        return UserDoc(db, ownerUid).Collection(FriendServiceConstants.RequestsOutCollection).Document(toUid);
    }

    public static CollectionReference GiftsInCol(FirebaseFirestore db, string ownerUid)
    {
        return UserDoc(db, ownerUid).Collection(FriendServiceConstants.GiftsInCollection);
    }

    public static DocumentReference GiftInDoc(FirebaseFirestore db, string ownerUid, string giftId)
    {
        return GiftsInCol(db, ownerUid).Document(giftId);
    }

    public static DocumentReference GiftStateDoc(FirebaseFirestore db, string ownerUid, string stateId)
    {
        return UserDoc(db, ownerUid).Collection(FriendServiceConstants.GiftStateCollection).Document(stateId);
    }
}
