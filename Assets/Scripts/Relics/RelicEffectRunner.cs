using Soup.Employees;
using Soup.Events;
using Soup.Game;
using Soup.Items;
using Soup.Jobs;
using Soup.Levels;
using System.Collections.Generic;
using UnityEngine;

namespace Soup.Relics
{
    /// <summary>
    /// Evaluates owned relic rules for a given trigger against RelicContext.
    /// </summary>
    public static class RelicEffectRunner
    {
        public static void Run(RelicTrigger trigger, RelicContext context)
        {
            if (context == null) return;
            if (trigger == RelicTrigger.Passive) return;

            var manager = RelicManager.Instance;
            if (manager == null) return;

            // Snapshot: effects may Acquire more relics mid-iteration.
            var owned = manager.Owned;
            var snapshot = new List<RelicItem>(owned.Count);
            for (int i = 0; i < owned.Count; i++)
                snapshot.Add(owned[i]);

            for (int i = 0; i < snapshot.Count; i++)
                RunRelic(snapshot[i], trigger, context);
        }

        public static void RunRelic(RelicItem relic, RelicTrigger trigger, RelicContext context)
        {
            if (relic == null || context == null || relic.Rules == null) return;
            if (trigger == RelicTrigger.Passive) return;

            for (int r = 0; r < relic.Rules.Count; r++)
            {
                var rule = relic.Rules[r];
                if (rule == null || rule.Trigger != trigger) continue;
                // 炖煮吱吱的处理食材改由进关检测发放，避免 OnAcquire / LevelStart 规则与关卡清库存打架。
                if (relic.Id == RelicManager.StewedZhizhiId && rule.Effect == RelicEffectType.AddProcessed)
                    continue;
                if (!EvaluateCondition(rule, context)) continue;
                ApplyEffect(rule, context);
            }
        }

        public static bool EvaluateCondition(RelicRule rule, RelicContext context)
        {
            if (rule == null || context == null) return false;

            switch (rule.Condition)
            {
                case RelicConditionType.Always:
                    return true;
                case RelicConditionType.NoCategoryGathered:
                    // 有对应分类的未摧毁采集岗则不触发（与本回合是否实际采到无关）。
                    return !HasActiveGatherJobOfCategory(rule.ConditionCategory);
                case RelicConditionType.HasFlavorCountAtLeast:
                    return context.CountPresentFlavors() >= rule.ConditionInt;
                case RelicConditionType.HasFlavorCountAtMost:
                    return context.CountPresentFlavors() <= rule.ConditionInt;
                case RelicConditionType.TurnIndexInRange:
                {
                    int turn = context.LevelTurnNumber;
                    int min = rule.ConditionInt;
                    int max = rule.ConditionIntMax;
                    if (max < min)
                        (min, max) = (max, min);
                    return turn >= min && turn <= max;
                }
                case RelicConditionType.WarehouseSpaceBelowHalf:
                    return IsWarehouseSpaceBelowHalf(context.Store ?? ResourceStore.Instance);
                case RelicConditionType.LastNLevelTurns:
                    return IsInLastNLevelTurns(context.LevelTurnNumber, rule.ConditionInt);
                case RelicConditionType.AllEmployeesOnCook:
                    return EmployeeManager.Instance != null
                           && EmployeeManager.Instance.AreAllEmployeesOnCookJobs();
                default:
                    return false;
            }
        }

        /// <summary>空闲仓库量 &lt; 仓库总量一半。</summary>
        public static bool IsWarehouseSpaceBelowHalf(ResourceStore store)
        {
            if (store == null) return false;
            int cap = store.WarehouseCapacity;
            if (cap <= 0) return false; // 不限容量时不触发
            return store.WarehouseSpace < cap / 2;
        }

        /// <summary>本关是否处于最后 n 个回合（n≤0 视为不触发）。</summary>
        public static bool IsInLastNLevelTurns(int levelTurnNumber, int lastN)
        {
            if (lastN <= 0) return false;
            int maxTurns = 10;
            var levels = LevelManager.Instance;
            if (levels != null && levels.Current != null)
                maxTurns = Mathf.Max(1, levels.Current.MaxTurns);
            int turn = Mathf.Max(1, levelTurnNumber);
            int start = Mathf.Max(1, maxTurns - lastN + 1);
            return turn >= start && turn <= maxTurns;
        }

        /// <summary>
        /// 是否仍持有产出该分类食材的未摧毁采集岗（小白花摧毁后会从解锁列表移除）。
        /// </summary>
        public static bool HasActiveGatherJobOfCategory(IngredientCategory category)
        {
            var progression = JobProgressionManager.Instance;
            if (progression == null) return false;

            var unlocked = progression.GetUnlocked(JobType.Gather);
            for (int i = 0; i < unlocked.Count; i++)
            {
                var job = unlocked[i];
                if (job == null || progression.IsDestroyedGatherJob(job)) continue;

                var ingredient = job.OutputIngredient;
                if (progression != null)
                {
                    var def = progression.ResolveGatherDefinition(job);
                    if (def != null)
                        ingredient = def.OutputIngredient;
                }
                if (ingredient == null) continue;
                if (ingredient.Category == category)
                    return true;
                if (category == IngredientCategory.Meat && ingredient.HasTag("肉类"))
                    return true;
            }

            return false;
        }

        public static void ApplyEffect(RelicRule rule, RelicContext context)
        {
            if (rule == null || context == null) return;

            switch (rule.Effect)
            {
                case RelicEffectType.AddFinalMultiplier:
                    context.FinalMultiplier += rule.FloatValue;
                    break;

                case RelicEffectType.AddFinalMultiplierPerPresentFlavor:
                    context.FinalMultiplier += rule.FloatValue * context.CountPresentFlavors();
                    break;

                case RelicEffectType.DisableSpicyCap:
                    context.SpicyUncapped = true;
                    break;

                case RelicEffectType.AddSpicyScoreMultiplier:
                    context.SpicyScoreMultiplierBonus += rule.FloatValue;
                    break;

                case RelicEffectType.GrantIngredientPerGather:
                {
                    int every = rule.IntValue;
                    int grantAmount = rule.Amount;
                    var ingredient = ResolveSporeInvasionMushroom(rule.Ingredient);
                    if (every <= 0 || grantAmount <= 0 || ingredient == null) break;
                    if (context.ApplyYield == null) break;

                    int grants = context.GatheredUnits / every;
                    if (grants <= 0) break;

                    int units = grants * grantAmount;
                    var yield = IngredientYieldResolver.FromIngredient(ingredient, units);
                    context.ApplyYield(yield);
                    break;
                }

                case RelicEffectType.ModifyWarehouseCapacity:
                {
                    // Capacity from this effect is applied live via SumOwnedWarehouseCapacityBonus
                    // (avoids OnAcquire / undo / save desync that made the warehouse "shrink").
                    ResourceStore.Instance?.NotifyChanged();
                    break;
                }

                case RelicEffectType.AddRawMaterial:
                {
                    var store = context.Store ?? ResourceStore.Instance;
                    if (store == null || rule.Amount == 0) break;
                    int added = store.AddRaw(rule.Material, rule.Amount);
                    if (context.Result != null && added > 0)
                        context.Result.RawGained += added;
                    break;
                }

                case RelicEffectType.AddProcessed:
                {
                    var store = context.Store ?? ResourceStore.Instance;
                    if (store == null || rule.Amount == 0) break;
                    store.AddProcessed(rule.Amount);
                    if (context.Result != null && rule.Amount > 0)
                        context.Result.ProcessedGained += rule.Amount;
                    break;
                }

                case RelicEffectType.MultiplyIndependentScore:
                    if (rule.FloatValue > 0f)
                        context.IndependentMultiplier *= rule.FloatValue;
                    break;

                case RelicEffectType.ModifyElfCount:
                {
                    if (rule.Amount == 0) break;
                    ElfManager.Instance?.AddElves(rule.Amount);
                    break;
                }

                case RelicEffectType.GrantEmployee:
                {
                    if (rule.Amount == 0) break;
                    string id = string.IsNullOrEmpty(rule.EmployeeTypeId)
                        ? EmployeeManager.GhostId
                        : rule.EmployeeTypeId;
                    EmployeeManager.Instance?.Add(id, rule.Amount);
                    break;
                }

                case RelicEffectType.ConvertAllGatherToHappyTuotuo:
                    JobProgressionManager.Instance?.ConvertAllGatherJobsToHappyTuotuo();
                    break;

                case RelicEffectType.AddAdvanceChargesAllZones:
                {
                    int n = rule.Amount != 0 ? rule.Amount : 1;
                    ApplyAdvanceChargesAllZones(n);
                    break;
                }

                case RelicEffectType.ConvertElvesToGhosts:
                {
                    int cost = rule.IntValue > 0 ? rule.IntValue : 3;
                    int gain = rule.Amount > 0 ? rule.Amount : 4;
                    EmployeeManager.Instance?.TryConvertElvesToGhosts(cost, gain);
                    break;
                }

                case RelicEffectType.ConvertToughSolidFractionToSoft:
                {
                    var store = context.Store ?? ResourceStore.Instance;
                    if (store == null) break;
                    float frac = rule.FloatValue > 0f ? Mathf.Clamp01(rule.FloatValue) : 0.75f;
                    int fromTough = ConvertMaterialFractionToSoft(store, IngredientMaterial.Tough, frac);
                    int fromSolid = ConvertMaterialFractionToSoft(store, IngredientMaterial.Solid, frac);
                    int gained = fromTough + fromSolid;
                    if (context.Result != null && gained > 0)
                        context.Result.RawGained += gained;
                    break;
                }

                case RelicEffectType.PresentBonusStageEvents:
                {
                    int n = rule.Amount > 0 ? rule.Amount : 3;
                    var events = EventManager.Instance;
                    if (events == null) break;
                    int presented = events.PresentBonusStageEvents(n);
                    if (presented > 0)
                    {
                        var rewards = LevelManager.Instance?.ClearRewards;
                        if (rewards != null)
                        {
                            rewards.NotifyStageEventsPresented();
                            rewards.ReopenEventsForBonus();
                        }
                    }
                    // 弹窗由 EventPresented → EventPanelUI 统一处理，避免重复打开。
                    break;
                }

                case RelicEffectType.GrantLinkedRelic:
                {
                    if (rule.LinkedRelic == null) break;
                    RelicManager.Instance?.Acquire(rule.LinkedRelic);
                    break;
                }

                case RelicEffectType.GrantRawPerRawProduced:
                {
                    var store = context.Store ?? ResourceStore.Instance;
                    if (store == null) break;
                    int every = rule.IntValue;
                    int grant = rule.Amount;
                    if (every <= 0 || grant <= 0) break;

                    int produced = rule.Material == IngredientMaterial.Solid
                        ? context.SolidProducedThisBatch
                        : 0;
                    if (produced <= 0) break;

                    int extras = (produced / every) * grant;
                    if (extras <= 0) break;
                    int added = store.AddRaw(rule.Material, extras);
                    if (context.Result != null && added > 0)
                        context.Result.RawGained += added;
                    break;
                }

                case RelicEffectType.GrantRawPerGather:
                {
                    var store = context.Store ?? ResourceStore.Instance;
                    if (store == null) break;
                    int every = rule.IntValue;
                    int grant = rule.Amount;
                    if (every <= 0 || grant <= 0) break;

                    int extras = (context.GatheredUnits / every) * grant;
                    if (extras <= 0) break;
                    int added = store.AddRaw(rule.Material, extras);
                    if (context.Result != null && added > 0)
                        context.Result.RawGained += added;
                    break;
                }

                case RelicEffectType.GrantSoftFromUnusedWarehousePercent:
                {
                    var store = context.Store ?? ResourceStore.Instance;
                    if (store == null) break;
                    float pct = rule.FloatValue;
                    if (pct <= 0f) break;
                    int soft = Mathf.FloorToInt(context.PreviousUnusedWarehouse * pct);
                    if (soft <= 0) break;
                    int added = store.AddRaw(IngredientMaterial.Soft, soft);
                    if (context.Result != null && added > 0)
                        context.Result.RawGained += added;
                    break;
                }

                case RelicEffectType.ChanceGrantRandomRaw:
                {
                    var store = context.Store ?? ResourceStore.Instance;
                    if (store == null || rule.Amount <= 0) break;
                    if (Random.value > Mathf.Clamp01(rule.FloatValue)) break;
                    var mat = (IngredientMaterial)Random.Range(0, 3);
                    int added = store.AddRaw(mat, rule.Amount);
                    if (context.Result != null && added > 0)
                        context.Result.RawGained += added;
                    break;
                }

                // Passive-only effects — handled by query helpers below.
                case RelicEffectType.AddGlobalLaborEfficiency:
                case RelicEffectType.AddEmployeeTypeLaborEfficiency:
                case RelicEffectType.AddProcessLaborEfficiency:
                case RelicEffectType.AddCookLaborEfficiency:
                case RelicEffectType.AddCookOutputWasteFraction:
                case RelicEffectType.GrantEmployeeOnElfLoss:
                case RelicEffectType.AddGatherAmountPerWorker:
                case RelicEffectType.ReduceWarehouseWaste:
                case RelicEffectType.ConvertWasteToEqualGain:
                case RelicEffectType.AddColdScorePerUnit:
                case RelicEffectType.ReduceMagicConsumePercent:
                case RelicEffectType.OverrideSourTopTierPercent:
                case RelicEffectType.OverrideSourSecondTierPercent:
                case RelicEffectType.AddAllJobMaxWorkers:
                    break;
            }
        }

        /// <summary>处理岗效率叠加值（不含 +1 底数；条件不满足的规则不计）。</summary>
        public static float SumProcessLaborEfficiency(int levelTurnNumber = -1)
        {
            if (levelTurnNumber < 0)
                levelTurnNumber = RelicManager.GetLevelTurnNumber();
            float sum = 0f;
            ForEachPassiveRelicRule(levelTurnNumber, (_, rule) =>
            {
                if (rule.Effect == RelicEffectType.AddProcessLaborEfficiency)
                    sum += rule.FloatValue;
            });
            return sum;
        }

        /// <summary>烹饪岗效率叠加值（不含 +1 底数；条件不满足的规则不计）。</summary>
        public static float SumCookLaborEfficiency(int levelTurnNumber = -1)
        {
            if (levelTurnNumber < 0)
                levelTurnNumber = RelicManager.GetLevelTurnNumber();
            float sum = 0f;
            ForEachPassiveRelicRule(levelTurnNumber, (_, rule) =>
            {
                if (rule.Effect == RelicEffectType.AddCookLaborEfficiency)
                    sum += rule.FloatValue;
            });
            return sum;
        }

        /// <summary>烹饪产出浪费比例叠加（0.2 = 浪费 20%；上限 1）。</summary>
        public static float SumCookOutputWasteFraction(int levelTurnNumber = -1)
        {
            if (levelTurnNumber < 0)
                levelTurnNumber = RelicManager.GetLevelTurnNumber();
            float sum = 0f;
            ForEachPassiveRelicRule(levelTurnNumber, (_, rule) =>
            {
                if (rule.Effect == RelicEffectType.AddCookOutputWasteFraction)
                    sum += Mathf.Max(0f, rule.FloatValue);
            });
            return Mathf.Clamp01(sum);
        }

        /// <summary>按遗物浪费比例扣减烹饪产出（回收器可将浪费转为额外产出）。</summary>
        public static int ApplyCookOutputWaste(int cooked)
        {
            if (cooked <= 0) return 0;
            float wasteFrac = SumCookOutputWasteFraction();
            if (wasteFrac <= 0f) return cooked;

            int wasted = Mathf.FloorToInt(cooked * wasteFrac);
            if (wasted <= 0) return cooked;

            float wasteMult = WasteConvertMultiplier();
            if (wasteMult > 0f)
                return cooked + GameMath.CeilToInt(wasted * wasteMult);
            return Mathf.Max(0, cooked - wasted);
        }

        /// <summary>施工队等：通关发放进阶次数时，采集/处理/烹饪各额外加的次数。</summary>
        public static int SumExtraAdvanceChargesAllZones()
        {
            var manager = RelicManager.Instance;
            if (manager == null) return 0;

            int sum = 0;
            var owned = manager.Owned;
            for (int i = 0; i < owned.Count; i++)
            {
                var relic = owned[i];
                if (relic?.Rules == null) continue;
                for (int r = 0; r < relic.Rules.Count; r++)
                {
                    var rule = relic.Rules[r];
                    if (rule == null || rule.Effect != RelicEffectType.AddAdvanceChargesAllZones)
                        continue;
                    sum += rule.Amount != 0 ? Mathf.Abs(rule.Amount) : 1;
                }
            }

            return sum;
        }

        /// <summary>当前关卡间奖励会话中，三区进阶次数各 +n（购买施工队时即时生效）。</summary>
        public static void ApplyAdvanceChargesAllZones(int n)
        {
            if (n == 0) return;
            var levels = LevelManager.Instance;
            var session = levels != null ? levels.ClearRewards : null;
            if (session == null || !session.IsActive) return;
            session.AddAdvanceCharges(n, n, n);
            GameFloatingToast.Show($"三区进阶机会各 +{Mathf.Abs(n)}", 2.8f);
        }

        /// <summary>消耗 stock×frac 的指定材质，转为等量柔软；返回生成柔软量。</summary>
        public static int ConvertMaterialFractionToSoft(
            ResourceStore store,
            IngredientMaterial material,
            float fraction)
        {
            if (store == null || fraction <= 0f) return 0;
            if (material != IngredientMaterial.Tough && material != IngredientMaterial.Solid)
                return 0;

            int stock = store.GetRaw(material);
            if (stock <= 0) return 0;
            int take = Mathf.FloorToInt(stock * Mathf.Clamp01(fraction));
            if (take <= 0) return 0;
            int consumed = store.ConsumeRawUpTo(material, take);
            if (consumed <= 0) return 0;
            return store.AddRaw(IngredientMaterial.Soft, consumed);
        }

        /// <summary>激励/疲惫遗物叠加值（不含 +1 底数）。</summary>
        public static float SumIncentiveFatigueLaborModifier(int levelTurnNumber)
        {
            float sum = 0f;
            float incentiveAmp = JobAdvanceGatherMods.SumIncentiveEffectAmplify();
            float fatigueReduce = JobAdvanceGatherMods.SumFatigueEffectReduce();

            ForEachPassiveRelicRule(levelTurnNumber, (relic, rule) =>
            {
                if (rule.Effect != RelicEffectType.AddGlobalLaborEfficiency) return;
                if (relic == null) return;
                if (relic.Id != RelicManager.IncentiveId && relic.Id != RelicManager.FatigueId) return;

                float value = rule.FloatValue;
                if (relic.Id == RelicManager.IncentiveId)
                    value *= 1f + incentiveAmp;
                else
                    value *= 1f - fatigueReduce;
                sum += value;
            });

            return sum;
        }

        /// <summary>除激励/疲惫外，全局工作效率遗物叠加值（不含 +1 底数）。</summary>
        public static float SumRelicLaborModifierExcludingIncentiveFatigue(int levelTurnNumber)
        {
            float sum = 0f;
            ForEachPassiveRelicRule(levelTurnNumber, (relic, rule) =>
            {
                if (rule.Effect != RelicEffectType.AddGlobalLaborEfficiency) return;
                if (relic != null
                    && (relic.Id == RelicManager.IncentiveId || relic.Id == RelicManager.FatigueId))
                    return;
                sum += rule.FloatValue;
            });
            return sum;
        }

        /// <summary>
        /// 全部工作效率加成叠加值（兼容旧接口；不含 +1 底数）。
        /// 不含快乐坨坨光环——该层由 <see cref="WorkEfficiencyResolver"/> 单独乘算。
        /// </summary>
        public static float SumGlobalLaborEfficiency(int levelTurnNumber)
        {
            return SumIncentiveFatigueLaborModifier(levelTurnNumber)
                   + SumRelicLaborModifierExcludingIncentiveFatigue(levelTurnNumber);
        }

        public static float SumEmployeeLaborEfficiency(string employeeTypeId, int levelTurnNumber)
        {
            if (string.IsNullOrEmpty(employeeTypeId)) return 0f;
            float sum = 0f;
            ForEachPassiveRelicRule(levelTurnNumber, (_, rule) =>
            {
                if (rule.Effect != RelicEffectType.AddEmployeeTypeLaborEfficiency) return;
                string id = string.IsNullOrEmpty(rule.EmployeeTypeId)
                    ? EmployeeManager.GhostId
                    : rule.EmployeeTypeId;
                if (id == employeeTypeId)
                    sum += rule.FloatValue;
            });
            return sum;
        }

        public static int SumGhostsPerElfLoss()
        {
            int sum = 0;
            ForEachPassiveRelicRule(1, (_, rule) =>
            {
                if (rule.Effect == RelicEffectType.GrantEmployeeOnElfLoss)
                    sum += Mathf.Max(0, rule.Amount);
            });
            return sum;
        }

        /// <summary>Passive: extra gather amount-per-worker from relics (e.g. 丰饶祝福).</summary>
        public static int SumGatherAmountPerWorkerBonus()
        {
            int sum = 0;
            ForEachPassiveRelicRule(1, (_, rule) =>
            {
                if (rule.Effect == RelicEffectType.AddGatherAmountPerWorker)
                    sum += rule.Amount;
            });
            return sum;
        }

        /// <summary>Passive: all capped jobs gain this many max workers (人满为患).</summary>
        public static int SumAllJobMaxWorkersBonus()
        {
            int sum = 0;
            ForEachPassiveRelicRule(1, (_, rule) =>
            {
                if (rule.Effect == RelicEffectType.AddAllJobMaxWorkers)
                    sum += rule.Amount;
            });
            return sum;
        }

        /// <summary>
        /// Owned relics with <see cref="RelicEffectType.ModifyWarehouseCapacity"/> (e.g. 大仓库 +4000).
        /// Applied live in <see cref="ResourceStore.WarehouseCapacity"/> so acquire/undo/save stay in sync.
        /// </summary>
        public static int SumOwnedWarehouseCapacityBonus()
        {
            var manager = RelicManager.Instance;
            if (manager == null) return 0;

            int sum = 0;
            var owned = manager.Owned;
            for (int i = 0; i < owned.Count; i++)
            {
                var relic = owned[i];
                if (relic == null || relic.Rules == null) continue;
                for (int r = 0; r < relic.Rules.Count; r++)
                {
                    var rule = relic.Rules[r];
                    if (rule == null || rule.Effect != RelicEffectType.ModifyWarehouseCapacity)
                        continue;
                    sum += rule.Amount;
                }
            }

            return sum;
        }

        /// <summary>
        /// Passive: fraction of overflow waste prevented (0.75 = keep 75% that would be discarded).
        /// Clamped to [0, 1].
        /// </summary>
        public static float SumWarehouseWasteReduction()
        {
            float sum = 0f;
            ForEachPassiveRelicRule(1, (_, rule) =>
            {
                if (rule.Effect == RelicEffectType.ReduceWarehouseWaste)
                    sum += Mathf.Max(0f, rule.FloatValue);
            });
            return Mathf.Clamp01(sum);
        }

        /// <summary>
        /// Passive: wasted ingredients convert into processed food × multiplier.
        /// 0 = off; 回收器 amount=2 → 双倍.
        /// </summary>
        public static float WasteConvertMultiplier()
        {
            float mult = 0f;
            ForEachPassiveRelicRule(1, (_, rule) =>
            {
                if (rule.Effect != RelicEffectType.ConvertWasteToEqualGain) return;
                float m = rule.Amount > 0 ? rule.Amount : 1f;
                if (m > mult) mult = m;
            });
            return mult;
        }

        /// <summary>Backward-compatible: any waste→gain relic present.</summary>
        public static bool ConvertsWasteToEqualGain() => WasteConvertMultiplier() > 0f;

        /// <summary>
        /// Passive: extra score per cold-cooked unit (冰点 amount=2 → 每份 2→4 分).
        /// </summary>
        public static int SumColdScorePerCookedUnit()
        {
            int sum = 0;
            ForEachPassiveRelicRule(1, (_, rule) =>
            {
                if (rule.Effect == RelicEffectType.AddColdScorePerUnit)
                    sum += Mathf.Max(0, rule.Amount);
            });
            return sum;
        }

        /// <summary>Obsolete name; use <see cref="SumColdScorePerCookedUnit"/>.</summary>
        public static int SumColdScorePerUnit() => SumColdScorePerCookedUnit();

        /// <summary>Passive: absolute magic consume rate reduction (0.2 = −20pp, 50%→30%).</summary>
        public static float SumMagicConsumeReduction()
        {
            float sum = 0f;
            ForEachPassiveRelicRule(1, (_, rule) =>
            {
                if (rule.Effect == RelicEffectType.ReduceMagicConsumePercent)
                    sum += Mathf.Max(0f, rule.FloatValue);
            });
            return Mathf.Clamp01(sum);
        }

        /// <summary>
        /// Passive: sour best-tier cooked percent override (e.g. 20). 0 = use default 10.
        /// </summary>
        public static int ResolveSourTopTierPercent(int defaultPercent = 10)
        {
            int best = 0;
            ForEachPassiveRelicRule(1, (_, rule) =>
            {
                if (rule.Effect != RelicEffectType.OverrideSourTopTierPercent) return;
                if (rule.IntValue > best) best = rule.IntValue;
            });
            return best > 0 ? best : Mathf.Max(1, defaultPercent);
        }

        /// <summary>
        /// Passive: sour second-tier cooked percent ceiling (e.g. 70). 0 = use default 50.
        /// </summary>
        public static int ResolveSourSecondTierPercent(int defaultPercent = 50)
        {
            int best = 0;
            ForEachPassiveRelicRule(1, (_, rule) =>
            {
                if (rule.Effect != RelicEffectType.OverrideSourSecondTierPercent) return;
                if (rule.IntValue > best) best = rule.IntValue;
            });
            return best > 0 ? best : Mathf.Max(1, defaultPercent);
        }

        private static void ForEachPassiveRule(int levelTurnNumber, System.Action<RelicRule> action)
        {
            ForEachPassiveRelicRule(levelTurnNumber, (_, rule) => action(rule));
        }

        private static void ForEachPassiveRelicRule(int levelTurnNumber, System.Action<RelicItem, RelicRule> action)
        {
            if (action == null) return;
            var manager = RelicManager.Instance;
            if (manager == null) return;

            var ctx = new RelicContext(ResourceStore.Instance, null)
            {
                LevelTurnNumber = Mathf.Max(1, levelTurnNumber)
            };

            var owned = manager.Owned;
            for (int i = 0; i < owned.Count; i++)
            {
                var relic = owned[i];
                if (relic == null || relic.Rules == null) continue;
                for (int r = 0; r < relic.Rules.Count; r++)
                {
                    var rule = relic.Rules[r];
                    if (rule == null || rule.Trigger != RelicTrigger.Passive) continue;
                    if (!EvaluateCondition(rule, ctx)) continue;
                    action(relic, rule);
                }
            }
        }

        /// <summary>孢子入侵：蘑菇种类跟随蘑菇岗当前进阶（变异/肥大/奇异）。</summary>
        private static IngredientItem ResolveSporeInvasionMushroom(IngredientItem fallback)
        {
            if (fallback == null || !IsBaseMushroomIngredient(fallback))
                return fallback;

            var jobs = JobManager.Instance;
            var progression = JobProgressionManager.Instance;
            if (jobs == null || progression == null)
                return fallback;

            var mushroomJob = jobs.GetById("mushroom") ?? jobs.FindByName("蘑菇");
            if (mushroomJob == null)
                return fallback;

            var advancePath = progression.GetAdvancePath(mushroomJob);
            var mods = JobAdvanceGatherMods.From(mushroomJob, advancePath);
            var baseIngredient = mushroomJob.OutputIngredient ?? fallback;

            if (!mods.HasVariant || mods.VariantIngredient == null)
                return baseIngredient;

            return UnityEngine.Random.value < mods.VariantChance
                ? mods.VariantIngredient
                : baseIngredient;
        }

        private static bool IsBaseMushroomIngredient(IngredientItem ingredient)
        {
            if (ingredient == null) return false;
            if (string.Equals(ingredient.Id, "mushroom", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(ingredient.Id, "mushroom_relic", System.StringComparison.OrdinalIgnoreCase))
                return true;
            return string.Equals(ingredient.DisplayName, "蘑菇", System.StringComparison.Ordinal);
        }
    }
}
