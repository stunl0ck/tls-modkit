
# TLS.ModKit — Quick Start

> This guide targets **v1.0.x** of TLS.ModKit. If something looks off, check the README for updates.


## What v1.0.x Supports (Today)

> *Note: Omens are referred to as Glyphs in the game files*.

### Targets
- **GlyphDefinition** (via `Targets/Glyphs/GlyphTargetAdapter`)
- **PerkDefinition** (via `Targets/Perks/PerkTargetAdapter`)
- **SkillDefinition** (via `Targets/Skills/SkillsTargetAdapter`)
- **ItemDefinition** (via `Targets/Items/ItemTargetAdapter`)
- **CityStash** (via `Targets/CityStash/CityStashTargetAdapter`)

### Actions
- **add** — parse the provided `<…Definition>` and **insert** (or **replace** if `replace="true"`).
- **remove** — delete by `id`.
- **replace** — replace a full definition by `id` (safest for structural changes).
- **edit** — apply `<Set .../>` operations:
  - `PerkDefinition`: token-only edits (modify values in its token bag).
  - `SkillDefinition`: best-effort field/property edits (simple values).
  - `ItemDefinition`: `edit` is not implemented (use `replace`).
  - `CityStash`: `edit` is not supported (use `add`).


## Token Edit Selector (PerkDefinition)

**Short form:**

```xml
<Set select="token:StatMalus" value="0"/>
```

Only **existing** token keys can be set. If you need to change structure or non‑token fields, use **`action="replace"`** instead.


## MCM Interpolation (Optional)

> Requires [MCM](<https://github.com/stunl0ck/tls-mcm>) to be installed.

Anywhere in your XML **attribute values** you can embed:

```
${MCM:<modId>/<key>}
```

TLS.ModKit resolves this to the current Mod Config value **before** the game parses the XML.  
(Used in the glyph cost example below.)

## Glyph Icon Override

For your custom Glyph, drop `ModKit/Icons/<GlyphId>.png` in your mod folder; ModKit auto-loads it.

Alternatively, inside a `<GlyphDefinition>`, you can reference a game sprite (or provide a PNG via `ModKit/Icons`):

```xml
<IconOverride Path="Assets/Resources/view/sprites/ui/perks/Specialist" />
```

> Note: unless the sprite is the same type/size it won't scale well, in that case `ModKit/Icons/<GlyphId>.png` is recommended.
```
BepInEx/plugins/<YourMod>/ModKit/
  Icons/
    *.png
```

## "Native" XML Layouts (Copy/Paste Patterns)

> Place patch files under `BepInEx/plugins/<YourMod>/ModKit/<Target>/<name>.patch.xml`

### 1) Add a new glyph

**File:** `ModKit/Glyphs/myglyph.patch.xml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<Patch target="GlyphDefinition" action="add">
  <Definition>
    <GlyphDefinition Id="Specialist" Cost="${MCM:com.omenofspecialists.mod/SlotsCost}" IsCustom="false">
      <IconOverride Path="Assets/Resources/view/sprites/ui/perks/Specialist" />
      <TokenVariables>
        <TokenVariable Key="PerkId" Value="Specialist"/>
      </TokenVariables>
      <LocArguments>
        <LocalizedArgument Prefix="PerkName_" Value="PerkId"/>
      </LocArguments>
      <GlyphEffects>
        <NativePerk PerkId="PerkId"/>
      </GlyphEffects>
    </GlyphDefinition>
  </Definition>
</Patch>
```

### 2) Remove a glyph

**File:** `ModKit/Glyphs/remove_something.patch.xml`

```xml
<Patch target="GlyphDefinition" action="remove" id="SomeGlyphId"/>
```

### 3) Add a new perk

**File:** `ModKit/Perks/newperk.patch.xml`

```xml
<Patch target="PerkDefinition" action="add">
  <Definition>
    <PerkDefinition Id="MyPerk">
      <!-- full native perk XML here -->
    </PerkDefinition>
  </Definition>
</Patch>
```

### 4) Remove a perk

**File:** `ModKit/Perks/remove_intothewild.patch.xml`

```xml
<Patch target="PerkDefinition" action="remove" id="IntoTheWild"/>
```

### 5) Edit a perk’s token (balance tweak)

**File:** `ModKit/Perks/intothewilds.patch.xml`

```xml
<Patch target="PerkDefinition" action="edit" id="IntoTheWild">
  <Set select="token:StatMalus" value="0"/>
</Patch>
```

### 6) Replace a perk entirely (structural changes)

**File:** `ModKit/Perks/overwrite_intothewilds.patch.xml`

```xml
<Patch target="PerkDefinition" action="replace" id="IntoTheWild">
  <Definition>
    <PerkDefinition Id="IntoTheWild">
      <!-- your fully-edited version, e.g. different modules/events/effects -->
    </PerkDefinition>
  </Definition>
</Patch>
```
> See full perk definition below or extract them from game files using UABEA

## What’s **Not** Covered by `edit` (Today)

- **Structural changes**: adding/removing modules or changing event/action graphs.
- **View/LocArguments** edits when not driven by tokens.
- **Arbitrary node/attribute** edits outside the token bag.
- **Adding/removing token keys** (only values of existing keys can be set).

➡ For these, use `action="replace"` with a complete `<PerkDefinition>` (simple and deterministic).

> A future "XML-edit → reparse" mode could enable non‑token edits without full replace. Not implemented in v1 to keep it small and safe.

## Where Patches Live

```
BepInEx/plugins/<YourMod>/ModKit/
  Glyphs/
    *.patch.xml
  Perks/
    *.patch.xml
  Skills/
    *.patch.xml
  Items/
    *.patch.xml
  CityStash/
    *.patch.xml
  Icons/
    *.png
```

The engine auto-discovers and applies patches when each database deserializes.

## TL;DR

- **add / remove** for straightforward insert/delete.
- **edit** for **TokenVariables** tweaks in `PerkDefinition` (e.g., `StatMalus`, `AccuracyBonus`, etc.) and simple `SkillDefinition` fields.
- **replace** for structural/UI/Loc changes.
- **MCM values** and **glyph icon overrides** are supported directly in XML.

## Runtime Patch: City Stash (NewGame only)

This target runs only when starting a **new run** (game state `NewGame`). It adds items into the **city stash**.

**File:** `ModKit/CityStash/starter_items.patch.xml`

```xml
<Patch target="CityStash" action="add">
  <Definition>
    <Item Id="LightningBlade" Qty="1" Level="0" Rarity="Common"/>
  </Definition>
</Patch>
```

Notes:
- `Id` must exist in `ItemDatabase` (use an `ItemDefinition` patch if you’re adding a custom item).
- Defaults: `Qty=1`, `Level=0`, `Rarity=Common`.

## Advanced Examples

- **Omen of Specialists** — a clean example of a custom glyph + perk setup, and how to handle cases that require a tiny helper plugin.  
  Repo: <https://github.com/stunl0ck/tls-omen-of-specialists>

- **Lightning Blade** — a “full stack” example: custom item + skills + icons, plus how to wire optional config (MCM) for gameplay toggles.  
  Repo: <https://github.com/stunl0ck/tls-lightning-blade>

If you’re adding a custom weapon, these are the main folders to study:
- `ModKit/Items/*.patch.xml` (ItemDefinition)
- `ModKit/Skills/*.patch.xml` (SkillDefinition)
- `ModKit/Icons/*.png` (UI + hand sprites)
- `ModKit/CityStash/*.patch.xml` (starter items on new runs)


## Tools & Tips

- **UABEA** — extract Unity `TextAsset` contents to inspect the game’s XML.  
  <https://github.com/nesrak1/UABEA>
- **dnSpy / ILSpy** — inspect `TheLastStand.dll` to see databases and models (useful for advanced BepInEx helpers).  
  dnSpy: <https://github.com/dnSpy/dnSpy> · ILSpy: <https://github.com/icsharpcode/ILSpy>
- **Enable BepInEx console**: edit `BepInEx/config/BepInEx.cfg`
  ```ini
  [Logging.Console]
  Enabled = true
  ```

## Advanced Note — Example: Omen of Specialists

Some base-game systems fire **before** certain UI layers exist. If a mod performs actions that rely on the UI (e.g., removing equipment slots at run start), it may cause a crash.

**Fix pattern:** a tiny BepInEx helper to delay or queue the risky action until after the UI is ready or other updates may be necessary. [How it was fixed with Omen of Specialists](<https://github.com/stunl0ck/tls-omen-of-specialists>)

If you hit something like this, consider shipping a small plugin alongside your data patch.


## Full Perk Definition Example 
Original `IntoTheWild` (extracted with  UABEA):

```xml
<PerkDefinition Id="IntoTheWild">
  <TokenVariables>
    <TokenVariable Key="MovePointReduction"   Value="1" />
    <TokenVariable Key="StatMalus"            Value="-27" />
    <TokenVariable Key="StatBonus"            Value="7" />
    <TokenVariable Key="PerkNeedRefresh"      Value="Module.Buffer ! IsInsideHaven" />
    <TokenVariable Key="IsInsideHaven"        Value="BooleanChoice(Owner.IsInCity, 1, 0)" />
    <TokenVariable Key="InsideHavenModifier"  Value="BooleanChoice(Owner.IsInCity, 1, 0)" />
    <TokenVariable Key="OutsideHavenModifier" Value="BooleanChoice(Owner.IsInCity, 0, 1)" />
  </TokenVariables>

  <LocArguments>
    <LocalizedArgument Prefix="SkillName_" Value="JumpOverWall" Style="Skill" />
    <LocArgument      Prefix="-"          Value="MovePointReduction" Style="GoodNb" />
    <StatArgument     Stat="MovePointsTotal" />
    <StatArgument     Stat="OverallDamage" Value="StatMalus" />
    <StatArgument     Stat="Accuracy"      Value="StatBonus" />
  </LocArguments>

  <View>
    <DisplayInHUD>
      <GreyOut>
        <IsTrue Value="IsInsideHaven = 1" />
      </GreyOut>
      <HoverDisplay>
        <HavenArea />
      </HoverDisplay>
    </DisplayInHUD>
    <FeedbackActivationConditions>
      <IsTrue Value="IsInsideHaven = 0" />
    </FeedbackActivationConditions>
  </View>

  <Modules>
    <BufferModule>
      <Conditions>
        <IsTrue Value="Owner.IsInWorld" />
      </Conditions>

      <Events>
        <!-- Init on unit spawn -->
        <Event EffectTime="OnUnitSpawned">
          <Actions>
            <SetBufferTo BufferIndex="Buffer" Value="IsInsideHaven" />
            <RefreshPerkActivationFeedback RefreshView="false" />
          </Actions>
        </Event>

        <!-- OUT OF HAVEN -->
        <Event EffectTime="OnMovementEnd">
          <Conditions>
            <IsTrue Value="IsInsideHaven = 0" />
            <IsTrue Value="PerkNeedRefresh" />
          </Conditions>
          <Actions>
            <SetBufferTo BufferIndex="Buffer" Value="IsInsideHaven" />
            <RefreshPerkActivationFeedback RefreshView="true" />
            <InstantiateStatEffectDisplay Value="StatBonus"      Stat="Accuracy" />
            <InstantiateStatEffectDisplay Value="StatMalus * -1" Stat="OverallDamage" />
          </Actions>
        </Event>

        <!-- IN HAVEN -->
        <Event EffectTime="OnMovementEnd">
          <Conditions>
            <IsTrue Value="IsInsideHaven = 1" />
            <IsTrue Value="PerkNeedRefresh" />
          </Conditions>
          <Actions>
            <SetBufferTo BufferIndex="Buffer" Value="IsInsideHaven" />
            <RefreshPerkActivationFeedback RefreshView="true" />
            <InstantiateStatEffectDisplay Value="StatBonus * -1" Stat="Accuracy" />
            <InstantiateStatEffectDisplay Value="StatMalus"      Stat="OverallDamage" />
          </Actions>
        </Event>
      </Events>

      <Effects>
        <StatModifier Stat="OverallDamage" Value="StatMalus * InsideHavenModifier" />
        <StatModifier Stat="Accuracy"      Value="StatBonus * OutsideHavenModifier" />
        <ReplacePerk PerkToReplaceId="Leapfrog" PerkReplacementId="ElvenLeapfrog" />
        <UnlockContextualSkill ContextualSkillId="ElvenJumpOverWall" />
        <LockSkill SkillId="JumpOverWall" />
      </Effects>
    </BufferModule>
  </Modules>
</PerkDefinition>
```

---

Happy modding!
