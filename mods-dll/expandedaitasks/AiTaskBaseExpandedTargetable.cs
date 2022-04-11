using System.Collections.Generic;
using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace ExpandedAiTasks
{
    //This class is derrived inherits from AiTaskBaseTargetable and adds functionality and fixes that we want in many of our expanded Ai Tasks.
 
    public class AiTaskBaseExpandedTargetable : AiTaskBaseTargetable
    {
        protected List<Entity> herdMembers = new List<Entity>();

    public AiTaskBaseExpandedTargetable(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);
        }
        public override bool ShouldExecute()
        {
            Debug.Assert(false, "This function needs to be overriden and should never be called.");
            return false;
        }

        /// <summary>
        /// //This function should be called the first time you initilize this Ai's herd.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="range"></param>
        /// <param name="ignoreEntityCode"></param>
        /// <returns></returns>

        protected virtual bool CountHerdMembers(Entity ent, float range, bool ignoreEntityCode = false)
        {
            
            if ( ent is EntityAgent)
            {
                EntityAgent agent = ent as EntityAgent;
                if(agent.Alive && agent.HerdId == entity.HerdId)
                    herdMembers.Add(agent);
            }

            return false;
        }

        /// <summary>
        /// //This function should be called when we need to update the count of valid herd members.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="range"></param>
        /// <param name="ignoreEntityCode"></param>
        /// <returns></returns>

        protected virtual void UpdateHerdCount()
        {
            List<Entity> currentMembers = new List<Entity>();
            foreach (Entity agent in herdMembers)
            {
                if (agent == null)
                    continue;

                if (!agent.Alive)
                    continue;

                currentMembers.Add(agent);
            }

            herdMembers = currentMembers;
        }

        //This is an override for the default OnEntityHurt func that prevents Ai from aggoing on friendly herd members.
        public override void OnEntityHurt(DamageSource source, float damage)
        {
            if (source.SourceEntity is EntityAgent)
            {
                EntityAgent attacker = source.SourceEntity as EntityAgent;
                if ( attacker.HerdId != entity.HerdId)
                {
                    attackedByEntity = source.SourceEntity;
                    attackedByEntityMs = entity.World.ElapsedMilliseconds;
                }     
            }
            else
            {
                attackedByEntity = source.SourceEntity;
                attackedByEntityMs = entity.World.ElapsedMilliseconds;
            }
        }
    }
}