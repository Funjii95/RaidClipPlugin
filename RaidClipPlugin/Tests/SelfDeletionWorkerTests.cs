using RaidClipPlugin.Services;
using SnapshotProtocol;
using Xunit;
namespace RaidClipPlugin.Tests;
public sealed class SelfDeletionWorkerTests
{
    [Fact]public async Task Defers_until_stopped_resumes_after_restart_and_never_acknowledges_before_cloud_erasure(){
        var root=Path.Combine(Path.GetTempPath(),"otter-self-delete-"+Guid.NewGuid());
        try{
            var store=new ViewerPointStore(root);await store.SetPointsAsync("1001","SYNTHETIC",500,0,default);await store.SetPointsAsync("1002","OTHER",600,0,default);
            var admission=new StreamAdmission();store.Admission=admission;admission.Begin(()=>true);
            var job=new SelfDeletionJob("1001",Guid.NewGuid(),DateTimeOffset.UtcNow);var acknowledgements=0;
            SelfDeletionWorker Worker(ViewerPointStore s)=>new(s,new HashSet<string>{"1001"},_=>Task.FromResult(new[]{job}),(_,_)=>{acknowledgements++;return Task.CompletedTask;});
            await Worker(store).TickAsync(default);Assert.Null(await store.ReadPrivacyRequestAsync("1001"));
            admission.End();await Worker(store).TickAsync(default,allowLocalErasure:false);Assert.Null(await store.ReadPrivacyRequestAsync("1001"));
            await Worker(store).TickAsync(default);Assert.True((await store.ReadPrivacyRequestAsync("1001"))!.LocalErased);Assert.Equal(0,acknowledgements);
            store=new ViewerPointStore(root);await Worker(store).TickAsync(default);Assert.Equal(0,acknowledgements);
            await store.MarkPrivacyCloudErasedAsync("1001",default);await Worker(store).TickAsync(default);Assert.Equal(1,acknowledgements);
            Assert.Equal(600,await store.GetPointsAsync("1002",default));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>store.AddPointsAsync("1001","SYNTHETIC",50,0,default));
        }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
