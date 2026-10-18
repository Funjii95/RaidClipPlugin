export type MinigameId = 'heist' | 'gamble' | 'coinflip' | 'slots' | 'roulette' | 'race' | 'duel';
export type EventPhase = 'inactive' | 'joining' | 'closed' | 'running' | 'result' | 'completed';
export type Connection = 'connecting' | 'connected' | 'disconnected' | 'offline' | 'unauthorized';
export interface HeistParticipant { participantId: string; displayName: string; }
export interface HeistRules {
  enabled: boolean; canStart: boolean; canJoin: boolean; minimumStake: number; maximumStake: number;
  usesStake: boolean; joinSeconds: number; minimumParticipants: number; maximumParticipants: number;
  cooldownSeconds: number; startCommand: string; joinCommand: string;
}
export interface MinigameRules extends HeistRules { id: MinigameId; name: string; description: string; choices: string[]; payoutDescription?: string; }
export interface HeistResult { outcome: 'success' | 'failure' | 'cancelled' | 'breakEven'; multiplier?: number; personalNet?: number; totalPayout: number; totalLoss: number; personalPayout: number; personalLoss: number; message: string; }
export interface HeistStatus {
  eventId: string; version: number; game: MinigameId; name: string; phase: EventPhase;
  startedAt?: number; joinEndsAt?: number; phaseEndsAt?: number; cooldownEndsAt?: number;
  participants: HeistParticipant[]; participantCount: number; joined: boolean; personalStake: number;
  betChoice?: string; requiredStake?: number; totalStake?: number; jackpot?: number; presentation?: string[]; result?: HeistResult; challenge?: { challenger: string; target: string; canAccept: boolean };
}
// Identity always comes from a verified server session; requests contain no trusted user ID.
export interface HeistJoinRequest { requestId: string; eventId: string; expectedVersion: number; stake: number; }
export interface HeistStartRequest { requestId: string; stake: number; }
export interface MinigameActionRequest {
  requestId: string; game: MinigameId; action: 'start' | 'join' | 'play' | 'challenge' | 'accept' | 'deny';
  eventId?: string; expectedVersion?: number; stake: number; choice?: string; targetLogin?: string;
}
export interface HeistEvent { streamId: string; revision: number; serverTime: number; states: HeistStatus[]; balance: number; }
export interface HeistJoinResult { requestId: string; accepted: boolean; snapshot: HeistEvent; }
export type HeistStartResult = HeistJoinResult;
export interface MinigameClient {
  readonly mode: 'demo' | 'live';
  getRules(): Promise<MinigameRules[]>;
  getSnapshot(): Promise<HeistEvent>;
  execute(request: MinigameActionRequest): Promise<HeistJoinResult>;
  subscribe(listener: (event: HeistEvent) => void, disconnected: (reason: Connection) => void): () => void;
}
export class MinigameError extends Error {
  constructor(readonly code: Connection | 'invalid' | 'expired' | 'duplicate' | 'unavailable', message: string) { super(message); }
}
export interface FixtureWallet { balance(): number; settle(requestId: string, stake: number, payout: number): Promise<void>; }
