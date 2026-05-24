using System.Diagnostics;
using Unity.Entities;

namespace GAS.Runtime
{
    public static partial class HeadlessAutoChessScenario
    {
        private static void TickRuntime()
        {
            TickRuntimeMeasured();
        }

        private static RuntimeTickGroupTiming TickRuntimeMeasured(
            HeadlessAutoChessRuntimeSystemTimingCollector systemTimingCollector = null)
        {
            var world = GASManager.ExWorld;
            var command = world.GetExistingSystemManaged<GASCommandGroup>();
            var resetDirty = world.GetExistingSystemManaged<GASResetDirtyGroup>();
            var tag = world.GetExistingSystemManaged<GASTagGroup>();
            var effect = world.GetExistingSystemManaged<GASEffectGroup>();
            var attribute = world.GetExistingSystemManaged<GASAttributeGroup>();
            var ability = world.GetExistingSystemManaged<GASAbilityGroup>();
            var cue = world.GetExistingSystemManaged<GASCueGroup>();

            var totalStartTicks = Stopwatch.GetTimestamp();
            var commandTicks = UpdateGroupMeasured(command, "Command", systemTimingCollector);
            var resetDirtyTicks = UpdateGroupMeasured(resetDirty, "ResetDirty", systemTimingCollector);
            var tagTicks = UpdateGroupMeasured(tag, "Tag", systemTimingCollector);
            var effectTicks = UpdateGroupMeasured(effect, "Effect", systemTimingCollector);
            var attributeTicks = UpdateGroupMeasured(attribute, "Attribute", systemTimingCollector);
            var abilityTicks = UpdateGroupMeasured(ability, "Ability", systemTimingCollector);
            var cueTicks = UpdateGroupMeasured(cue, "Cue", systemTimingCollector);
            var totalTicks = Stopwatch.GetTimestamp() - totalStartTicks;

            return new RuntimeTickGroupTiming(
                totalTicks,
                commandTicks,
                resetDirtyTicks,
                tagTicks,
                effectTicks,
                attributeTicks,
                abilityTicks,
                cueTicks);
        }

        private static void RecordRuntimeDiagnostics(in RuntimeTickGroupTiming timing)
        {
            var em = GASManager.EntityManager;
            var debugger = GASManager.EntityRuntimeDebugger;
            if (debugger == Entity.Null || !em.Exists(debugger))
                return;

            var frame = GASManager.CurrentFrame;
            GasRuntimeDebugger.RecordTickTiming(
                em,
                debugger,
                frame,
                timing.TotalTicks,
                timing.CommandTicks,
                timing.ResetDirtyTicks,
                timing.TagTicks,
                timing.EffectTicks,
                timing.AttributeTicks,
                timing.AbilityTicks,
                timing.CueTicks,
                Stopwatch.Frequency);
            GasRuntimeDebugger.CollectAndRecordRuntimeCoreCounters(
                em,
                debugger,
                frame,
                GASManager.EntityEventBus,
                GASManager.EntityEventLogSink);
            GasRuntimeDebugger.RecordEventBusPressure(
                em,
                debugger,
                GASManager.EntityEventBus,
                frame);
        }

        private static void RecordSystemTimingDiagnostics(HeadlessAutoChessSystemTiming[] systemTimings)
        {
            if (systemTimings == null || systemTimings.Length == 0)
                return;

            var em = GASManager.EntityManager;
            var debugger = GASManager.EntityRuntimeDebugger;
            if (debugger == Entity.Null || !em.Exists(debugger))
                return;

            var frame = GASManager.CurrentFrame;
            for (var i = 0; i < systemTimings.Length; i++)
            {
                var timing = systemTimings[i];
                GasRuntimeDebugger.RecordSystemTimingAggregate(
                    em,
                    debugger,
                    frame,
                    timing.GroupName,
                    timing.SystemName,
                    timing.CallCount,
                    timing.ElapsedTicks,
                    Stopwatch.Frequency);
            }
        }

        private static long UpdateGroupMeasured(
            ComponentSystemGroup group,
            string groupName,
            HeadlessAutoChessRuntimeSystemTimingCollector systemTimingCollector)
        {
            var startTicks = Stopwatch.GetTimestamp();
            if (systemTimingCollector == null)
            {
                group.Update();
            }
            else
            {
                var systems = systemTimingCollector.GetSystems(group, groupName);
                for (var i = 0; i < systems.Length; i++)
                    UpdateSystemMeasured(group.World, systems[i], groupName, systemTimingCollector);
            }

            return Stopwatch.GetTimestamp() - startTicks;
        }

        private static long UpdateSystemMeasured(
            World world,
            SystemHandle system,
            string groupName,
            HeadlessAutoChessRuntimeSystemTimingCollector systemTimingCollector)
        {
            var systemTypeIndex = world.Unmanaged.GetSystemTypeIndex(system);
            var systemName = GetShortSystemName(systemTypeIndex);
            var managedSystem = world.GetExistingSystemManaged(systemTypeIndex);
            var childGroup = managedSystem as ComponentSystemGroup;

            if (childGroup != null)
            {
                var childGroupName = string.IsNullOrEmpty(groupName)
                    ? systemName
                    : groupName + "/" + systemName;
                return UpdateGroupMeasured(childGroup, childGroupName, systemTimingCollector);
            }

            var startTicks = Stopwatch.GetTimestamp();
            system.Update(world.Unmanaged);
            var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            systemTimingCollector.Record(groupName, systemName, elapsedTicks);
            return elapsedTicks;
        }

        private static string GetShortSystemName(SystemTypeIndex systemTypeIndex)
        {
            var systemName = TypeManager.GetSystemName(systemTypeIndex).ToString();
            var separatorIndex = systemName.LastIndexOf('.');
            return separatorIndex >= 0 && separatorIndex + 1 < systemName.Length
                ? systemName.Substring(separatorIndex + 1)
                : systemName;
        }
    }
}
