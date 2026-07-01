# Unity Extras

Unity Extras is a small runtime and editor utility package for designer-friendly Unity workflows. It focuses on serializable data helpers, UI Toolkit inspector drawers, and editor shortcuts that make common tuning tasks faster.

The package is split into:

- `Runtime`: serializable data types, attributes, and runtime-safe helpers.
- `Editor`: UI Toolkit drawers, editor windows, toolbars, and inspector enhancements.

The runtime assembly is named `UnityExtras`. The editor assembly is named `UnityExtras.Editor`.

## Namespace

Most runtime types live under:

```csharp
using UnityEditorExtras.Runtime;
```

Some older attributes in this package are in the global namespace. If a type does not resolve under `UnityEditorExtras.Runtime`, inspect the file in `Runtime/` and use the namespace declared there.

## Design and Balance Helpers

These helpers are intended for ScriptableObjects and serializable config classes. They are useful for economy, upgrade, reward, spawn, and progression data.

They follow a fail-loud rule: runtime evaluation throws when data is invalid. Empty tables, invalid weights, bad percentages, null curves, out-of-range normalized values, and impossible soft caps should be fixed in data instead of hidden by fallback code.

### BalanceCurve

`BalanceCurve` maps discrete upgrade steps or normalized progress to a value.

Use it when a designer needs:

- Upgrade costs from level 1 to max level.
- Crop yield by upgrade level.
- Truck capacity by order tier.
- Ball power by charge percent.
- Any value where the first and final values are known, and the middle should be shaped by a curve.

Fields:

- `minValue`: value at step 0 when the curve evaluates to 0.
- `maxValue`: value at the last step when the curve evaluates to 1.
- `steps`: number of discrete steps. Must be at least 2.
- `curve`: maps normalized `0..1` progress to a normalized value.
- `roundingMode`: optional `None`, `Floor`, `Round`, or `Ceil`.
- `roundingIncrement`: increment used by rounding modes. `5` rounds to multiples of 5, `10` rounds to multiples of 10, and `0.25` rounds to quarter steps.

Example:

```csharp
using UnityEditorExtras.Runtime;
using UnityEngine;

[CreateAssetMenu]
public sealed class CropUpgradeData : ScriptableObject
{
    public BalanceCurve coinCost = new BalanceCurve();
    public BalanceCurve durability = new BalanceCurve();

    public int GetCost(int upgradeIndex)
    {
        return coinCost.EvaluateStepAsInt(upgradeIndex);
    }

    public float GetDurability(int upgradeIndex)
    {
        return durability.EvaluateStep(upgradeIndex);
    }
}
```

Useful APIs:

```csharp
float value = curve.EvaluateStep(stepIndex);
float rawValue = curve.EvaluateStepRaw(stepIndex);
int intValue = curve.EvaluateStepAsInt(stepIndex);

float normalizedValue = curve.EvaluateNormalized(0.5f);
float rawNormalizedValue = curve.EvaluateNormalizedRaw(0.5f);
int nearestStep = curve.NormalizedToNearestStep(0.5f);
float t = curve.StepToNormalized(stepIndex);
```

Inspector behavior:

- Shows min, max, steps, curve, and rounding.
- Includes a step-snapping preview slider under the curve.
- Shows the current normalized position, nearest step, rounded value, and raw value.
- Includes a `Step Table` foldout that previews step values.

### WeightedTable<T>

`WeightedTable<T>` is for weighted random choices where the total does not need to equal 100.

Use it when a designer needs:

- Random crop choice.
- Random reward bundle.
- Random cosmetic or visual variant.
- Weighted special-ball selection.

Each entry has:

- `value`: the selected value.
- `weight`: relative weight. Must be greater than zero.

Example:

```csharp
using UnityEditorExtras.Runtime;
using UnityEngine;

[CreateAssetMenu]
public sealed class RewardRollData : ScriptableObject
{
    public WeightedTable<GameObject> rewardPrefabs = new WeightedTable<GameObject>();

    public GameObject RollRewardPrefab()
    {
        return rewardPrefabs.Roll();
    }

    public GameObject PickRewardPrefab(float normalizedRoll)
    {
        return rewardPrefabs.Pick(normalizedRoll);
    }
}
```

Useful APIs:

```csharp
T value = table.Roll();
T deterministicValue = table.Pick(0.25f);
int index = table.PickIndex(0.25f);
float total = table.TotalWeight;
float probability = table.GetProbability(index);
table.Validate();
```

Inspector behavior:

- Shows the serialized entries list.
- Shows total weight.
- Shows each entry's relative chance.
- Includes a `Sample Roll` button that selects an entry using `UnityEngine.Random.value`.

### BudgetBreakdown<T>

`BudgetBreakdown<T>` is for distributions that must sum to exactly 100%.

Use it when a designer needs:

- Reward composition that must total 100%.
- Resource distribution across crop types.
- Spawn budget by enemy/resource category.
- Any table where "remaining percent" matters.

Each entry has:

- `value`: the selected or allocated value.
- `percent`: share of the budget. Must be greater than zero.

At runtime, `Validate()` and roll APIs throw unless the total is `100%` within a small tolerance.

Example:

```csharp
using UnityEditorExtras.Runtime;
using UnityEngine;

[CreateAssetMenu]
public sealed class ResourceDistributionData : ScriptableObject
{
    public BudgetBreakdown<ResourceDefinition> resources = new BudgetBreakdown<ResourceDefinition>();

    public ResourceDefinition PickResource(float normalizedRoll)
    {
        return resources.Pick(normalizedRoll);
    }
}
```

Useful APIs:

```csharp
breakdown.Validate();
float total = breakdown.TotalPercent;
float remaining = breakdown.RemainingPercent;
float percent = breakdown.GetPercent(index);
float fraction = breakdown.GetFraction(index);
T value = breakdown.Pick(0.75f);
```

Inspector behavior:

- Shows the serialized entries list.
- Shows total percent and remaining percent.
- Shows each entry's percent and fraction.
- Includes a `Sample Roll` button.

### MinMaxCurve

`MinMaxCurve` maps a normalized value to a range between `minValue` and `maxValue`, shaped by an `AnimationCurve`.

Use it when a designer needs:

- Random reward amount with a curved distribution.
- Spawn delay from a normalized random roll.
- Hit impulse variance.
- Effect intensity variance.

This is different from `BalanceCurve`: `BalanceCurve` is step/progression oriented, while `MinMaxCurve` is range/sample oriented.

Example:

```csharp
using UnityEditorExtras.Runtime;
using UnityEngine;

[CreateAssetMenu]
public sealed class RewardAmountData : ScriptableObject
{
    public MinMaxCurve coinAmount = new MinMaxCurve();

    public int RollCoins()
    {
        return coinAmount.EvaluateAsInt(UnityEngine.Random.value);
    }
}
```

Useful APIs:

```csharp
float value = range.Evaluate(0.4f);
float rawValue = range.EvaluateRaw(0.4f);
float randomValue = range.RandomValue();
int intValue = range.EvaluateAsInt(0.4f);
range.Validate();
```

Inspector behavior:

- Shows min, max, curve, and rounding.
- Includes a preview slider that displays rounded and raw values.

### SoftCapCurve

`SoftCapCurve` produces linear growth until a configured step, then smoothly approaches a cap without hard-stopping.

Use it when a designer needs:

- Price growth that should slow near a target value.
- Reward growth that should remain exciting but not explode.
- Capacity or score targets that need diminishing returns.

Fields:

- `startValue`: value at step 0.
- `linearIncreasePerStep`: straight-line increase before the soft cap.
- `softCapStartStep`: step where tapering begins.
- `softCapValue`: asymptotic target. The curve approaches this value but does not hard clamp.
- `softnessInSteps`: how slowly the curve approaches the cap. Higher values taper more slowly.
- `roundingMode`: optional rounding for evaluated values.

Example:

```csharp
using UnityEditorExtras.Runtime;
using UnityEngine;

[CreateAssetMenu]
public sealed class EconomyGrowthData : ScriptableObject
{
    public SoftCapCurve truckReward = new SoftCapCurve();

    public int GetReward(int completedOrders)
    {
        return truckReward.EvaluateStepAsInt(completedOrders);
    }
}
```

Useful APIs:

```csharp
float value = curve.EvaluateStep(stepIndex);
float valueAtFractionalStep = curve.Evaluate(12.5f);
float rawValue = curve.EvaluateRaw(12.5f);
float linearOnly = curve.LinearValue(step);
int intValue = curve.EvaluateStepAsInt(stepIndex);
curve.Validate();
```

Inspector behavior:

- Shows all soft-cap parameters.
- Includes a preview step slider.
- Automatically extends the preview slider past the soft-cap start so the taper is visible.

## Existing Runtime Helpers

### Optional<T>

`Optional<T>` serializes an enabled toggle plus a value. Use this only when absence is a valid authored state.

```csharp
public Optional<AudioClip> overrideClip;

if (overrideClip)
{
    AudioClip clip = overrideClip;
}
```

The inspector drawer shows the value field and an enable toggle.

### InlineAttribute

`InlineAttribute` draws an embedded serialized object as always-open child fields instead of the default foldout.

Use it when:

- A small nested settings object should read as part of the parent inspector.
- The foldout adds unnecessary clicking.
- Designers usually edit all child fields together.

Do not use it on arrays/lists. It is intended for embedded serializable classes and structs.

Example:

```csharp
using System;
using UnityEditorExtras.Runtime;
using UnityEngine;

[Serializable]
public sealed class RewardTuning
{
    public BalanceCurve coinReward = new BalanceCurve();
    public MinMaxCurve burstDelay = new MinMaxCurve();
}

[CreateAssetMenu]
public sealed class EconomyData : ScriptableObject
{
    [Inline]
    public RewardTuning rewards = new RewardTuning();
}
```

The drawer keeps the group subtle: a small label, a thin left border, and light left padding. You can hide the label or tune the indent:

```csharp
[Inline(showLabel: false)]
public RewardTuning rewards;

[Inline(indentPixels: 4)]
public RewardTuning rewards;
```

### CompactList<T>

`CompactList<T>` is a list wrapper with a custom editor drawer for dense list editing. Use it when a normal array/list wastes too much vertical inspector space.

### Required

`Required` marks fields that should be assigned. The extended inspector support can highlight missing required references.

### MeasurementAttribute

`MeasurementAttribute` adds a small unit suffix next to a numeric field.

```csharp
[Measurement(MeasurementUnit.Second)]
public float cooldown;
```

Supported units include seconds, meters, kilograms, percent, hertz, and several derived units.

### SpritePreviewAttribute

`SpritePreviewAttribute` adds a sprite thumbnail preview beside a sprite field.

```csharp
[SpritePreview]
public Sprite icon;
```

### SceneSelectionAttribute

Use this when a string/int scene field should be selected from build settings instead of typed manually.

### TableAttribute

Adds table-style inspector display for supported list/array data.

### QuickAccessAttribute

Marks ScriptableObject types for the Quick Access editor window.

```csharp
[QuickAccess]
public sealed class DesignDataSo : ScriptableObject
{
}
```

### ButtonAttribute

Used by the extended inspector to expose methods as inspector buttons.

### TitleAttribute / InspectorTitleAttribute

Adds title-style inspector presentation for fields.

### ShowInInspectorAttribute

Allows supported non-field members to be shown by the extended inspector tooling.

### Timer

Runtime helper for simple time tracking. Inspect the source before using it in gameplay-critical paths so the semantics match the callsite.

## Existing Editor Tools

The package also includes editor-only utilities:

- Quick Access window and popup for quickly opening marked assets.
- Label browser window.
- Scene loading toolbar.
- Fast play button.
- Editor time scale toolbar.
- Version toolbar.
- Game View fullscreen toggle.

These tools are editor-only and live under the `Editor` assembly.

## Choosing the Right Helper

Use `BalanceCurve` when the input is an upgrade level, tier, or normalized progression value.

Use `WeightedTable<T>` when the entries are relative weights and the total can be any positive value.

Use `BudgetBreakdown<T>` when the entries must add to exactly 100%.

Use `MinMaxCurve` when the input is usually a random normalized roll and the output should sit between min and max.

Use `SoftCapCurve` when a value should grow linearly early and then taper toward a target.

Use `Optional<T>` only when the missing value is a real supported state. Do not use it to hide required data problems.

## Validation Workflow

This package is designed to be refreshed and compiled by Unity.

Recommended checks after editing package code:

1. Run `git diff --check` in the package folder.
2. Refresh Unity.
3. Open a ScriptableObject that uses the edited drawer.
4. Test invalid data once and confirm it fails loudly or shows a clear inspector error.
5. Test valid data and confirm the runtime evaluation method returns the expected value.

## Ideas for Future Additions

Good follow-up utilities for this package:

- `CurvePresetLibrary`: reusable named curve presets for economy and motion.
- `EnumMap<TEnum, TValue>`: serialized one-entry-per-enum table with missing/duplicate validation.
- `SerializableDictionary<TKey, TValue>` with a compact UI Toolkit drawer.
- `AssetReferenceTable<T>`: validated list of keyed ScriptableObject references.
- `TieredValue<T>`: ordered thresholds with preview for "at score X, use tier Y".
- `FormulaPreview`: inspector field that evaluates a simple authored formula across sample inputs.
- `ColorRampPreview`: gradient plus sampled swatches for VFX/art tuning.
- `ValidationReportWindow`: scans assets for `Validate()` methods and shows failures in one editor window.
