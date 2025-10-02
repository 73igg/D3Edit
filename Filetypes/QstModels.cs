using System;
using System.Collections.Generic;

namespace D3Edit.Filetypes.Qst
{
    public enum QuestType
    {
        MainQuest = 0,
        Event = 2,
        Challenge = 4,
        Bounty = 5,
        HoradricQuest = 6,
        SetDungeon = 7,
        SetDungeonBonus = 8,
        SetDungeonMastery = 9,
        SetDungeonTracker = 10
    }

    public enum QuestMode
    {
        None = -1,
        TimedDungeon = 0,
        WaveFight = 1,
        Horde = 2,
        Zapper = 3,
        GoblinHunt = 4
    }

    public sealed class QstJsonFile
    {
        public D3Edit.Core.Header Header { get; set; } = D3Edit.Core.Header.Default();

        public QuestType QuestType { get; set; }
        public int NumberOfSteps { get; set; }
        public int NumberOfCompletionSteps { get; set; }
        public int I2 { get; set; }
        public int I3 { get; set; }
        public int I4 { get; set; }
        public int I5 { get; set; }

        public UnassignedStepJson UnassignedStep { get; set; } = new UnassignedStepJson();
        public List<StepSummary> QuestSteps { get; set; } = new();
        public List<CompletionStepSummary> QuestCompletionSteps { get; set; } = new();

        public int[] SNOs { get; set; } = new int[18];
        public int WorldSNO { get; set; }
        public QuestMode Mode { get; set; }

        public BountyDataJson Bounty { get; set; } = new();

        public List<StringAtOffset> Strings { get; set; } = new();
    }

    public sealed class UnassignedStepJson
    {
        public int ID { get; set; }
        public int I0 { get; set; }
        public List<string> ObjectiveNames { get; set; } = new();
    }

    public sealed class StepSummary
    {
        public string Name { get; set; } = "";
        public int ID { get; set; }
        public int I1 { get; set; }
    }

    public sealed class CompletionStepSummary
    {
        public string Name { get; set; } = "";
        public int ID { get; set; }
    }

    public sealed class BountyDataJson
    {
        public int ActData { get; set; }
        public int Type { get; set; }
        public int I0 { get; set; }
        public float F0 { get; set; }
    }

    public sealed class StringAtOffset
    {
        public int Offset { get; set; }
        public string Value { get; set; } = "";
    }
}