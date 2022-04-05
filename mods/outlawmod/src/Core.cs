
using System;
using System.Diagnostics;
using System.Reflection;
using ProtoBuf;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.API.Config;
using ExpandedAiTasks;

namespace OutlawMod
{
    [ProtoContract]
    public class OutlawModConfig
    {
        [ProtoMember(1)]
        public bool EnableLooters = true;
        
        [ProtoMember(2)]
        public bool EnablePoachers = true;

        [ProtoMember(3)]
        public bool EnableBrigands = true;

        [ProtoMember(4)]
        public bool EnableYeomen = true;

        [ProtoMember(5)]
        public float StartingSpawnSafeZoneRadius = 500f;

        [ProtoMember(6)]
        public bool StartingSafeZoneHasLifetime = true;

        [ProtoMember(7)]
        public bool StartingSafeZoneShrinksOverLifetime = true;

        [ProtoMember(8)]
        public float StartingSpawnSafeZoneLifetimeInDays = 180f;

        [ProtoMember(9)]
        public bool ClaimedLandBlocksOutlawSpawns = true;

        [ProtoMember(10)]
        public bool OutlawsUseClassicVintageStoryVoices = false;

        [ProtoMember(11)]
        public bool DevMode = false;
    }

    public class Core : ModSystem
    {
        ICoreAPI api;
        ICoreClientAPI capi;

        private Harmony harmony;

        OutlawModConfig config = new OutlawModConfig();

        public override void Start(ICoreAPI api)
        {
            this.api = api;

            //Broadcast Outlaw Mod Config to Clients.
            api.Network.RegisterChannel("outlawModConfig").RegisterMessageType<OutlawModConfig>();

            base.Start(api);              

            harmony = new Harmony("com.grifthegnome.outlawmod.causeofdeath");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            ModSystem expandedAiTasksMod = api.ModLoader.GetModSystem("ExpandedAiTasksLoader.ExpandedAiTasksLoaderCore");

            if (expandedAiTasksMod != null)
            {
                api.World.Logger.Warning("Outlaw Mod: ExpandedAiTasksMod Found, we will skip our internal ai task registration so that ExpandedAiTasks mod can handle-intermod dependencies.");
            }
            else
            {
                api.World.Logger.Warning("Outlaw Mod: ExpandedAiTasksMod Not Found. No Worries! we will register our ai tasks ourselves instead. Nothing to worry about here.");
            }

            RegisterEntitiesShared();
            RegisterBlocksShared();
            RegisterBlockEntitiesShared();
            RegisterAiTasksShared();
            RegisterItemsShared();

        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            capi.Network.GetChannel("outlawModConfig").SetMessageHandler<OutlawModConfig>(onConfigFromServer);

            api.Event.LevelFinalize += () =>
            {
                //Events we can run after the level finalizes.
            };
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            api.Event.ServerRunPhase(EnumServerRunPhase.ModsAndConfigReady, loadConfig);

            api.Event.ServerRunPhase(EnumServerRunPhase.GameReady, () => {
                applyConfig();
                //config.ResolveStartItems(api.World);
            });
            api.Event.ServerRunPhase(EnumServerRunPhase.GameReady, () =>
            {
                //Initialize our static instance of our spawn evaluator.
                OutlawSpawnEvaluator.Initialize(api as ICoreServerAPI);
            });

            //We need to make sure we don't double register with Expanded Ai Tasks, if that mod loaded first.
            if (!AiTaskRegistry.TaskTypes.ContainsKey("shootatentity") )
                AiTaskRegistry.Register<AiTaskShootProjectileAtEntity>("shootatentity");

            if (!AiTaskRegistry.TaskTypes.ContainsKey("engageentity"))
                AiTaskRegistry.Register<AiTaskPursueAndEngageEntity>("engageentity");

            if (!AiTaskRegistry.TaskTypes.ContainsKey("stayclosetoherd"))
                AiTaskRegistry.Register<AiTaskStayCloseToHerd>("stayclosetoherd");

            if (!AiTaskRegistry.TaskTypes.ContainsKey("eatdead"))
                AiTaskRegistry.Register<AiTaskEatDeadEntities>("eatdead");
        }

        public override void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            base.Dispose();
        }

        private void RegisterEntitiesShared()
        {
            api.RegisterEntity("EntityOutlaw", typeof(EntityOutlaw));
        }

        private void RegisterBlocksShared()
        {
            api.RegisterBlockClass("BlockStocks", typeof(BlockStocks));
            api.RegisterBlockClass("BlockHeadOnSpear", typeof(BlockHeadOnSpear));
        }

        private void RegisterBlockEntitiesShared()
        {
            api.RegisterBlockEntityClass("BlockEntityOutlawSpawnBlocker", typeof(BlockEntityOutlawSpawnBlocker));
        }

        private void RegisterAiTasksShared()
        {
            //We need to make sure we don't double register with Expanded Ai Tasks, if that mod loaded first.
            if (!AiTaskRegistry.TaskTypes.ContainsKey("shootatentity") )
                AiTaskRegistry.Register("shootatentity", typeof(AiTaskShootProjectileAtEntity));

            if (!AiTaskRegistry.TaskTypes.ContainsKey("engageentity"))
                AiTaskRegistry.Register("engageentity", typeof(AiTaskPursueAndEngageEntity));

            if (!AiTaskRegistry.TaskTypes.ContainsKey("stayclosetoherd"))
                AiTaskRegistry.Register("stayclosetoherd", typeof(AiTaskStayCloseToHerd));

            if (!AiTaskRegistry.TaskTypes.ContainsKey("eatdead"))
                AiTaskRegistry.Register("eatdead", typeof(AiTaskEatDeadEntities));

            if (!AiTaskRegistry.TaskTypes.ContainsKey("morale"))
                AiTaskRegistry.Register("morale", typeof(AiTaskMorale));
        }

        private void RegisterItemsShared()
        {
            api.RegisterItemClass("ItemOutlawHead", typeof(ItemOutlawHead));
        }

        private void loadConfig()
        {
            try
            {
                OutlawModConfig modConfig = api.LoadModConfig<OutlawModConfig>("OutlawModConfig.json");

                if (modConfig != null)
                {
                    config = modConfig;
                }
                else
                {
                    //We don't have a valid config.
                    throw new Exception();
                }
                    
            }
            catch (Exception e)
            {
                api.World.Logger.Error("Failed loading OutlawModConfig.json, Will initialize new one", e);
                config = new OutlawModConfig();
                api.StoreModConfig( config, "OutlawModConfig.json");
            }
            
            // Called on both sides
        }


        private void applyConfig()
        {
            //Enable/Disable Outlaw Types
            OMGlobalConstants.enableLooters     = config.EnableLooters;
            OMGlobalConstants.enablePoachers    = config.EnablePoachers;
            OMGlobalConstants.enableBrigands    = config.EnableBrigands;
            OMGlobalConstants.enableYeomen      = config.EnableYeomen;

            //Start Spawn Safe Zone Vars
            OMGlobalConstants.startingSpawnSafeZoneRadius           = config.StartingSpawnSafeZoneRadius;
            OMGlobalConstants.startingSafeZoneHasLifetime           = config.StartingSafeZoneHasLifetime;
            OMGlobalConstants.startingSafeZoneShrinksOverlifetime   = config.StartingSafeZoneShrinksOverLifetime;
            OMGlobalConstants.startingSpawnSafeZoneLifetimeInDays   = config.StartingSpawnSafeZoneLifetimeInDays;
            OMGlobalConstants.claimedLandBlocksOutlawSpawns         = config.ClaimedLandBlocksOutlawSpawns;

            //Classic Voice Setting
            OMGlobalConstants.outlawsUseClassicVintageStoryVoices   = config.OutlawsUseClassicVintageStoryVoices;

            //Devmode
            OMGlobalConstants.devMode = config.DevMode;

            //Store an up-to-date version of the config so any new fields that might differ between mod versions are added without altering user values.
            api.StoreModConfig(config, "OutlawModConfig.json");

        }

        private void onConfigFromServer(OutlawModConfig networkMessage)
        {
            this.config = networkMessage;
            applyConfig();
        }
    }
}
