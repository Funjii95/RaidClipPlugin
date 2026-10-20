using Google.Api.Gax;
using Google.Cloud.Firestore;
using SnapshotFirestore;
using SnapshotProtocol;

namespace SnapshotPublisher;

public sealed class PublisherSettings
{
    public string Mode { get; set; } = "";
    public string ChannelId { get; set; } = "";
    public string ApiKeyHash { get; set; } = "";
    public string StoreDirectory { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string DatabaseId { get; set; } = "(default)";
    public string Collection { get; set; } = "";
    public bool WritesEnabled { get; set; }
    public bool PrivacyDeletionEnabled {get;set;}
    public bool AutomaticPlayerEnrollment {get;set;}
    public string[] AllowedUserIds { get; set; } = [];

    public void Validate()
    {
        if (ChannelId.Length is < 1 or > 25 || ChannelId.Any(c => c is < '0' or > '9') ||
            ApiKeyHash.Length != 64 || ApiKeyHash.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidOperationException("Publisher channel and credential hash required.");
        if (AutomaticPlayerEnrollment && Mode != "FirestoreProfilesTest") throw new InvalidOperationException("Enrollment requires the protected profile publisher.");
        if (Mode == "LocalPrototype")
        {
            if (string.IsNullOrWhiteSpace(StoreDirectory)) throw new InvalidOperationException("Explicit local directory required.");
            return;
        }
        if (Mode == "FirestoreProfilesTest")
        {
            if (ProjectId != "funjii-otter-rpg" || DatabaseId != "(default)" || Collection != "rpgReadSnapshots" ||
                ChannelId != "42812664" || AllowedUserIds.Length is < 1 or > 100 || AllowedUserIds.Distinct().Count() != AllowedUserIds.Length ||
                AllowedUserIds.Any(id => id.Length is < 1 or > 25 || id.Any(c => c is < '0' or > '9')))
                throw new InvalidOperationException("Explicit unique test scope of 1 to 100 accounts required.");
            return;
        }
        // The original synthetic mode keeps its original scope.
        if (Mode != "FirestoreTest" || ProjectId != "funjii-otter-rpg" || DatabaseId != "(default)" ||
            Collection != "rpgSnapshotPipelineTest" || ChannelId != "42" ||
            AllowedUserIds.Length != 1 || AllowedUserIds[0] != "1001")
            throw new InvalidOperationException("Only the explicitly scoped synthetic Firestore test is supported.");
    }

    public ISnapshotSink CreateSink() => Mode == "LocalPrototype"
        ? new LocalSnapshotStore(StoreDirectory, ChannelId)
        : new FirestoreSnapshotSink(new FirestoreDbBuilder
        {
            ProjectId = ProjectId, DatabaseId = DatabaseId, EmulatorDetection = EmulatorDetection.ProductionOnly
        }.Build(), ChannelId, Collection, sequentialUpdatesOnly: Mode == "FirestoreProfilesTest",allowSequentialDeletes:PrivacyDeletionEnabled,allowInitialProfiles:AutomaticPlayerEnrollment);
}
