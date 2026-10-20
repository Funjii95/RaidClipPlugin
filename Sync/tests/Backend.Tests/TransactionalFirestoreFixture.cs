using System.Net;
using Backend;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using SnapshotFirestore;

namespace Backend.Tests;

// A small optimistic transaction transport double, NOT a complete Firestore emulator or IAM test.
public sealed class TransactionalFirestoreFixture(WebApplication app, GrpcChannel transport,
    TransactionalFirestoreFixture.Service service, FirestoreDb database) : IAsyncDisposable
{
    public Service Server => service;
    public FirestoreDb Database => database;
    public FirestoreSnapshotSink Sink => new(database, "42");
    public FirestorePlayerReader Reader => new(database, "rpgReadSnapshots");
    public const string DocumentName = "projects/demo-funjii-transactions/databases/(default)/documents/rpgReadSnapshots/1001";

    public static async Task<TransactionalFirestoreFixture> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0, e => e.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        var service = new Service();
        builder.Services.AddSingleton(service);
        var app = builder.Build();
        app.MapGrpcService<Service>();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var transport = GrpcChannel.ForAddress(address);
        var client = new FirestoreClientBuilder { CallInvoker = transport.CreateCallInvoker() }.Build();
        return new(app, transport, service, FirestoreDb.Create("demo-funjii-transactions", client));
    }
    public async ValueTask DisposeAsync()
    {
        transport.Dispose();
        await app.StopAsync();
        await app.DisposeAsync();
    }

    public sealed class Service : Firestore.FirestoreBase
    {
        private readonly object gate = new();
        private readonly Dictionary<string, Document> documents = [];
        private readonly Dictionary<string, long> versions = [];
        private readonly Dictionary<ByteString, Dictionary<string, long>> transactions = [];
        private int abortNext;
        private int begins;
        private int commits;
        private int writes;
        public bool DenyAccess { get; set; }
        public int BeginCount => Volatile.Read(ref begins);
        public int CommitCount => Volatile.Read(ref commits);
        public int WriteCount => Volatile.Read(ref writes);
        public void AbortNextCommit() => Interlocked.Exchange(ref abortNext, 1);
        public Document? Peek() { lock (gate) return documents.GetValueOrDefault(DocumentName)?.Clone(); }
        public void Seed(Document document)
        {
            lock (gate)
            {
                documents[document.Name] = document.Clone();
                versions[document.Name] = versions.GetValueOrDefault(document.Name) + 1;
            }
        }
        private void Authorize()
        {
            if (DenyAccess) throw new RpcException(new Status(StatusCode.PermissionDenied, "synthetic denial"));
        }
        public override Task<BeginTransactionResponse> BeginTransaction(BeginTransactionRequest request, ServerCallContext context)
        {
            Authorize();
            Interlocked.Increment(ref begins);
            var id = ByteString.CopyFromUtf8(Guid.NewGuid().ToString());
            lock (gate) transactions.Add(id, []);
            return Task.FromResult(new BeginTransactionResponse { Transaction = id });
        }
        public override async Task BatchGetDocuments(BatchGetDocumentsRequest request,
            IServerStreamWriter<BatchGetDocumentsResponse> stream, ServerCallContext context)
        {
            Authorize();
            foreach (var name in request.Documents)
            {
                BatchGetDocumentsResponse response;
                lock (gate)
                {
                    if (!request.Transaction.IsEmpty)
                    {
                        if (!transactions.TryGetValue(request.Transaction, out var reads))
                            throw new RpcException(new Status(StatusCode.FailedPrecondition, "unknown transaction"));
                        reads[name] = versions.GetValueOrDefault(name);
                    }
                    var time = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                    response = new BatchGetDocumentsResponse { ReadTime = time };
                    if (documents.TryGetValue(name, out var doc))
                    {
                        response.Found = doc.Clone();
                        response.Found.CreateTime = time;
                        response.Found.UpdateTime = time;
                    }
                    else response.Missing = name;
                }
                await stream.WriteAsync(response);
            }
        }
        public override Task<CommitResponse> Commit(CommitRequest request, ServerCallContext context)
        {
            Authorize();
            Interlocked.Increment(ref commits);
            lock (gate)
            {
                var direct=request.Transaction.IsEmpty;
                if (!transactions.Remove(request.Transaction, out var reads)&&!direct)
                    throw new RpcException(new Status(StatusCode.FailedPrecondition, "transaction required"));
                if(direct)reads=request.Writes.ToDictionary(w=>w.Update.Name,w=>versions.GetValueOrDefault(w.Update.Name));
                if (Interlocked.Exchange(ref abortNext, 0) == 1 ||
                    reads!.Any(r => versions.GetValueOrDefault(r.Key) != r.Value))
                    throw new RpcException(new Status(StatusCode.Aborted, "synthetic concurrent modification"));
                var response = new CommitResponse
                    { CommitTime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow) };
                foreach (var write in request.Writes)
                {
                    Assert.Equal(Google.Cloud.Firestore.V1.Write.OperationOneofCase.Update, write.OperationCase);
                    Assert.Null(write.UpdateMask); // Full replace, never a merge retaining old account data.
                    Assert.Contains(write.Update.Name, reads!.Keys);
                    documents[write.Update.Name] = write.Update.Clone();
                    // Match the real service's microsecond precision for stored timestamps.
                    foreach (var field in documents[write.Update.Name].Fields.Values)
                        if (field.ValueTypeCase == Google.Cloud.Firestore.V1.Value.ValueTypeOneofCase.TimestampValue)
                            field.TimestampValue.Nanos = field.TimestampValue.Nanos / 1000 * 1000;
                    versions[write.Update.Name] = versions.GetValueOrDefault(write.Update.Name) + 1;
                    Interlocked.Increment(ref writes);
                    response.WriteResults.Add(new Google.Cloud.Firestore.V1.WriteResult { UpdateTime = response.CommitTime });
                }
                return Task.FromResult(response);
            }
        }
        public override async Task RunQuery(RunQueryRequest request,IServerStreamWriter<RunQueryResponse> stream,ServerCallContext context)
        {
            Authorize();var query=request.StructuredQuery;var from=Assert.Single(query.From);
            Assert.False(from.AllDescendants);Assert.Equal("userId",query.Where.FieldFilter.Field.FieldPath);
            Assert.Equal(StructuredQuery.Types.FieldFilter.Types.Operator.Equal,query.Where.FieldFilter.Op);
            var user=query.Where.FieldFilter.Value.StringValue;Document[] matches;
            lock(gate)matches=documents.Values.Where(d=>d.Name.StartsWith(request.Parent+"/"+from.CollectionId+"/",StringComparison.Ordinal)&&d.Fields.TryGetValue("userId",out var v)&&v.StringValue==user).Take(200).Select(d=>d.Clone()).ToArray();
            foreach(var doc in matches)await stream.WriteAsync(new RunQueryResponse{Document=doc,ReadTime=Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow)});
            if(matches.Length==0)await stream.WriteAsync(new RunQueryResponse{ReadTime=Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow)});
        }
        public override Task<Empty> Rollback(RollbackRequest request, ServerCallContext context)
        {
            lock (gate) transactions.Remove(request.Transaction);
            return Task.FromResult(new Empty());
        }
    }
}
