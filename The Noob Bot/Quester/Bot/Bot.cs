﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Windows.Forms;
using nManager.Plugins;
using nManager.Wow.Bot.Tasks;
using nManager.Wow.Enums;
using nManager.Wow.ObjectManager;
using Quester.Profile;
using nManager;
using nManager.FiniteStateMachine;
using nManager.Helpful;
using nManager.Wow.Bot.States;
using nManager.Wow.Helpers;
using nManager.Wow.Class;
using Quester.Tasks;
using ObjectManager = nManager.Wow.ObjectManager.ObjectManager;
using Quest = nManager.Wow.Helpers.Quest;

namespace Quester.Bot
{
    internal class Bot
    {
        private static readonly Engine Fsm = new Engine();

        internal static QuesterProfile Profile;

        internal static bool DisplayedOnce = false;

        internal static void DumpInfoAboutProfile(string profileName, QuesterProfile profile)
        {
            if (string.IsNullOrEmpty(profile.Author))
                return;
            if (DisplayedOnce)
                Logging.Write(profileName + " by " + profile.Author);
            else
            {
                Logging.Write("Loading " + profileName + " created by " + profile.Author);
                DisplayedOnce = true;
            }
            switch (profile.DevelopmentStatus)
            {
                case DevelopmentStatus.WorkInProgress:
                    Logging.WriteDebug("This profile is still a work in progress and may not be even complete.");
                    break;
                case DevelopmentStatus.Untested:
                    Logging.WriteDebug("This profile has yet to be reviewed by our team and may contains stucks, bugs.");
                    break;
                case DevelopmentStatus.Outdated:
                    Logging.WriteDebug("This profile is outdated and may contains stucks, bugs, don't hesitate to report them so we can update it properly.");
                    break;
                case DevelopmentStatus.ReleaseCandidate:
                    Logging.WriteDebug("This profile has been reviewed by our team but is subject to have unseen bugs.");
                    break;
                case DevelopmentStatus.Completed:
                    Logging.WriteDebug("This profile is marked as completed, if any issue, please report so we can downgrade it to outdated.");
                    break;
            }
            if (!string.IsNullOrEmpty(profile.Description))
                Logging.Write("Description found: " + profile.Description);
            if (!string.IsNullOrEmpty(profile.ExtraCredits))
                Logging.Write("Special thanks: " + profile.ExtraCredits);
        }

        internal static bool Pulse()
        {
            try
            {
                MountTask.AllowMounting = true;
                Quest.GetSetIgnoreFight = false;
                Quest.GetSetIgnoreAllFight = false;
                Quest.GetSetDismissPet = false;
                Profile = new QuesterProfile();
                QuesterProfileLoader f = new QuesterProfileLoader();
                f.ShowDialog();
                if (!string.IsNullOrWhiteSpace(QuesterSettings.CurrentSettings.LastProfile) &&
                    ((QuesterSettings.CurrentSettings.LastProfileSimple &&
                      File.Exists(Application.StartupPath + "\\Profiles\\Quester\\" + QuesterSettings.CurrentSettings.LastProfile)) ||
                     (!QuesterSettings.CurrentSettings.LastProfileSimple &&
                      File.Exists(Application.StartupPath + "\\Profiles\\Quester\\Grouped\\" + QuesterSettings.CurrentSettings.LastProfile))))
                {
                    Profile = QuesterSettings.CurrentSettings.LastProfileSimple
                        ? XmlSerializer.Deserialize<QuesterProfile>(Application.StartupPath + "\\Profiles\\Quester\\" + QuesterSettings.CurrentSettings.LastProfile)
                        : XmlSerializer.Deserialize<QuesterProfile>(Application.StartupPath + "\\Profiles\\Quester\\Grouped\\" +
                                                                    QuesterSettings.CurrentSettings.LastProfile);
                    DumpInfoAboutProfile(QuesterSettings.CurrentSettings.LastProfile, Profile);

                    foreach (Include include in Profile.Includes)
                    {
                        try
                        {
                            if (!Script.Run(include.ScriptCondition)) continue;
                            //Logging.Write(Translation.GetText(Translation.Text.SubProfil) + " " + include.PathFile);
                            QuesterProfile profileInclude = XmlSerializer.Deserialize<QuesterProfile>(Application.StartupPath + "\\Profiles\\Quester\\" + include.PathFile);
                            if (profileInclude != null)
                            {
                                // Profile.Includes.AddRange(profileInclude.Includes);
                                Profile.Questers.AddRange(profileInclude.Questers);
                                Profile.Blackspots.AddRange(profileInclude.Blackspots);
                                Profile.AvoidMobs.AddRange(profileInclude.AvoidMobs);
                                Profile.Quests.AddRange(profileInclude.Quests);
                                DumpInfoAboutProfile(include.PathFile, profileInclude);
                            }
                        }

                        catch (Exception e)
                        {
                            MessageBox.Show(Translate.Get(Translate.Id.File_not_found) + ": " + e);
                            Logging.Write(Translate.Get(Translate.Id.File_not_found));
                            return false;
                        }
                    }
                    // Now check the integrity by checking we have all NPC required
                    foreach (Profile.Quest q in Profile.Quests)
                    {
                        bool isWorldQuest = q.WorldQuestLocation != null && q.WorldQuestLocation.IsValid;
                        if (!isWorldQuest && q.ItemPickUp == 0 && FindQuesterById(q.PickUp).Entry == 0 && !q.AutoAccepted)
                        {
                            MessageBox.Show("Your profile is missing the definition of NPC entry " + q.PickUp +
                                            "\nThe quest is '" + q.Name + "' (" + q.Id + "). Cannot continues!");
                            return false;
                        }
                        if (!isWorldQuest && FindQuesterById(q.TurnIn).Entry == 0)
                        {
                            MessageBox.Show("Your profile is missing the definition of NPC entry " + q.TurnIn +
                                            "\nThe quest is '" + q.Name + "' (" + q.Id + "). Cannot continues!");
                            return false;
                        }
                        foreach (Profile.QuestObjective o in q.Objectives)
                        {
                            if (o.NpcEntry != 0 && FindQuesterById(o.NpcEntry).Entry == 0)
                            {
                                MessageBox.Show("Your profile is missing the definition of NPC entry " + o.NpcEntry +
                                                "\nThe quest is '" + q.Name + "' (" + q.Id + "). Cannot continues!");
                                return false;
                            }
                            if (o.InternalIndex != 0 && o.Count <= 0 && o.CollectCount <= 0)
                            {
                                MessageBox.Show("Your profile has an objective with an InternalIndex but without proper Count or CollectCount values" +
                                                "\nThe quest is '" + q.Name + "' (" + q.Id + "). Cannot continues!");
                                return false;
                            }
                            if (o.InternalIndex > 23)
                            {
                                MessageBox.Show("Your profile has an objective with an InternalIndex > 23, which is not possible." +
                                                "\nThe quest is '" + q.Name + "' (" + q.Id + "). Cannot continues!");
                                return false;
                            }
                        }
                    }
                    Logging.Write("Loaded " + Profile.Quests.Count + " quests");
                    Profile.Filter();
                    Logging.Write(Profile.Quests.Count + " quests left after filtering on class/race");

                    Tasks.QuestingTask.completed = false;

                    Quest.ConsumeQuestsCompletedRequest();
                    Logging.Write("received " + Quest.FinishedQuestSet.Count + " quests.");
                }
                else
                    return false;

                // Black List:
                nManagerSetting.AddRangeBlackListZone(new List<nManagerSetting.BlackListZone>(Profile.Blackspots));

                // Load CC:
                CombatClass.LoadCombatClass();

                ImportedQuesters = false;

                // FSM
                Fsm.States.Clear();

                Fsm.AddState(new Pause {Priority = 200});
                Fsm.AddState(new Resurrect {Priority = 150});
                Fsm.AddState(new IsAttacked {Priority = 140});
                Fsm.AddState(new Regeneration {Priority = 130});
                Fsm.AddState(new FlightMasterDiscovery {Priority = 120});
                Fsm.AddState(new Looting {Priority = 110});
                Fsm.AddState(new Travel {Priority = 100});
                Fsm.AddState(new ToTown {Priority = 90});
                Fsm.AddState(new SpecializationCheck {Priority = 80});
                Fsm.AddState(new LevelupCheck {Priority = 70});
                Fsm.AddState(new Trainers {Priority = 60});
                Fsm.AddState(new AutoItemCombiner {Priority = 52});
                Fsm.AddState(new MillingState {Priority = 51});
                Fsm.AddState(new ProspectingState {Priority = 50});
                Fsm.AddState(new Farming {Priority = 30});
                Fsm.AddState(new QuesterState {Priority = 20});
                Fsm.AddState(new MovementLoop {Priority = 10});
                Fsm.AddState(new Idle {Priority = 0});

                foreach (var statePlugin in Plugins.ListLoadedStatePlugins)
                {
                    Fsm.AddState(statePlugin);
                }

                Fsm.States.Sort();
                Fsm.StartEngine(7, "FSM Quester");
                EventsListener.HookEvent(WoWEventsType.QUEST_DETAIL, callback => Quest.AutoCompleteQuest());
                EventsListener.HookEvent(WoWEventsType.QUEST_AUTOCOMPLETE, callback => Quest.AutoCompleteQuest());

                return true;
            }
            catch (Exception e)
            {
                try
                {
                    Dispose();
                }
                catch
                {
                }
                Logging.WriteError("Quester > Bot > Bot  > Pulse(): " + e);
                return false;
            }
        }

        internal static void Dispose()
        {
            try
            {
                Script.CachedScripts = new Dictionary<string, IScript>();
                // clear cache on Stop.
                Fsm.StopEngine();
                Fight.StopFight();
                MovementManager.StopMove();
                MountTask.AllowMounting = true;
                Quest.GetSetIgnoreFight = false;
                Quest.GetSetIgnoreAllFight = false;
                Quest.GetSetDismissPet = false;
                Profile = null;
                QuestingTask.Cleanup();
                EventsListener.UnHookEvent(WoWEventsType.QUEST_DETAIL, callback => Quest.AutoCompleteQuest());
                EventsListener.UnHookEvent(WoWEventsType.QUEST_AUTOCOMPLETE, callback => Quest.AutoCompleteQuest());
            }
            catch (Exception e)
            {
                Logging.WriteError("Quester > Bot > Bot  > Dispose(): " + e);
            }
        }

        public static bool ImportedQuesters = false;

        public static Npc FindQuesterById(int entry)
        {
            if (!ImportedQuesters)
            {
                if (Profile.Questers.Count > 0)
                    QuestersDB.AddNpcRange(Profile.Questers);
                ImportedQuesters = true;
            }
            return QuestersDB.GetNpcByEntry(entry);
        }

        public static Npc FindNearestQuesterById(int entry)
        {
            WoWUnit unit = ObjectManager.GetNearestWoWUnit(ObjectManager.GetWoWUnitByEntry(entry, true), ObjectManager.Me.Position, false, false, true);
            if (unit.IsValid && unit.GetDistance <= 60f && unit.IsNpcQuestGiver && (unit.CanTurnIn || unit.HasQuests))
            {
                var npc = new Npc
                {
                    Entry = unit.Entry,
                    Name = unit.Name,
                    ContinentId = Usefuls.ContinentNameMpq,
                    Position = unit.Position,
                    Type = Npc.NpcType.QuestGiver,
                    Faction = ObjectManager.Me.PlayerFaction.ToLower() == "horde" ? Npc.FactionType.Horde : Npc.FactionType.Alliance
                };
                return npc;
            }
            if (ImportedQuesters)
                return QuestersDB.GetNpcNearbyByEntry(entry);
            if (Profile.Questers.Count > 0)
                QuestersDB.AddNpcRange(Profile.Questers);
            ImportedQuesters = true;
            return QuestersDB.GetNpcNearbyByEntry(entry);
        }
    }
}