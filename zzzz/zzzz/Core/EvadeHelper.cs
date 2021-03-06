﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Aimtec;
using Aimtec.SDK.Events;
using Aimtec.SDK.Extensions;
using Aimtec.SDK.Menu.Components;
using Aimtec.SDK.Util.Cache;
//using SharpDX;

//using SharpDX;

namespace zzzz
{
    internal class EvadeHelper
    {
        public static bool fastEvadeMode;
        private static Obj_AI_Hero myHero => ObjectManager.GetLocalPlayer();

        public static bool PlayerInSkillShot(Spell spell)
        {
            return ObjectCache.myHeroCache.serverPos2D.InSkillShot(spell, ObjectCache.myHeroCache.boundingRadius);
        }

        public static PositionInfo InitPositionInfo(Vector2 pos, float extraDelayBuffer, float extraEvadeDistance,
            Vector2 lastMovePos, Spell lowestEvadeTimeSpell)
        {
            if (!ObjectCache.myHeroCache.HasPath && ObjectCache.myHeroCache.serverPos2D.Distance(pos) <= 75)
                pos = ObjectCache.myHeroCache.serverPos2D;

            var extraDist = ObjectCache.menuCache.cache["ExtraCPADistance"].As<MenuSlider>().Value;

            PositionInfo posInfo;
            posInfo = CanHeroWalkToPos(pos, ObjectCache.myHeroCache.moveSpeed, extraDelayBuffer + ObjectCache.gamePing,
                extraDist);
            posInfo.isDangerousPos = pos.CheckDangerousPos(6);
            posInfo.hasExtraDistance = extraEvadeDistance > 0 && pos.CheckDangerousPos(extraEvadeDistance);
            posInfo.closestDistance = posInfo.distanceToMouse;
            posInfo.distanceToMouse = pos.GetPositionValue();
            posInfo.posDistToChamps = pos.GetDistanceToChampions();
            posInfo.speed = ObjectCache.myHeroCache.moveSpeed;

            if (ObjectCache.menuCache.cache["RejectMinDistance"].As<MenuSlider>().Value > 0 &&
                ObjectCache.menuCache.cache["RejectMinDistance"].As<MenuSlider>().Value >
                posInfo.closestDistance) //reject closestdistance
                posInfo.rejectPosition = true;

            if (ObjectCache.menuCache.cache["MinComfortZone"].As<MenuSlider>().Value > posInfo.posDistToChamps)
                posInfo.hasComfortZone = false;

            return posInfo;
        }

        public static IOrderedEnumerable<PositionInfo> GetBestPositionTest()
        {
            var posChecked = 0;
            var maxPosToCheck = 50;
            var posRadius = 50;
            var radiusIndex = 0;

            var heroPoint = ObjectCache.myHeroCache.serverPos2D;
            var lastMovePos = Game.CursorPos.To2D();

            var extraDelayBuffer = ObjectCache.menuCache.cache["ExtraPingBuffer"].As<MenuSlider>().Value;
            var extraEvadeDistance = ObjectCache.menuCache.cache["ExtraEvadeDistance"].As<MenuSlider>().Value;

            if (ObjectCache.menuCache.cache["HigherPrecision"].As<MenuBool>().Enabled)
            {
                maxPosToCheck = 150;
                posRadius = 25;
            }

            var posTable = new List<PositionInfo>();
            var fastestPositions = GetFastestPositions();

            Spell lowestEvadeTimeSpell;
            var lowestEvadeTime = SpellDetector.GetLowestEvadeTime(out lowestEvadeTimeSpell);

            foreach (var pos in fastestPositions) //add the fastest positions into list of candidates
                posTable.Add(InitPositionInfo(pos, extraDelayBuffer, extraEvadeDistance, lastMovePos,
                    lowestEvadeTimeSpell));

            while (posChecked < maxPosToCheck)
            {
                radiusIndex++;

                var curRadius = radiusIndex * 2 * posRadius;
                var curCircleChecks = (int) Math.Ceiling(2 * Math.PI * curRadius / (2 * (double) posRadius));

                for (var i = 1; i < curCircleChecks; i++)
                {
                    posChecked++;
                    var cRadians = 2 * Math.PI / (curCircleChecks - 1) * i; //check decimals
                    var pos = new Vector2((float) Math.Floor(heroPoint.X + curRadius * Math.Cos(cRadians)),
                        (float) Math.Floor(heroPoint.Y + curRadius * Math.Sin(cRadians)));


                    posTable.Add(InitPositionInfo(pos, extraDelayBuffer, extraEvadeDistance, lastMovePos,
                        lowestEvadeTimeSpell));


                    //if (pos.IsWall())
                    //{
                    //Render.Circle.DrawCircle(new Vector3(pos.X, pos.Y, myHero.Position.Z), (float)25, Color.White, 3);
                    //}
                    /*
                    if (posDangerLevel > 0)
                    {
                        Render.Circle.DrawCircle(new Vector3(pos.X, pos.Y, myHero.Position.Z), (float) posRadius, Color.White, 3);
                    }*/

                    // fix : uncomment path line
                    //var path = myHero.GetPath(pos.To3D());

                    //Render.Circle.DrawCircle(path[path.Length - 1], (float)posRadius, Color.White, 3);
                    //Render.Circle.DrawCircle(new Vector3(pos.X, pos.Y, myHero.Position.Z), (float)posRadius, Color.White, 3);

                    //var posOnScreen = Drawing.WorldToScreen(path[path.Length - 1]);
                    //Render.Text(posOnScreen.X, posOnScreen.Y, Color.Aqua, "" + path.Length);
                }
            }

            var sortedPosTable =
                posTable.OrderBy(p => p.isDangerousPos)
                    .ThenBy(p => p.posDangerLevel)
                    .ThenBy(p => p.posDangerCount)
                    .ThenBy(p => p.distanceToMouse);

            return sortedPosTable;
        }

        public static PositionInfo GetBestPosition()
        {
            var posChecked = 0;
            var maxPosToCheck = 50;
            var posRadius = 50;
            var radiusIndex = 0;

            var extraDelayBuffer = ObjectCache.menuCache.cache["ExtraPingBuffer"].As<MenuSlider>().Value;
            var extraEvadeDistance = ObjectCache.menuCache.cache["ExtraEvadeDistance"].As<MenuSlider>().Value;

            SpellDetector.UpdateSpells();
            CalculateEvadeTime();

            if (ObjectCache.menuCache.cache["CalculateWindupDelay"].As<MenuBool>().Enabled)
            {
                var extraWindupDelay = Evade.lastWindupTime - EvadeUtils.TickCount;
                if (extraWindupDelay > 0)
                    extraDelayBuffer += (int) extraWindupDelay;
            }

            extraDelayBuffer += (int) Evade.avgCalculationTime;

            if (ObjectCache.menuCache.cache["HigherPrecision"].As<MenuBool>().Enabled)
            {
                maxPosToCheck = 150;
                posRadius = 25;
            }

            var heroPoint = ObjectCache.myHeroCache.serverPos2D;
            var lastMovePos = Game.CursorPos.To2D();

            var posTable = new List<PositionInfo>();

            Spell lowestEvadeTimeSpell;
            var lowestEvadeTime = SpellDetector.GetLowestEvadeTime(out lowestEvadeTimeSpell);

            var fastestPositions = GetFastestPositions();

            foreach (var pos in fastestPositions) //add the fastest positions into list of candidates
                posTable.Add(InitPositionInfo(pos, extraDelayBuffer, extraEvadeDistance, lastMovePos,
                    lowestEvadeTimeSpell));

            while (posChecked < maxPosToCheck)
            {
                radiusIndex++;

                var curRadius = radiusIndex * 2 * posRadius;
                var curCircleChecks = (int) Math.Ceiling(2 * Math.PI * curRadius / (2 * (double) posRadius));

                for (var i = 1; i < curCircleChecks; i++)
                {
                    posChecked++;
                    var cRadians = 2 * Math.PI / (curCircleChecks - 1) * i; //check decimals
                    var pos = new Vector2((float) Math.Floor(heroPoint.X + curRadius * Math.Cos(cRadians)),
                        (float) Math.Floor(heroPoint.Y + curRadius * Math.Sin(cRadians)));
                    posTable.Add(InitPositionInfo(pos, extraDelayBuffer, extraEvadeDistance, lastMovePos,
                        lowestEvadeTimeSpell));
                }
            }

            IOrderedEnumerable<PositionInfo> sortedPosTable;

            if (ObjectCache.menuCache.cache["EvadeMode"].As<MenuList>().SelectedItem == "Fastest")
            {
                sortedPosTable =
                    posTable.OrderBy(p => p.isDangerousPos)
                        .ThenByDescending(p => p.intersectionTime)
                        .ThenBy(p => p.posDangerLevel)
                        .ThenBy(p => p.posDangerCount);

                fastEvadeMode = true;
            }
            else if (fastEvadeMode)
            {
                sortedPosTable =
                    posTable.OrderBy(p => p.isDangerousPos)
                        .ThenByDescending(p => p.intersectionTime)
                        .ThenBy(p => p.posDangerLevel)
                        .ThenBy(p => p.posDangerCount);
            }
            else if (ObjectCache.menuCache.cache["FastEvadeActivationTime"].As<MenuSlider>().Value > 0
                     && ObjectCache.menuCache.cache["FastEvadeActivationTime"].As<MenuSlider>().Value +
                     ObjectCache.gamePing + extraDelayBuffer > lowestEvadeTime)
            {
                sortedPosTable =
                    posTable.OrderBy(p => p.isDangerousPos)
                        .ThenByDescending(p => p.intersectionTime)
                        .ThenBy(p => p.posDangerLevel)
                        .ThenBy(p => p.posDangerCount);

                fastEvadeMode = true;
            }
            else
            {
                sortedPosTable =
                    posTable.OrderBy(p => p.rejectPosition)
                        .ThenBy(p => p.posDangerLevel)
                        .ThenBy(p => p.posDangerCount)
                        .ThenBy(p => p.distanceToMouse);

                if (sortedPosTable.First().posDangerCount != 0) //if can't dodge smoothly, dodge fast
                {
                    var sortedPosTableFastest =
                        posTable.OrderBy(p => p.isDangerousPos)
                            .ThenByDescending(p => p.intersectionTime)
                            .ThenBy(p => p.posDangerLevel)
                            .ThenBy(p => p.posDangerCount);

                    if (sortedPosTableFastest.First().posDangerCount == 0)
                    {
                        sortedPosTable = sortedPosTableFastest;
                        fastEvadeMode = true;
                    }
                }
            }

            foreach (var posInfo in sortedPosTable)
                // fix
                if (CheckPathCollision(myHero, posInfo.position) == false)
                {
                    if (fastEvadeMode)
                    {
                        posInfo.position = GetExtendedSafePosition(ObjectCache.myHeroCache.serverPos2D,
                            posInfo.position, extraEvadeDistance);
                        return CanHeroWalkToPos(posInfo.position, ObjectCache.myHeroCache.moveSpeed,
                            ObjectCache.gamePing, 0);
                    }

                    if (PositionInfoStillValid(posInfo))
                    {
                        if (posInfo.position.CheckDangerousPos(extraEvadeDistance)
                        ) //extra evade distance, no multiple skillshots
                            posInfo.position = GetExtendedSafePosition(ObjectCache.myHeroCache.serverPos2D,
                                posInfo.position, extraEvadeDistance);

                        return posInfo;
                    }
                }

            return PositionInfo.SetAllUndodgeable();
        }

        public static PositionInfo GetBestPositionMovementBlock(Vector2 movePos)
        {
            var posChecked = 0;
            var maxPosToCheck = 50;
            var posRadius = 50;
            var radiusIndex = 0;

            var extraEvadeDistance = ObjectCache.menuCache.cache["ExtraAvoidDistance"].As<MenuSlider>().Value;

            var heroPoint = ObjectCache.myHeroCache.serverPos2D;
            var lastMovePos = movePos; //Game.CursorPos.To2D(); //movePos

            var posTable = new List<PositionInfo>();

            var extraDist = ObjectCache.menuCache.cache["ExtraCPADistance"].As<MenuSlider>().Value;
            var extraDelayBuffer = ObjectCache.menuCache.cache["ExtraPingBuffer"].As<MenuSlider>().Value;

            while (posChecked < maxPosToCheck)
            {
                radiusIndex++;

                var curRadius = radiusIndex * 2 * posRadius;
                var curCircleChecks = (int) Math.Ceiling(2 * Math.PI * curRadius / (2 * (double) posRadius));

                for (var i = 1; i < curCircleChecks; i++)
                {
                    posChecked++;
                    var cRadians = 2 * Math.PI / (curCircleChecks - 1) * i; //check decimals
                    var pos = new Vector2((float) Math.Floor(heroPoint.X + curRadius * Math.Cos(cRadians)),
                        (float) Math.Floor(heroPoint.Y + curRadius * Math.Sin(cRadians)));

                    var posInfo = CanHeroWalkToPos(pos, ObjectCache.myHeroCache.moveSpeed,
                        extraDelayBuffer + ObjectCache.gamePing, extraDist);
                    posInfo.isDangerousPos = pos.CheckDangerousPos(6) || CheckMovePath(pos);
                    posInfo.distanceToMouse = pos.GetPositionValue();
                    posInfo.hasExtraDistance = extraEvadeDistance > 0 && pos.HasExtraAvoidDistance(extraEvadeDistance);

                    posTable.Add(posInfo);
                }
            }

            var sortedPosTable =
                posTable.OrderBy(p => p.isDangerousPos)
                    .ThenBy(p => p.posDangerLevel)
                    .ThenBy(p => p.hasExtraDistance)
                    .ThenBy(p => p.distanceToMouse);

            foreach (var posInfo in sortedPosTable)
                if (CheckPathCollision(myHero, posInfo.position) == false)
                    return posInfo;

            return null;
        }

        public static PositionInfo GetBestPositionBlink()
        {
            var posChecked = 0;
            var maxPosToCheck = 100;
            var posRadius = 50;
            var radiusIndex = 0;

            var extraEvadeDistance = Math.Max(100,
                ObjectCache.menuCache.cache["ExtraEvadeDistance"].As<MenuSlider>().Value);

            var heroPoint = ObjectCache.myHeroCache.serverPos2DPing;
            var lastMovePos = Game.CursorPos.To2D();

            var minComfortZone = ObjectCache.menuCache.cache["MinComfortZone"].As<MenuSlider>().Value;

            var posTable = new List<PositionInfo>();

            while (posChecked < maxPosToCheck)
            {
                radiusIndex++;

                var curRadius = radiusIndex * 2 * posRadius;
                var curCircleChecks = (int) Math.Ceiling(2 * Math.PI * curRadius / (2 * (double) posRadius));

                for (var i = 1; i < curCircleChecks; i++)
                {
                    posChecked++;
                    var cRadians = 2 * Math.PI / (curCircleChecks - 1) * i; //check decimals
                    var pos = new Vector2((float) Math.Floor(heroPoint.X + curRadius * Math.Cos(cRadians)),
                        (float) Math.Floor(heroPoint.Y + curRadius * Math.Sin(cRadians)));

                    var isDangerousPos = pos.CheckDangerousPos(6);
                    var dist = pos.GetPositionValue();

                    var posInfo = new PositionInfo(pos, isDangerousPos, dist);
                    posInfo.hasExtraDistance = extraEvadeDistance > 0
                        ? pos.CheckDangerousPos(extraEvadeDistance)
                        : false;

                    posInfo.posDistToChamps = pos.GetDistanceToChampions();

                    if (minComfortZone < posInfo.posDistToChamps)
                        posTable.Add(posInfo);
                }
            }

            var sortedPosTable =
                posTable.OrderBy(p => p.isDangerousPos)
                    .ThenBy(p => p.hasExtraDistance)
                    .ThenBy(p => p.distanceToMouse);

            foreach (var posInfo in sortedPosTable)
                if (CheckPointCollision(myHero, posInfo.position) == false)
                    return posInfo;

            return null;
        }

        public static PositionInfo GetBestPositionDash(EvadeSpellData spell)
        {
            var posChecked = 0;
            var maxPosToCheck = 100;
            var posRadius = 50;
            var radiusIndex = 0;

            var extraDelayBuffer = ObjectCache.menuCache.cache["ExtraPingBuffer"].As<MenuSlider>().Value;
            var extraEvadeDistance = Math.Max(100,
                ObjectCache.menuCache.cache["ExtraEvadeDistance"].As<MenuSlider>().Value);
            var extraDist = ObjectCache.menuCache.cache["ExtraCPADistance"].As<MenuSlider>().Value;

            var heroPoint = ObjectCache.myHeroCache.serverPos2DPing;
            var lastMovePos = Game.CursorPos.To2D();

            var posTable = new List<PositionInfo>();
            var spellList = SpellDetector.GetSpellList();

            var minDistance = 50; //Math.Min(spell.range, minDistance)
            var maxDistance = int.MaxValue;

            if (spell.fixedRange)
                minDistance = maxDistance = (int) spell.range;

            while (posChecked < maxPosToCheck)
            {
                radiusIndex++;

                var curRadius = radiusIndex * 2 * posRadius + (minDistance - 2 * posRadius);
                var curCircleChecks = (int) Math.Ceiling(2 * Math.PI * curRadius / (2 * (double) posRadius));

                for (var i = 1; i < curCircleChecks; i++)
                {
                    posChecked++;
                    var cRadians = 2 * Math.PI / (curCircleChecks - 1) * i; //check decimals
                    var pos = new Vector2((float) Math.Floor(heroPoint.X + curRadius * Math.Cos(cRadians)),
                        (float) Math.Floor(heroPoint.Y + curRadius * Math.Sin(cRadians)));

                    var posInfo = CanHeroWalkToPos(pos, spell.speed, extraDelayBuffer + ObjectCache.gamePing,
                        extraDist);
                    posInfo.isDangerousPos = pos.CheckDangerousPos(6);
                    posInfo.hasExtraDistance = extraEvadeDistance > 0
                        ? pos.CheckDangerousPos(extraEvadeDistance)
                        : false; // ? 1 : 0;                    
                    posInfo.distanceToMouse = pos.GetPositionValue();
                    posInfo.spellList = spellList;

                    posInfo.posDistToChamps = pos.GetDistanceToChampions();

                    posTable.Add(posInfo);
                }

                if (curRadius >= maxDistance)
                    break;
            }

            var sortedPosTable =
                posTable.OrderBy(p => p.isDangerousPos)
                    .ThenBy(p => p.posDangerLevel)
                    .ThenBy(p => p.posDangerCount)
                    .ThenBy(p => p.hasExtraDistance)
                    .ThenBy(p => p.distanceToMouse);

            foreach (var posInfo in sortedPosTable)
                if (CheckPathCollision(myHero, posInfo.position) == false)
                    if (PositionInfoStillValid(posInfo, spell.speed))
                        return posInfo;

            return null;
        }

        public static PositionInfo GetBestPositionTargetedDash(EvadeSpellData spell)
        {
            var extraDelayBuffer = ObjectCache.menuCache.cache["ExtraPingBuffer"].As<MenuSlider>().Value;
            var extraEvadeDistance = Math.Max(100,
                ObjectCache.menuCache.cache["ExtraEvadeDistance"].As<MenuSlider>().Value);
            var extraDist = ObjectCache.menuCache.cache["ExtraCPADistance"].As<MenuSlider>().Value;

            var heroPoint = ObjectCache.myHeroCache.serverPos2DPing;
            var lastMovePos = Game.CursorPos.To2D();

            var posTable = new List<PositionInfo>();
            var spellList = SpellDetector.GetSpellList();

            var minDistance = 50; //Math.Min(spell.range, minDistance)
            var maxDistance = int.MaxValue;

            if (spell.fixedRange)
                minDistance = maxDistance = (int) spell.range;

            var collisionCandidates = new List<Obj_AI_Base>();

            if (spell.spellTargets.Contains(SpellTargets.Targetables))
            {
                foreach (var obj in ObjectManager.Get<Obj_AI_Base>()
                    .Where(h => !h.IsMe && h.IsValidTarget(spell.range, false)))
                    if (obj.Type != GameObjectType.obj_AI_Turret)
                        collisionCandidates.Add(obj);
            }
            else
            {
                var heroList = new List<Obj_AI_Hero>(); // Maybe change to IEnumerable

                if (spell.spellTargets.Contains(SpellTargets.EnemyChampions)
                    && spell.spellTargets.Contains(SpellTargets.AllyChampions))
                    heroList = GameObjects.Heroes.ToList();
                else if (spell.spellTargets.Contains(SpellTargets.EnemyChampions))
                    heroList = GameObjects.EnemyHeroes.ToList();
                else if (spell.spellTargets.Contains(SpellTargets.AllyChampions))
                    heroList = GameObjects.AllyHeroes.ToList();


                foreach (var hero in heroList.Where(h => !h.IsMe && h.IsValidTarget(spell.range)))
                    collisionCandidates.Add(hero);

                var minionList = new List<Obj_AI_Minion>();

                if (spell.spellTargets.Contains(SpellTargets.EnemyMinions)
                    && spell.spellTargets.Contains(SpellTargets.AllyMinions))
                    minionList = GameObjects.Minions.Where(m => m.Distance(myHero.ServerPosition) <= spell.range)
                        .ToList();
                else if (spell.spellTargets.Contains(SpellTargets.EnemyMinions))
                    minionList = GameObjects.EnemyMinions.Where(m => m.Distance(myHero.ServerPosition) <= spell.range)
                        .ToList();
                else if (spell.spellTargets.Contains(SpellTargets.AllyMinions))
                    minionList = GameObjects.AllyMinions.Where(m => m.Distance(myHero.ServerPosition) <= spell.range)
                        .ToList();

                foreach (var minion in minionList.Where(h => h.IsValidTarget(spell.range)))
                    collisionCandidates.Add(minion);
            }

            foreach (var candidate in collisionCandidates)
            {
                var pos = candidate.ServerPosition.To2D();

                PositionInfo posInfo;

                if (spell.spellName == "YasuoDashWrapper")
                {
                    var hasDashBuff = false;

                    foreach (var buff in candidate.Buffs)
                        if (buff.Name == "YasuoDashWrapper")
                        {
                            hasDashBuff = true;
                            break;
                        }

                    if (hasDashBuff)
                        continue;
                }

                if (spell.behindTarget)
                {
                    var dir = (pos - heroPoint).Normalized();
                    pos = pos + dir * (candidate.BoundingRadius + ObjectCache.myHeroCache.boundingRadius);
                }

                if (spell.infrontTarget)
                {
                    var dir = (pos - heroPoint).Normalized();
                    pos = pos - dir * (candidate.BoundingRadius + ObjectCache.myHeroCache.boundingRadius);
                }

                if (spell.fixedRange)
                {
                    var dir = (pos - heroPoint).Normalized();
                    pos = heroPoint + dir * spell.range;
                }

                if (spell.evadeType == EvadeType.Dash)
                {
                    posInfo = CanHeroWalkToPos(pos, spell.speed, extraDelayBuffer + ObjectCache.gamePing, extraDist);
                    posInfo.isDangerousPos = pos.CheckDangerousPos(6);
                    posInfo.distanceToMouse = pos.GetPositionValue();
                    posInfo.spellList = spellList;
                }
                else
                {
                    var isDangerousPos = pos.CheckDangerousPos(6);
                    var dist = pos.GetPositionValue();

                    posInfo = new PositionInfo(pos, isDangerousPos, dist);
                }

                posInfo.target = candidate;
                posTable.Add(posInfo);
            }

            if (spell.evadeType == EvadeType.Dash)
            {
                var sortedPosTable =
                    posTable.OrderBy(p => p.isDangerousPos)
                        .ThenBy(p => p.posDangerLevel)
                        .ThenBy(p => p.posDangerCount)
                        .ThenBy(p => p.distanceToMouse);

                var first = sortedPosTable.FirstOrDefault();
                if (first != null && Evade.lastPosInfo != null && first.isDangerousPos == false
                    && Evade.lastPosInfo.posDangerLevel > first.posDangerLevel)
                    return first;
            }
            else
            {
                var sortedPosTable =
                    posTable.OrderBy(p => p.isDangerousPos)
                        //.ThenByDescending(p => p.hasComfortZone)
                        //.ThenBy(p => p.hasExtraDistance)
                        .ThenBy(p => p.distanceToMouse);

                var first = sortedPosTable.FirstOrDefault();

                return first;
            }

            return null;
        }

        public static bool CheckWindupTime(float windupTime)
        {
            foreach (var entry in SpellDetector.spells)
            {
                var spell = entry.Value;

                var hitTime = spell.GetSpellHitTime(ObjectCache.myHeroCache.serverPos2D);
                if (hitTime < windupTime)
                    return true;
            }

            return false;
        }

        public static float GetMovementBlockPositionValue(Vector2 pos, Vector2 movePos)
        {
            float value = 0; // pos.Distance(movePos);

            foreach (var entry in SpellDetector.spells)
            {
                var spell = entry.Value;
                var spellPos = spell.GetCurrentSpellPosition(true, ObjectCache.gamePing);
                var extraDist = 100 + spell.radius;

                value -= Math.Max(0,
                    -(10 * ((float) 0.8 * extraDist) / pos.Distance(spell.GetSpellProjection(pos))) + extraDist);
            }

            return value;
        }

        public static bool PositionInfoStillValid(PositionInfo posInfo, float moveSpeed = 0)
        {
            return true; //too buggy
        }

        public static List<Vector2> GetExtendedPositions(Vector2 from, Vector2 to, float extendDistance)
        {
            var direction = (to - from).Normalized();
            var positions = new List<Vector2>();
            float sectorDistance = 50;

            for (var i = sectorDistance; i < extendDistance; i += sectorDistance)
            {
                var pos = to + direction * i;

                positions.Add(pos);
            }

            return positions;
        }

        public static Vector2 GetExtendedSafePosition(Vector2 from, Vector2 to, float extendDistance)
        {
            var direction = (to - from).Normalized();
            var lastPosition = to;
            float sectorDistance = 50;

            for (var i = sectorDistance; i <= extendDistance; i += sectorDistance)
            {
                var pos = to + direction * i;

                if (pos.CheckDangerousPos(6)
                    || CheckPathCollision(myHero, pos))
                    return lastPosition;

                lastPosition = pos;
            }

            return lastPosition;
        }

        public static void CalculateEvadeTime()
        {
            foreach (var entry in SpellDetector.spells)
            {
                var spell = entry.Value;
                float evadeTime, spellHitTime;
                spell.CanHeroEvade(myHero, out evadeTime, out spellHitTime);

                spell.spellHitTime = spellHitTime;
                spell.evadeTime = evadeTime;
            }
        }

        public static Vector2 GetFastestPosition(Spell spell)
        {
            var heroPos = ObjectCache.myHeroCache.serverPos2D;

            if (spell.spellType == SpellType.Line)
            {
                var projection = heroPos.ProjectOn(spell.startPos, spell.endPos).SegmentPoint;
                return projection.Extend(heroPos, spell.radius + ObjectCache.myHeroCache.boundingRadius + 10);
            }
            if (spell.spellType == SpellType.Circular)
                return spell.endPos.Extend(heroPos, spell.radius + 10);

            return Vector2.Zero;
        }

        public static List<Vector2> GetFastestPositions()
        {
            var positions = new List<Vector2>();

            foreach (var entry in SpellDetector.spells)
            {
                var spell = entry.Value;
                var pos = GetFastestPosition(spell);


                if (pos != Vector2.Zero)
                    positions.Add(pos);
            }

            return positions;
        }

        public static float CompareFastestPosition(Spell spell, Vector2 start, Vector2 movePos)
        {
            var fastestPos = GetFastestPosition(spell);
            var moveDir = (movePos - start).Normalized();
            var fastestDir = (GetFastestPosition(spell) - start).Normalized();

            return moveDir.AngleBetween(fastestDir); // * (180 / ((float)Math.PI));
        }

        public static float GetMinCPADistance(Vector2 movePos)
        {
            var minDist = float.MaxValue;
            var heroPoint = ObjectCache.myHeroCache.serverPos2D;

            foreach (var spell in SpellDetector.spells.Values)
                minDist = Math.Min(minDist,
                    GetClosestDistanceApproach(spell, movePos, ObjectCache.myHeroCache.moveSpeed, ObjectCache.gamePing,
                        ObjectCache.myHeroCache.serverPos2DPing, 0));

            return minDist;
        }

        public static float GetCombinedIntersectionDistance(Vector2 movePos)
        {
            var heroPoint = ObjectCache.myHeroCache.serverPos2D;
            float sumIntersectDist = 0;

            foreach (var spell in SpellDetector.spells.Values)
            {
                var intersectDist = GetIntersectDistance(spell, heroPoint, movePos);
                sumIntersectDist += intersectDist * spell.dangerlevel;
            }

            return sumIntersectDist;
        }

        public static Vector3 GetNearWallPoint(Vector3 start, Vector3 end)
        {
            var direction = (end - start).Normalized();
            var distance = start.Distance(end);
            for (var i = 20; i < distance; i += 20)
            {
                var v = end - direction * i;

                /* if (!v.IsWall())*/
                if (!NavMesh.WorldToCell(v).Flags.HasFlag(NavCellFlags.Wall | NavCellFlags.Building))
                    return v;
            }

            return Vector3.Zero;
        }

        public static Vector3 GetNearWallPoint(Vector2 start, Vector2 end)
        {
            var direction = (end - start).Normalized();
            var distance = start.Distance(end);
            for (var i = 20; i < distance; i += 20)
            {
                var v = end - direction * i;
                // fix if (!v.IsWall())
                if (!NavMesh.WorldToCell(v.To3D()).Flags.HasFlag(NavCellFlags.Wall | NavCellFlags.Building))
                    return v.To3D();
            }

            return Vector3.Zero;
        }

        public static float GetIntersectDistance(Spell spell, Vector2 start, Vector2 end)
        {
            if (spell == null)
                return float.MaxValue;

            var startX = start.X;
            var startY = start.Y;
            /*SharpDX.*/
            var vec2Start = new /*SharpDX.*/Vector2(startX, startY);

            var endX = end.X;
            var endY = end.Y;
            /*SharpDX.*/
            var vec2End = new /*SharpDX.*/Vector2(startX, startY);

            /*SharpDX.*/
            var start3D = new /*SharpDX.*/Vector3(start.X, start.Y, 0);
            /*SharpDX.*/
            var walkDir = vec2End - vec2Start;
            /*SharpDX.*/
            var walkDir3D = new /*SharpDX.*/Vector3(walkDir.X, walkDir.Y, 0);

            //Ray heroPath = new Ray(start3D, walkDir3D);

            if (spell.spellType == SpellType.Line)
            {
                Vector2 intersection;
                var hasIntersection = spell.LineIntersectLinearSpellEx(start, end, out intersection);
                if (hasIntersection)
                    return start.Distance(intersection);
            }
            else if (spell.spellType == SpellType.Circular)
            {
                if (end.InSkillShot(spell, ObjectCache.myHeroCache.boundingRadius) == false)
                {
                    Vector2 intersection1, intersection2;
                    MathUtils.FindLineCircleIntersections(spell.endPos, spell.radius, start, end, out intersection1,
                        out intersection2);

                    if (intersection1.X != float.NaN && MathUtils.isPointOnLineSegment(intersection1, start, end))
                        return start.Distance(intersection1);
                    if (intersection2.X != float.NaN && MathUtils.isPointOnLineSegment(intersection2, start, end))
                        return start.Distance(intersection2);
                }
            }

            return float.MaxValue;
        }

        public static PositionInfo CanHeroWalkToPos(Vector2 pos, float speed, float delay, float extraDist,
            bool useServerPosition = true)
        {
            var posDangerLevel = 0;
            var posDangerCount = 0;
            var closestDistance = float.MaxValue;
            var dodgeableSpells = new List<int>();
            var undodgeableSpells = new List<int>();

            var heroPos = ObjectCache.myHeroCache.serverPos2D;

            var minComfortDistance = ObjectCache.menuCache.cache["MinComfortZone"].As<MenuSlider>().Value;

            if (useServerPosition == false)
                heroPos = myHero.Position.To2D();

            foreach (var entry in SpellDetector.spells)
            {
                var spell = entry.Value;

                closestDistance = Math.Min(closestDistance,
                    GetClosestDistanceApproach(spell, pos, speed, delay, heroPos, extraDist));

                if (pos.InSkillShot(spell, ObjectCache.myHeroCache.boundingRadius - 8)
                    || PredictSpellCollision(spell, pos, speed, delay, heroPos, extraDist, useServerPosition)
                    || spell.info.spellType != SpellType.Line && pos.isNearEnemy(minComfortDistance))
                {
                    posDangerLevel = Math.Max(posDangerLevel, spell.dangerlevel);
                    posDangerCount += spell.dangerlevel;
                    undodgeableSpells.Add(spell.spellID);
                }
                else
                {
                    dodgeableSpells.Add(spell.spellID);
                }
            }

            return new PositionInfo(
                pos,
                posDangerLevel,
                posDangerCount,
                posDangerCount > 0,
                closestDistance,
                dodgeableSpells,
                undodgeableSpells);
        }

        public static float GetClosestDistanceApproach(Spell spell, Vector2 pos, float speed, float delay,
            Vector2 heroPos, float extraDist)
        {
            var walkDir = (pos - heroPos).Normalized();

            if (spell.spellType == SpellType.Line && spell.info.projectileSpeed != float.MaxValue)
            {
                var spellPos = spell.GetCurrentSpellPosition(true, delay);
                var spellEndPos = spell.GetSpellEndPosition();
                var extendedPos = pos.ExtendDir(walkDir, ObjectCache.myHeroCache.boundingRadius + speed * delay / 1000);

                Vector2 cHeroPos;
                Vector2 cSpellPos;

                var cpa2 = MathUtils.GetCollisionDistanceEx(
                    heroPos, walkDir * speed, ObjectCache.myHeroCache.boundingRadius,
                    spellPos, spell.direction * spell.info.projectileSpeed, spell.radius + extraDist,
                    out cHeroPos, out cSpellPos);

                var cHeroPosProjection = cHeroPos.ProjectOn(heroPos, extendedPos);
                var cSpellPosProjection = cSpellPos.ProjectOn(spellPos, spellEndPos);

                if (cSpellPosProjection.IsOnSegment && cHeroPosProjection.IsOnSegment && cpa2 != float.MaxValue)
                    return 0;

                var cpa = MathUtilsCPA.CPAPointsEx(
                    heroPos, walkDir * speed, spellPos, spell.direction * spell.info.projectileSpeed,
                    pos, spellEndPos, out cHeroPos, out cSpellPos);

                cHeroPosProjection = cHeroPos.ProjectOn(heroPos, extendedPos);
                cSpellPosProjection = cSpellPos.ProjectOn(spellPos, spellEndPos);

                var checkDist = ObjectCache.myHeroCache.boundingRadius + spell.radius + extraDist;

                if (cSpellPosProjection.IsOnSegment && cHeroPosProjection.IsOnSegment)
                    return Math.Max(0, cpa - checkDist);

                return checkDist;

                //return MathUtils.ClosestTimeOfApproach(heroPos, walkDir * speed, spellPos, spell.Orientation * spell.info.projectileSpeed);
            }
            if (spell.spellType == SpellType.Line && spell.info.projectileSpeed == float.MaxValue)
            {
                var spellHitTime = Math.Max(0, spell.endTime - EvadeUtils.TickCount - delay); //extraDelay
                var walkRange = heroPos.Distance(pos);
                var predictedRange = speed * (spellHitTime / 1000);
                var tHeroPos = heroPos + walkDir * Math.Min(predictedRange, walkRange); //Hero predicted pos

                var projection = tHeroPos.ProjectOn(spell.startPos, spell.endPos);

                return Math.Max(0, tHeroPos.Distance(projection.SegmentPoint)
                                   - (spell.radius + ObjectCache.myHeroCache.boundingRadius +
                                      extraDist)); //+ dodgeBuffer
            }
            if (spell.spellType == SpellType.Circular)
            {
                var spellHitTime = Math.Max(0, spell.endTime - EvadeUtils.TickCount - delay); //extraDelay
                var walkRange = heroPos.Distance(pos);
                var predictedRange = speed * (spellHitTime / 1000);
                var tHeroPos = heroPos + walkDir * Math.Min(predictedRange, walkRange); //Hero predicted pos

                if (spell.info.spellName == "VeigarEventHorizon")
                {
                    var wallRadius = 65;
                    var midRadius = spell.radius - wallRadius;

                    if (spellHitTime == 0)
                        return 0;

                    return tHeroPos.Distance(spell.endPos) >= spell.radius
                        ? Math.Max(0, tHeroPos.Distance(spell.endPos) - midRadius - wallRadius)
                        : Math.Max(0, midRadius - tHeroPos.Distance(spell.endPos) - wallRadius);
                }

                if (spell.info.spellName == "DariusCleave")
                {
                    var wallRadius = 115;
                    var midRadius = spell.radius - wallRadius;

                    if (spellHitTime == 0)
                        return 0;

                    return tHeroPos.Distance(spell.endPos) >= spell.radius
                        ? Math.Max(0, tHeroPos.Distance(spell.endPos) - midRadius - wallRadius)
                        : Math.Max(0, midRadius - tHeroPos.Distance(spell.endPos) - wallRadius);
                }

                var closestDist = Math.Max(0, tHeroPos.Distance(spell.endPos) - (spell.radius + extraDist));
                if (spell.info.extraEndTime > 0 && closestDist != 0)
                {
                    var remainingTime = Math.Max(0,
                        spell.endTime + spell.info.extraEndTime - EvadeUtils.TickCount - delay);
                    var predictedRange2 = speed * (remainingTime / 1000);
                    var tHeroPos2 = heroPos + walkDir * Math.Min(predictedRange2, walkRange);

                    if (CheckMoveToDirection(tHeroPos, tHeroPos2))
                        return 0;
                }
                else
                {
                    return closestDist;
                }
            }
            else if (spell.spellType == SpellType.Arc)
            {
                var spellPos = spell.GetCurrentSpellPosition(true, delay);
                var spellEndPos = spell.GetSpellEndPosition();

                var pDir = spell.direction.Perpendicular();
                spellPos = spellPos - pDir * spell.radius / 2;
                spellEndPos = spellEndPos - pDir * spell.radius / 2;

                var extendedPos = pos.ExtendDir(walkDir, ObjectCache.myHeroCache.boundingRadius);

                Vector2 cHeroPos;
                Vector2 cSpellPos;

                var cpa = MathUtilsCPA.CPAPointsEx(heroPos, walkDir * speed, spellPos,
                    spell.direction * spell.info.projectileSpeed, pos, spellEndPos, out cHeroPos, out cSpellPos);

                var cHeroPosProjection = cHeroPos.ProjectOn(heroPos, extendedPos);
                var cSpellPosProjection = cSpellPos.ProjectOn(spellPos, spellEndPos);

                var checkDist = spell.radius + extraDist;

                if (cHeroPos.InSkillShot(spell, ObjectCache.myHeroCache.boundingRadius))
                    if (cSpellPosProjection.IsOnSegment && cHeroPosProjection.IsOnSegment)
                        return Math.Max(0, cpa - checkDist);
                    else
                        return checkDist;
            }
            else if (spell.spellType == SpellType.Cone)
            {
                var spellHitTime = Math.Max(0, spell.endTime - EvadeUtils.TickCount - delay); //extraDelay
                var walkRange = heroPos.Distance(pos);
                var predictedRange = speed * (spellHitTime / 1000);
                var tHeroPos = heroPos + walkDir * Math.Min(predictedRange, walkRange); //Hero predicted pos

                var sides = new[]
                {
                    heroPos.ProjectOn(spell.cnStart, spell.cnLeft).SegmentPoint,
                    heroPos.ProjectOn(spell.cnLeft, spell.cnRight).SegmentPoint,
                    heroPos.ProjectOn(spell.cnRight, spell.cnStart).SegmentPoint
                };

                var p = sides.OrderBy(x => x.Distance(x)).First();

                return Math.Max(0,
                    tHeroPos.Distance(p) - (spell.radius + ObjectCache.myHeroCache.boundingRadius + extraDist));
            }

            return 1;
        }

        public static bool PredictSpellCollision(Spell spell, Vector2 pos, float speed, float delay, Vector2 heroPos,
            float extraDist, bool useServerPosition = true)
        {
            extraDist = extraDist + 10;

            if (useServerPosition == false)
                return GetClosestDistanceApproach(spell, pos, speed, 0,
                           ObjectCache.myHeroCache.serverPos2D, 0) == 0;

            return
                GetClosestDistanceApproach(spell, pos, speed, delay, //Game.Ping + Extra Buffer
                    ObjectCache.myHeroCache.serverPos2DPing, extraDist) == 0
                || GetClosestDistanceApproach(spell, pos, speed, ObjectCache.gamePing, //Game.Ping
                    ObjectCache.myHeroCache.serverPos2DPing, extraDist) == 0;
        }

        public static Vector2 GetRealHeroPos(float delay = 0)
        {
            var path = myHero.Path;
            if (path.Length < 1)
                return ObjectCache.myHeroCache.serverPos2D;

            var serverPos = ObjectCache.myHeroCache.serverPos2D;
            var heroPos = myHero.Position.To2D();

            var walkDir = (path[0].To2D() - heroPos).Normalized();
            var realPos = heroPos + walkDir * ObjectCache.myHeroCache.moveSpeed * (delay / 1000);

            return realPos;
        }

        public static bool CheckPathCollision(Obj_AI_Base unit, Vector2 movePos)
        {
            // fix :
            //return false;
            var path = /*unit.Path;*/unit.GetPath(ObjectCache.myHeroCache.serverPos2D.To3D(), movePos.To3D());

            //var path = new Vector3[(int)movePos.Length + 1];

            //for (int i = 1; i < movePos.Length / 10; i++)
            //{
            //    path[i] = unit.Position.Extend(movePos.To3D(), i * 10);
            //}

            if (path.Length > 0)
                if (movePos.Distance(path[path.Length - 1].To2D()) > 5 || path.Length > 2)
                    return true;

            return false;
        }

        public static bool CheckPointCollision(Obj_AI_Base unit, Vector2 movePos)
        {
            // fix
            //return false;
            var path = /*unit.Path;*/unit.GetPath(movePos.To3D());
            //var path = new Vector3[(int)movePos.Length + 1];

            //for (int i = 1; i < movePos.Length / 10; i++)
            //{
            //    path[i] = unit.Position.Extend(movePos.To3D(), i * 10);
            //}

            if (path.Length > 0)
                if (movePos.Distance(path[path.Length - 1].To2D()) > 5)
                    return true;

            return false;
        }

        public static bool CheckMovePath(Vector2 movePos, float extraDelay = 0)
        {
            /*if (EvadeSpell.lastSpellEvadeCommand.evadeSpellData != null)
            {
                var evadeSpell = EvadeSpell.lastSpellEvadeCommand.evadeSpellData;
                float evadeTime = ObjectCache.gamePing;

                if (EvadeSpell.lastSpellEvadeCommand.evadeSpellData.evadeType == EvadeType.Dash)
                    evadeTime += evadeSpell.spellDelay + ObjectCache.myHeroCache.serverPos2D.Distance(movePos) / (evadeSpell.speed / 1000);
                else if (EvadeSpell.lastSpellEvadeCommand.evadeSpellData.evadeType == EvadeType.Blink)
                    evadeTime += evadeSpell.spellDelay;

                if (Evade.GetTickCount - EvadeSpell.lastSpellEvadeCommand.timestamp < evadeTime)
                {

                    Console.WriteLine("in" + CheckMoveToDirection(EvadeSpell.lastSpellEvadeCommand.targetPosition, movePos));
                    return CheckMoveToDirection(EvadeSpell.lastSpellEvadeCommand.targetPosition, movePos);
                }
            }*/

            var startPoint = myHero.Position;

            if (myHero.IsDashing())
            {
                var dashItem = myHero.GetDashInfo();
                startPoint = dashItem.EndPos.To3D();
            }

           
            var poopy = myHero.GetPath(startPoint, movePos.To3D()); //from serverpos


            var lastPoint = new Vector2();

            foreach (var point in poopy)
            {
                var point2D = point.To2D();
                if (lastPoint != Vector2.Zero && CheckMoveToDirection(lastPoint, point2D, extraDelay))
                    return true;

                if (lastPoint != Vector2.Zero)
                    lastPoint = point2D;
                else
                    lastPoint = myHero.ServerPosition.To2D();
            }

            return false;
        }


        public static bool LineIntersectLinearSegment(Vector2 a1, Vector2 b1, Vector2 a2, Vector2 b2)
        {
            const int segmentRadius = 55;

            var myBoundingRadius = ObjectManager.GetLocalPlayer().BoundingRadius;
            var segmentDir = (b1 - a1).Normalized().Perpendicular();
            var segmentStart = a1;
            var segmentEnd = b1;

            var startRightPos = segmentStart + segmentDir * (segmentRadius + myBoundingRadius);
            var startLeftPos = segmentStart - segmentDir * (segmentRadius + myBoundingRadius);
            var endRightPos = segmentEnd + segmentDir * (segmentRadius + myBoundingRadius);
            var endLeftPos = segmentEnd - segmentDir * (segmentRadius + myBoundingRadius);

            var int1 = MathUtils.CheckLineIntersection(a2, b2, startRightPos, startLeftPos);
            var int2 = MathUtils.CheckLineIntersection(a2, b2, endRightPos, endLeftPos);
            var int3 = MathUtils.CheckLineIntersection(a2, b2, startRightPos, endRightPos);
            var int4 = MathUtils.CheckLineIntersection(a2, b2, startLeftPos, endLeftPos);

            if (int1 || int2 || int3 || int4)
                return true;

            return false;
        }

        public static bool CheckMoveToDirection(Vector2 from, Vector2 movePos, float extraDelay = 0)
        {
            var dir = (movePos - from).Normalized();
            //movePos = movePos.ExtendDir(dir, ObjectCache.myHeroCache.boundingRadius);

            foreach (var entry in SpellDetector.spells)
            {
                var spell = entry.Value;

                if (!from.InSkillShot(spell, ObjectCache.myHeroCache.boundingRadius))
                {
                    var spellPos = spell.currentSpellPosition;

                    if (spell.spellType == SpellType.Line)
                    {
                        if (spell.LineIntersectLinearSpell(from, movePos))
                        {
                            return true;
                        }
                    }
                    else if (spell.spellType == SpellType.Circular)
                    {
                        if (spell.info.spellName == "VeigarEventHorizon")
                        {
                            var cpa2 = MathUtilsCPA.CPAPointsEx(from, dir * ObjectCache.myHeroCache.moveSpeed,
                                spell.endPos, new Vector2(0, 0), movePos, spell.endPos);

                            if (from.Distance(spell.endPos) < spell.radius &&
                                !(from.Distance(spell.endPos) < spell.radius - 135 &&
                                  movePos.Distance(spell.endPos) < spell.radius - 135))
                                return true;
                            if (from.Distance(spell.endPos) > spell.radius && cpa2 < spell.radius + 10)
                                return true;
                        }
                        else if (spell.info.spellName == "DariusCleave")
                        {
                            var cpa3 = MathUtilsCPA.CPAPointsEx(from, dir * ObjectCache.myHeroCache.moveSpeed,
                                spell.endPos, new Vector2(0, 0), movePos, spell.endPos);

                            if (from.Distance(spell.endPos) < spell.radius &&
                                !(from.Distance(spell.endPos) < spell.radius - 230 &&
                                  movePos.Distance(spell.endPos) < spell.radius - 230))
                                return true;
                            if (from.Distance(spell.endPos) > spell.radius && cpa3 < spell.radius + 10)
                                return true;
                        }
                        else
                        {
                            Vector2 cHeroPos;
                            Vector2 cSpellPos;

                            var cpa2 = MathUtils.GetCollisionDistanceEx(
                                from, dir * ObjectCache.myHeroCache.moveSpeed, 1,
                                spell.endPos, new Vector2(0, 0), spell.radius,
                                out cHeroPos, out cSpellPos);

                            if (spell.info.spellName.Contains("_trap") && !(cpa2 < spell.radius + 10))
                                continue;

                            var cHeroPosProjection = cHeroPos.ProjectOn(from, movePos);
                            if (cHeroPosProjection.IsOnSegment && cpa2 != float.MaxValue)
                                return true;
                        }
                    }
                    else if (spell.spellType == SpellType.Arc)
                    {
                        if (from.isLeftOfLineSegment(spell.startPos, spell.endPos))
                            return MathUtils.CheckLineIntersection(from, movePos, spell.startPos, spell.endPos);

                        var spellRange = spell.startPos.Distance(spell.endPos);
                        var midPoint = spell.startPos + spell.direction * (spellRange / 2);

                        var cpa = MathUtilsCPA.CPAPointsEx(from, dir * ObjectCache.myHeroCache.moveSpeed, midPoint,
                            new Vector2(0, 0), movePos, midPoint);

                        if (cpa < spell.radius + 10)
                            return true;
                    }
                    else if (spell.spellType == SpellType.Cone)
                    {
                        if (LineIntersectLinearSegment(spell.cnStart, spell.cnLeft, from, movePos) ||
                            LineIntersectLinearSegment(spell.cnLeft, spell.cnRight, from, movePos) ||
                            LineIntersectLinearSegment(spell.cnRight, spell.cnStart, from, movePos))
                            return true;
                    }
                }
                else if (from.InSkillShot(spell, ObjectCache.myHeroCache.boundingRadius))
                {
                    if (movePos.InSkillShot(spell, ObjectCache.myHeroCache.boundingRadius))
                        return true;
                }
            }

            return false;
        }
    }
}