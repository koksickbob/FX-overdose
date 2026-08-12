using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FXOverdose.P2P.Core
{
    public enum P2PCompetitionAction : byte { BuyItem, UseItem, ChooseEvent }

    public readonly struct P2PCompetitionPlayerSnapshot
    {
        public P2PCompetitionPlayerSnapshot(ulong id, double health, double mental, bool eliminated,
            P2PEliminationReason reason, int water, int medicine, int comfort)
        { PlayerId=id; Health=health; Mental=mental; IsEliminated=eliminated; Reason=reason; Water=water; Medicine=medicine; Comfort=comfort; }
        public ulong PlayerId { get; } public double Health { get; } public double Mental { get; }
        public bool IsEliminated { get; } public P2PEliminationReason Reason { get; }
        public int Water { get; } public int Medicine { get; } public int Comfort { get; }
    }

    public sealed class P2PCompetitionSnapshot
    {
        public IReadOnlyList<P2PCompetitionPlayerSnapshot> Players { get; set; } = Array.Empty<P2PCompetitionPlayerSnapshot>();
        public bool EventActive { get; set; } public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty; public float EventSecondsLeft { get; set; }
        public bool HasChosen { get; set; } public bool Finished { get; set; }
        public string LastMessage { get; set; } = string.Empty;
    }

    public static class P2PCompetitionCodec
    {
        public static byte[] EncodeAction(ulong steamId, P2PCompetitionAction action, string value)
        { using var s=new MemoryStream(); using var w=new BinaryWriter(s,Encoding.UTF8); w.Write(steamId);w.Write((byte)action);w.Write(value??string.Empty);return s.ToArray(); }
        public static bool TryDecodeAction(byte[] b,out ulong id,out P2PCompetitionAction action,out string value)
        { id=0;action=default;value="";try{using var s=new MemoryStream(b,false);using var r=new BinaryReader(s,Encoding.UTF8);id=r.ReadUInt64();action=(P2PCompetitionAction)r.ReadByte();value=r.ReadString();return s.Position==s.Length&&Enum.IsDefined(typeof(P2PCompetitionAction),action);}catch{return false;} }
        public static byte[] EncodeState(P2PCompetitionSnapshot x)
        { using var s=new MemoryStream();using var w=new BinaryWriter(s,Encoding.UTF8);w.Write(x.EventActive);w.Write(x.EventId);w.Write(x.EventTitle);w.Write(x.EventSecondsLeft);w.Write(x.Finished);w.Write(x.LastMessage);w.Write(x.Players.Count);foreach(var p in x.Players){w.Write(p.PlayerId);w.Write(p.Health);w.Write(p.Mental);w.Write(p.IsEliminated);w.Write((byte)p.Reason);w.Write(p.Water);w.Write(p.Medicine);w.Write(p.Comfort);}return s.ToArray(); }
        public static bool TryDecodeState(byte[] b,ulong localId,out P2PCompetitionSnapshot x)
        { x=null;try{using var s=new MemoryStream(b,false);using var r=new BinaryReader(s,Encoding.UTF8);var y=new P2PCompetitionSnapshot{EventActive=r.ReadBoolean(),EventId=r.ReadInt32(),EventTitle=r.ReadString(),EventSecondsLeft=r.ReadSingle(),Finished=r.ReadBoolean(),LastMessage=r.ReadString()};int n=r.ReadInt32();if(n<0||n>4)return false;var list=new List<P2PCompetitionPlayerSnapshot>(n);for(int i=0;i<n;i++){ulong id=r.ReadUInt64();var p=new P2PCompetitionPlayerSnapshot(id,r.ReadDouble(),r.ReadDouble(),r.ReadBoolean(),(P2PEliminationReason)r.ReadByte(),r.ReadInt32(),r.ReadInt32(),r.ReadInt32());list.Add(p);}if(s.Position!=s.Length)return false;y.Players=list;x=y;return true;}catch{return false;} }
    }
}
