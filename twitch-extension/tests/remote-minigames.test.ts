import {it,expect,vi} from 'vitest';
import {RemoteMinigameClient} from '../src/minigames/remote-client';
import {PluginRuleError} from '../src/remote/errors';
import type {BackendRpgDataSource} from '../src/remote/BackendRpgDataSource';
const games=['gamble','coinflip','slots','roulette'];
function payload(){return {rules:games.map(id=>({id,name:id,enabled:true,minimumStake:1,maximumStake:1000,choices:[]})),frame:{streamId:'session',revision:1,serverTime:Date.now(),balance:1000,states:games.map(game=>({game,version:1,personalStake:0,phase:'inactive',participants:[]}))}};}
function fixture(){let player='1001';const readPlugin=vi.fn().mockResolvedValue(payload()),refresh=vi.fn().mockResolvedValue(undefined);const source={getSnapshot:()=>({player:{id:player},remote:{connection:'ready'}}),readPlugin,refresh} as unknown as BackendRpgDataSource;return {client:new RemoteMinigameClient(source),readPlugin,refresh,changePlayer:()=>{player='1002';}};}
it('shares status reads, sends the exact controller ActionId, then refreshes the authoritative profile',async()=>{
 const f=fixture();await Promise.all([f.client.getRules(),f.client.getSnapshot()]);expect(f.readPlugin).toHaveBeenCalledTimes(1);
 const id='11111111-1111-4111-8111-111111111111';await f.client.execute({requestId:id,game:'coinflip',action:'play',stake:100,choice:'kopf'});
 expect(f.readPlugin).toHaveBeenLastCalledWith('casino',{game:'coinflip',stake:'100',choice:'kopf'},id);expect(f.refresh).toHaveBeenCalledWith(true);
});
it('preserves uncertain transport failures and classifies confirmed refusals separately',async()=>{
 const f=fixture(),req={requestId:'11111111-1111-4111-8111-111111111111',game:'slots',action:'play',stake:100} as const;
 f.readPlugin.mockRejectedValueOnce(Error('lost acknowledgement'));await expect(f.client.execute(req)).rejects.toThrow('lost acknowledgement');
 f.readPlugin.mockRejectedValueOnce(new PluginRuleError('Zu wenig Perlen'));await expect(f.client.execute(req)).rejects.toMatchObject({code:'invalid'});expect(f.refresh).not.toHaveBeenCalled();
});
it('rejects unsafe balances, malformed rules and a response belonging to a previous player',async()=>{
 for(const change of [(p:any)=>p.frame.balance='9007199254740993',(p:any)=>p.rules[0].maximumStake=-1,(p:any)=>p.frame.states[0].phase='invented']){
  const f=fixture(),p=payload();change(p);f.readPlugin.mockResolvedValueOnce(p);await expect(f.client.getSnapshot()).rejects.toThrow();
 }
 const f=fixture();f.readPlugin.mockImplementationOnce(async()=>{f.changePlayer();return payload();});await expect(f.client.getRules()).rejects.toThrow('Spieler gewechselt');
});
it('does not send unsupported shared-player actions or unsafe stakes',async()=>{
 const f=fixture();await expect(f.client.execute({requestId:'x',game:'heist',action:'start',stake:100,targetLogin:'someone'})).rejects.toMatchObject({code:'unavailable'});
 await expect(f.client.execute({requestId:'x',game:'slots',action:'play',stake:9007199254740992})).rejects.toMatchObject({code:'invalid'});expect(f.readPlugin).not.toHaveBeenCalled();
});

it('sends shared game operations with an event binding and the original ActionId',async()=>{
 const f=fixture();const requestId='11111111-1111-4111-8111-111111111111';
 await f.client.execute({requestId,game:'heist',action:'join',eventId:'current-round',stake:0});
 expect(f.readPlugin).toHaveBeenLastCalledWith('community',{game:'heist',operation:'join',eventId:'current-round',stake:'0',target:'-'},requestId);
});
