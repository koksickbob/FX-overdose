using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FXOverdose.P2P.Core
{
    public enum P2PCompetitionAction : byte { BuyItem, UseItem, ChooseEvent }

    /// <summary>P2P 돌발 이벤트는 원본의 일반 1·2안만 사용하고 아이템 소모 3안은 제외합니다.</summary>
    public static class P2PChoiceRules
    {
        public const int OptionCount=2;
        public static bool IsValid(int option)=>option>=0&&option<OptionCount;
        public static int GetTimeoutChoice(ulong playerId,int eventId)=>(int)((playerId+(ulong)eventId)%OptionCount);
    }

    public readonly struct P2PCompetitionPlayerSnapshot
    {
        public P2PCompetitionPlayerSnapshot(ulong id, double health, double mental, bool eliminated,
            P2PEliminationReason reason, int energyDrink, int dessert, int sedative, int supplement)
        { PlayerId=id; Health=health; Mental=mental; IsEliminated=eliminated; Reason=reason; EnergyDrink=energyDrink; Dessert=dessert; Sedative=sedative; Supplement=supplement; }
        public ulong PlayerId { get; } public double Health { get; } public double Mental { get; }
        public bool IsEliminated { get; } public P2PEliminationReason Reason { get; }
        public int EnergyDrink { get; } public int Dessert { get; } public int Sedative { get; } public int Supplement { get; }
    }

    public sealed class P2PCompetitionSnapshot
    {
        public IReadOnlyList<P2PCompetitionPlayerSnapshot> Players { get; set; } = Array.Empty<P2PCompetitionPlayerSnapshot>();
        public bool EventActive { get; set; } public int EventId { get; set; }
        public string EventKey { get; set; } = string.Empty;
        public string EventTitle { get; set; } = string.Empty;
        public string EventDescription { get; set; } = string.Empty;
        public string EventMonologue { get; set; } = string.Empty;
        public string Choice1Title { get; set; } = string.Empty;
        public string Choice1Description { get; set; } = string.Empty;
        public byte Choice1Type { get; set; }
        public string Choice2Title { get; set; } = string.Empty;
        public string Choice2Description { get; set; } = string.Empty;
        public byte Choice2Type { get; set; }
        public float EventSecondsLeft { get; set; }
        public bool HasChosen { get; set; } public bool Finished { get; set; }
        public string LastMessage { get; set; } = string.Empty;
    }

    public static class P2PCompetitionCodec
    {
        private const int MaximumActionBytes=96;
        public static byte[] EncodeAction(ulong steamId, P2PCompetitionAction action, string value)
        { using var s=new MemoryStream(); using var w=new BinaryWriter(s,Encoding.UTF8); w.Write(steamId);w.Write((byte)action);w.Write(value??string.Empty);return s.ToArray(); }
        public static bool TryDecodeAction(byte[] b,out ulong id,out P2PCompetitionAction action,out string value)
        { id=0;action=default;value="";if(b==null||b.Length==0||b.Length>MaximumActionBytes)return false;try{using var s=new MemoryStream(b,false);using var r=new BinaryReader(s,Encoding.UTF8);id=r.ReadUInt64();action=(P2PCompetitionAction)r.ReadByte();value=r.ReadString();return value.Length<=32&&s.Position==s.Length&&Enum.IsDefined(typeof(P2PCompetitionAction),action);}catch{return false;} }
        public static byte[] EncodeState(P2PCompetitionSnapshot x)
        { using var s=new MemoryStream();using var w=new BinaryWriter(s,Encoding.UTF8);w.Write(x.EventActive);w.Write(x.EventId);w.Write(x.EventKey);w.Write(x.EventTitle);w.Write(x.EventDescription);w.Write(x.EventMonologue);w.Write(x.Choice1Title);w.Write(x.Choice1Description);w.Write(x.Choice1Type);w.Write(x.Choice2Title);w.Write(x.Choice2Description);w.Write(x.Choice2Type);w.Write(x.EventSecondsLeft);w.Write(x.Finished);w.Write(x.LastMessage);w.Write(x.Players.Count);foreach(var p in x.Players){w.Write(p.PlayerId);w.Write(p.Health);w.Write(p.Mental);w.Write(p.IsEliminated);w.Write((byte)p.Reason);w.Write(p.EnergyDrink);w.Write(p.Dessert);w.Write(p.Sedative);w.Write(p.Supplement);}return s.ToArray(); }
        public static bool TryDecodeState(byte[] b,ulong localId,out P2PCompetitionSnapshot x)
        { x=null;try{using var s=new MemoryStream(b,false);using var r=new BinaryReader(s,Encoding.UTF8);var y=new P2PCompetitionSnapshot{EventActive=r.ReadBoolean(),EventId=r.ReadInt32(),EventKey=r.ReadString(),EventTitle=r.ReadString(),EventDescription=r.ReadString(),EventMonologue=r.ReadString(),Choice1Title=r.ReadString(),Choice1Description=r.ReadString(),Choice1Type=r.ReadByte(),Choice2Title=r.ReadString(),Choice2Description=r.ReadString(),Choice2Type=r.ReadByte(),EventSecondsLeft=r.ReadSingle(),Finished=r.ReadBoolean(),LastMessage=r.ReadString()};int n=r.ReadInt32();if(n<0||n>4)return false;var list=new List<P2PCompetitionPlayerSnapshot>(n);for(int i=0;i<n;i++){ulong id=r.ReadUInt64();var p=new P2PCompetitionPlayerSnapshot(id,r.ReadDouble(),r.ReadDouble(),r.ReadBoolean(),(P2PEliminationReason)r.ReadByte(),r.ReadInt32(),r.ReadInt32(),r.ReadInt32(),r.ReadInt32());list.Add(p);}if(s.Position!=s.Length)return false;y.Players=list;x=y;return true;}catch{return false;} }
    }
}
