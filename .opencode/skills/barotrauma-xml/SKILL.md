---
name: Barotrauma Content XML
description: Author or debug Barotrauma content package XML - items, handheld weapons, submarine turrets, ammunition, character variants, prices and store availability, localization, and filelist.xml wiring. Use for any new Content/*.xml or when the game reports a prefab/XML loading error.
---

# Barotrauma content XML

Vanilla examples live in `D:\Steam\steamapps\common\Barotrauma\Content\`.
Always copy the closest vanilla file and strip it down - the schema is large and
undocumented attributes are easy to get wrong.

## File roots and filelist wiring

The root element must match the `filelist.xml` element that references the file:

| filelist entry | root element |
|---|---|
| `<Item file="%ModDir%/Content/Items/X.xml" />` | `<Items>` |
| `<Character file="%ModDir%/Content/Characters/X.xml" />` | `<Character>` or `<Charactervariant>` |
| `<Text file="%ModDir%/Text/English.xml" />` | `<infotexts language="English">` |
| `<Afflictions file=... />` | `<Afflictions>` |

`%ModDir%` is the package directory. Identifiers are global across all loaded
packages: prefix new ones with `go_`.

## Handheld weapon (verified minimal shape)

```xml
<Items>
  <Item identifier="go_harpoonrifle" category="Weapon" Scale="0.5"
        tags="mediumitem,weapon,gunsmith" cargocontaineridentifier="metalcrate">
    <Sprite texture="Content/Items/Weapons/weapons_new.png" sourcerect="0,203,181,39"
            depth="0.55" origin="0.5,0.5" />
    <Body width="180" height="30" density="25" />
    <Holdable slots="Any,RightHand+LeftHand" controlpose="true"
              holdpos="35,-10" aimpos="45,-10" handle1="-20,-6" handle2="30,10"
              holdangle="-40" msg="ItemMsgPickUpSelect" />
    <RangedWeapon reload="1.75" launchimpulse="20.0" barrelpos="85,14"
                  spread="1" unskilledspread="10" combatPriority="60">
      <Crosshair texture="Content/Items/Weapons/Crosshairs.png" sourcerect="0,256,256,256" />
    </RangedWeapon>
  </Item>
</Items>
```

## Ammunition / projectile (damage lives here)

```xml
<Item identifier="go_harpoonround" category="Weapon" maxstacksize="32" Scale="0.5"
      tags="mediumitem,handheldammo">
  <Sprite texture="Content/Items/Weapons/weapons_new.png" sourcerect="85,368,168,11"
          depth="0.55" origin="0.5,0.5" />
  <Body width="160" height="10" density="30" />
  <Pickable slots="Any" msg="ItemMsgPickUpSelect" />
  <Projectile launchimpulse="5" sticktocharacters="true" sticktoitems="true"
              sticktostructures="true">
    <Attack structuredamage="15" itemdamage="15" targetforce="20">
      <Affliction identifier="lacerations" strength="35" />
      <Affliction identifier="bleeding" strength="35" />
      <Affliction identifier="stun" strength="0.5" />
    </Attack>
  </Projectile>
</Item>
```

`<StatusEffect type="OnUse">` on the projectile or on a `<RangedWeapon>` is the
hook for exotic behaviour (spawn an item, apply an affliction, explode).

## Submarine turret

```xml
<Item identifier="go_turret" Tags="turret" category="Machine,Weapon"
      subcategory="subweapons" Scale="0.5" interactthroughwalls="true"
      linkable="true" allowedlinks="go_turretammo">
  <Sprite texture="Turrets.png" depth="0.01" sourcerect="772,8,248,248" />
  <StaticBody width="80" radius="80" />
  <Turret launchimpulse="80.0" barrelpos="126,82" firingoffset="0,-190"
          rotationlimits="180,360" powerconsumption="1000.0"
          showchargeindicator="true" showprojectileindicator="true"
          recoildistance="50" reload="0.25" rotationspeedlowskill="1"
          rotationspeedhighskill="8" MaxAngleOffset="10">
    <RequiredSkill identifier="weapons" level="50" />
    <StatusEffect type="OnUse" target="This">
      <Explosion range="1000.0" force="0.01" camerashake="5.0" />
    </StatusEffect>
  </Turret>
  <ConnectionPanel selectkey="Action" canbeselected="true" msg="ItemMsgRewireScrewdriver">
    <RequiredItem items="screwdriver" type="Equipped" />
    <input name="power_in" displayname="connection.powerin" />
    <input name="trigger_in" displayname="connection.turrettriggerin" />
  </ConnectionPanel>
</Item>
```

A turret needs a hardpoint (`turrethardpoint` / `largeturrethardpoint`) to be
installed on a submarine, plus a loader/ammo source linked via `allowedlinks`.

## Character variant (cheapest way to add an enemy)

```xml
<Charactervariant inherit="Crawler" speciesname="go_crawlerelite"
                  speciestranslationoverride="Crawler" usebosshealthbar="false">
  <health vitality="200" />
  <ai combatstrength="180" avoidgunfire="False" />
  <ragdoll scalemultiplier="1.15" />
  <attack damagemultiplier="1.2" rangemultiplier="1.1" impactmultiplier="1.2" />
</Charactervariant>
```

Inheriting avoids having to redefine ragdoll, animations and AI. Only override
what changes.

## Prices and store availability (XML-first trader overhaul)

Stock and pricing are driven by the `<Price>` element, not by code:

```xml
<Price baseprice="170" minavailable="1" soldbydefault="true">
  <Price storeidentifier="merchantoutpost"  multiplier="1.5"  minavailable="2" />
  <Price storeidentifier="merchantcity"     multiplier="1.25" sold="false" />
  <Price storeidentifier="merchantmilitary" multiplier="0.9"  minavailable="3" />
</Price>
```

`Barotrauma.PriceInfo` (see `_ref/project/Barotrauma/PriceInfo.cs`) reads:

| attribute | meaning |
|---|---|
| `baseprice` | base price in marks |
| `multiplier` | buying price multiplier for that store |
| `minavailable` / `maxavailable` (or `minamount`/`maxamount`) | stock range |
| `sold` / `soldbydefault` | whether the item appears at all |
| `canbespecial` | can become a daily special |
| `minleveldifficulty` | campaign difficulty gate |
| `requiresunlock`, `RequiredFaction` | progression/faction gate |
| `buyingpricemultiplier`, `displaynonempty` | price tweaks / UI hint |

Store identifiers used by vanilla: `merchantoutpost`, `merchantcity`,
`merchantresearch`, `merchantmilitary`, `merchantmine`, `merchantarmory`.
`CreateStock()` calls `ItemPrefab.CanBeBoughtFrom(storeInfo, out PriceInfo)`, so
this XML is enough to add or hide items per outpost type without any Harmony
patch. Use C# only for rules XML cannot express (campaign step, depth, reputation).

## Localization

```xml
<infotexts language="English" nowhitespace="false" translatedname="English">
  <entityname.go_harpoonrifle>Harpoon Rifle</entityname.go_harpoonrifle>
</infotexts>
```

`name=""` / missing `description` on an item means the game looks up
`entityname.<identifier>`; missing keys show the raw key in game.

## Validation checklist

- [ ] file exists at the path written in `filelist.xml` (`%ModDir%/...`)
- [ ] root element matches the filelist entry
- [ ] identifier unique, lowercase, prefixed
- [ ] every `texture=` path resolves relative to `Content/` (vanilla paths start
      with `Content/...`; files inside your own mod can be `%ModDir%/...`)
- [ ] `sourcerect` fits inside the texture
- [ ] localization keys exist in both `Text/English.xml` and `Text/Russian.xml`
- [ ] component order is irrelevant, but unknown attributes are silently ignored -
      check the vanilla file you copied from
- [ ] after loading, run `go_status` and check the log for XML errors
