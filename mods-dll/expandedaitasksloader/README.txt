Hi! Welcome to the Expanded Ai Tasks Loader Mod Readme and thanks for downloading.

This mod acts as a shared loader for the ExpandedAiTasks.dll that accompanies it. 
The .dll adds aiTasks that I use to develop my mods that the base game doesn't support natively.

Getting Started:

========================================================================================================================

FOR PLAYERS:
1. Just put the zip file in your mods folder, like any other mod.
2. You're good to go!

========================================================================================================================

FOR MODDERS:
If you're planning on integrating tasks from ExpandedAiTasks.dll into your mod there are two ways of doing it: 
	1. Using this Loader Mod.
	2. Doing your own .dll integration.

There are pros and cons to both approaches:
	The Loader Mod Method:
	In this method, you let this mod do the heavy lifting for you. You simply reference the AiTasks it registers.
		Pros:
			1. If you use this loader mod, you don't have to write any code, your mod can just reference the AiTasks in your .json files and it will work.
			2. You don't have to worry about compatibility with any other mod that also uses the loader mod, since all the AiTasks are registered in the same place.
		Cons:
			1. Your mod won't work properly if players don't have the ExpandedAiTasksLoader mod installed. You'll have to point people to this mod and tell them to download it so
			they will have the AiTasks registered when you reference them. (i.e. Your mod won't be a standalone mod).

	Doing your own .dll integration:
	In this method, You include the ExpandedAiTasks.dll in your project and register it's AiTasks in your Core ModSystem.
		Pros:
			1. You have total control over what is and is not registered from the .dll.
			2. Your mod is totally self-contained and people won't need to download the loader for your mod to work.
		Cons:
			1. You are entirely responsible for compatibility between your mod and other mods that use these AiTasks. 
			That is to say, if two different mods register the same AiTask, they will crash the engine. So you have to make sure you don't double register.

========================================================================================================================

Integrating ExpandedAiTasks.dll into your mod

	1. Add ExpandedAiTasks.dll as a reference to your project.
	2. In your Core ModSystem .cs file include 'using ExpandedAiTasks;'
	3. In your Core ModSystem class, on server and client start up:
		a. Register the AiTasks you want to use.
		b. Be sure to enclose each AiTask registration in a statement that checks whether or not this task has already been registered by another system.
		c. Be sure to register your AiTask under the same key as is used by the loader mod. That way if the player has the loader mod present in their folder,
		your mod will defer to its registration while still maintaining a self-contained and independent registration of the tasks.
		
		Example:

			if (!AiTaskRegistry.TaskTypes.ContainsKey("shootatentity") )
                AiTaskRegistry.Register("shootatentity", typeof(AiTaskShootProjectileAtEntity));

	4. At this point, you should now be able to reference this AiTask in your .json files.
	5. When packaging your mod, do not forget to include ExpandedAiTasks.dll in your zip file (Not to be confused with ExpandedAiTasksLoader.dll)

========================================================================================================================

AI Tasks Documentation:

	AiTaskShootProjectileAtEntity ( registered as "shootatentity"" )
		
		This AiTask tells an Ai to shoot a specified projectile at a target with the specified settings.

	json Settings

		int durationMs: The duration of the ranged attack in milliseconds.
		int releaseAtMs: The time at which the projectile is fired in milliseconds.
		float minDist: The minimum distance at which the attack can occur (measured in blocks).
		float maxDist: The maximum distance at which the attack can occur (measured in blocks).
		float minRangeDistOffTarget: The projectile's maximum distance off target at minimum range (measured in blocks).
		float maxRangeDistOffTarget: The projectile's maximum distance off target at maximum range (measured in blocks).
		float maxVelocity: The absolute maximum velocity of the projectile.
		float newTargetDistOffTarget: The projectile's maximum distance off target when the shooter has just acquired a new target (measured in blocks).
		float newTargetZeroingTime: The time (in seconds) over which a shooter zeros his aim and newTargetDistOffTarget lerps to a value of 0.0. 
		float damage: How much damage the attack deals when the projectile hits its target.
		float damageFalloffPercent: The percent of the damage value that is retained at damageFalloffEndDist.
		float damageFalloffStartDist: The distance at which damage begins to falloff (measured in blocks).
		float damageFalloffEndDist: The distance at which damage is fully reduced by damageFalloffPercent and will not falloff further (measured in blocks).
		string projectileItem: The code of the projectile that this attack will fire.
		bool projectileRemainsInWorld: Determines whether or not the projectile will remain in the world after impacting a surface.
		float projectileBreakOnImpactChance: The percentage chance that a projectile will break when impacting a surface is projectileRemainInWorld is true.

		Use Example from OutlawMod, yeoman-archer.json
		{
			code: "shootatentity",
			entityCodes: ["player", "drifter-*", "wolf-male", "wolf-female", "hyena-male", "hyena-female", "locust-*", "bear-*", "looter"],
			priority: 3.75,
			priorityForCancel: 9,
			mincooldown: 1000, 
			maxcooldown: 1500, 
			maxDist: 32,
			minRangeDistOffTarget: 0.25,
			maxRangeDistOffTarget: 0.75,
			maxVelocity: 1.25,
			newTargetDistOffTarget: 0.75,
			newTargetZeroingTime: 5.0,
			damage: 3.0,
			damageFalloffPercent: 0.66,
			damageFalloffStartDist: 18,
			damageFalloffEndDist: 28,
			projectileItem: "arrow-copper",
			projectileRemainsInWorld: true,
			projectileBreakOnImpactChance: 0.90,
			durationMs: 2000,
			releaseAtMs: 1000,
			seekingRange: 15,
			animationSpeed: 1.0,
			animation: "bowattack"
		},
	

			