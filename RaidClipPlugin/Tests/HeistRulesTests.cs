using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class HeistRulesTests
{
    [Theory]
    [InlineData(0,1,false)]
    [InlineData(1,1,true)]
    [InlineData(1,2,false)]
    [InlineData(50,50,true)]
    [InlineData(50,51,false)]
    [InlineData(100,100,true)]
    public void SuccessChanceUsesInclusiveW100(int chance,int roll,bool expected) =>
        Assert.Equal(expected,HeistRules.IsSuccess(chance,roll));

    [Theory]
    [InlineData(2,3)]
    [InlineData(3,3)]
    [InlineData(1000,3)]
    [InlineData(7,5)]
    public void PayoutDistributesCompleteJackpot(int jackpot,int participants)
    {
        var remainder=jackpot%participants;
        var payouts=HeistRules.CalculatePayouts(jackpot,participants,Enumerable.Range(0,remainder).ToArray());
        Assert.Equal(participants,payouts.Length);
        Assert.Equal(jackpot,payouts.Sum());
        Assert.True(payouts.Max()-payouts.Min()<=1);
    }

    [Fact]
    public async Task StorePaysEveryPointAndResetsJackpotAtomically()
    {
        var directory=Path.Combine(Path.GetTempPath(),"raidclip-heist-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new ViewerPointStore(directory);
            using var cts=new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var users=new[]{("1","Alpha"),("2","Beta"),("3","Gamma")};
            foreach(var user in users)await store.SetPointsAsync(user.Item1,user.Item2,0,0,cts.Token);
            var result=await store.PayoutHeistJackpotAsync(users,new[]{0},1000,true,500,cts.Token);
            Assert.Equal(1000,result.Payouts.Sum(x=>x.Payout));
            Assert.Equal(1000,result.JackpotAfter);
            Assert.Equal(new[]{334,333,333},result.Payouts.Select(x=>x.Payout));
        }
        finally { if(Directory.Exists(directory))Directory.Delete(directory,true); }
    }
}
