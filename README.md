# Card & Relic Tier Overlay for Slay the Spire 2

> 🤖 **Made with [Claude Code](https://claude.com/claude-code) using Claude Opus 5.5 (high effort)**: code, installer, docs and everything else.

A **Slay the Spire 2** mod that shows the tier from the Mobalytics [card](https://mobalytics.gg/slay-the-spire-2/tier-lists/cards) and [relic](https://mobalytics.gg/slay-the-spire-2/tier-lists/relics) tier lists (**S / A / B / C / D**) as a small badge on everything you are offered:

- **Cards:** card rewards after a fight, "choose a card" screens and the merchant. Also your deck, draw/discard pile views, upgrade/remove/transform choices, the card inspect screen and card previews in tooltips.
- **Relics:** treasure chests, "choose a relic" screens (boss relics), relic rewards after elites, Neow and the other Ancients' relic options, the merchant, and **every relic tooltip** (hover a relic anywhere, including your relic bar).

The badge sits on the top-right corner, so the energy cost stays visible. It's a dark disc with a colored ring and letter:

| Tier | Color  |
|------|--------|
| S    | red    |
| A    | orange |
| B    | yellow |
| C    | green  |
| D    | blue   |

Ships with 424 card tiers (Ironclad, Silent, Regent, Necrobinder, Defect, multiplayer) and 166 relic tiers.

Mobalytics doesn't rate everything (colorless cards, for example). When something unrated is **offered** to you, it gets a grey **?** badge, so you know the mod is working and there is simply no rating. While **browsing** (deck, piles, tooltips), unrated things get no badge, so Strikes and Defends stay clean. Cards in your hand during combat never get a badge.

## Install

1. Download `StsTierOverlay-vX.Y.Z.zip` from [Releases](https://github.com/ilikehotandcold-ux/sts2-card-tier-overlay/releases/latest) and extract the whole zip.
2. Close the game and double-click **`Install.bat`**.
   - It looks for the game in your Steam libraries.
   - If it can't find it, it asks you to drag in the folder that contains `SlayTheSpire2.exe`.
   - It also finds the game if you extract the zip inside or next to the game folder.
3. Start the game as usual: Steam, `SlayTheSpire2.exe` or your own `.bat`. If it asks whether to load mods, say yes.

To remove it: **`Uninstall.bat`**, or disable it in the game's *Mods* menu.

Manual install: copy the `StsTierOverlay` folder from the zip into `Slay the Spire 2\mods\`.

## How it works

It is a regular mod for the game's built-in mod loader (`mods/<id>/mod_manifest.json` + DLL). It does not matter how the game is launched: `SlayTheSpire2.exe` loads it itself.

- **Finding cards and relics:** every card node (`NCard`) and relic node (`NRelic`) that enters the scene is checked. If one of its parents has a type name containing one of the `_screens` patterns from `data/tiers.json`, it gets a badge: `cards` and `relics` are offer screens (unrated → "?"), `cardsBrowse` are screens where you look at cards you have (rated only). When a screen reuses a card node for another card (scrolling the deck, paging the inspect screen), the badge is refreshed. Relic rewards and Ancient/event options are recognised by their button types. Badges are removed when the node leaves the screen; the game recycles nodes, so badges never reach your hand.
- **Tooltips:** a Harmony postfix on the game's tooltip builder (`NHoverTipSet.Init`) adds the badge to every relic tooltip.
- **Matching names:** the game's class name (`PommelStrike`, `ChosenCheese`) is compared with the Mobalytics name (`pommel-strike`, `the-chosen-cheese`), ignoring case, dashes and a leading "the". Works with the game in any language.

### Built to survive game updates

| If after an update… | What happens | How to fix it |
|---|---|---|
| Mobalytics changes a tier list | nothing breaks | maintainer runs the monthly update; players get it at next start |
| new cards or relics are added | they have no badge until the tier list includes them | same as above |
| the game renames a screen | that screen shows no badges | change `_screens` in `data/tiers.json`; it is distributed automatically, no rebuild |
| the save system changes | the profile patch fails and the game falls back to its normal behaviour (`modded` profile) | rebuild |

- No hooks into specific screen methods. The only Harmony patches are the tooltip one and the profile ones, each applied separately: if one fails it is logged in `status.txt` and everything else keeps working.
- Tier data is fetched from this repo at every game start (see below), so most fixes are a data change, not a new release.
- It writes a health report to `mods\StsTierOverlay\status.txt`: where the data came from, which components are OK or failed, and which tier list names do not exist in your game version.

### Your progress

When mods are loaded, the game switches to a separate, empty `modded/` profile (no timeline, no unlocks). This mod declares `"affects_gameplay": false` and, **only if every loaded mod does the same**, keeps your normal profile. If you load any mod that changes gameplay, the original behaviour is kept.

## How tier updates reach players

```
 Mobalytics pages ──(maintainer, ~1x/month)──▶ data/tiers.json on GitHub ──(automatic)──▶ every player's game
```

**Players do nothing.** Every time the game starts, the mod downloads [`data/tiers.json`](data/tiers.json) from this repo (`raw.githubusercontent.com`, 8 s timeout) and uses it if its `_updated` date is at least as new as the copy it already has. Offline, it keeps using the last saved copy, or the data built into the DLL.

**The maintainer updates the repo by hand.** Mobalytics is behind Cloudflare bot protection, so it cannot be read by a bot, a GitHub Action or a scheduled script. Someone has to open the pages in a normal browser. It takes a couple of minutes, roughly once a month.

## Updating the tier lists (maintainer)

A [GitHub Action](.github/workflows/monthly-reminder.yml) opens a **"Monthly check: Mobalytics tier list"** issue on the 1st of each month, so you get a notification. It never contacts Mobalytics. You can also run it by hand from the *Actions* tab.

1. **Open** the [card tier list](https://mobalytics.gg/slay-the-spire-2/tier-lists/cards) and wait for it to load.
2. **Extract.** Run [`tools/extract-tiers.js`](tools/extract-tiers.js) on the page, either way:
   - **Bookmark (recommended):** once, create a bookmark whose URL is the contents of [`tools/bookmarklet.txt`](tools/bookmarklet.txt). Then just click it on the page.
   - **Console:** press F12, open *Console*, paste the whole file and press Enter.

   It saves `sts2-cards-YYYY-MM-DD.json` to your Downloads folder.
3. **Repeat on the [relic tier list](https://mobalytics.gg/slay-the-spire-2/tier-lists/relics).** Same bookmark; it saves `sts2-relics-YYYY-MM-DD.json`. You can skip a page you know didn't change.
4. **Publish.** In the repo folder, run:

   ```powershell
   .\tools\publish-tiers.ps1
   ```

   It takes the newest `sts2-cards*.json` and `sts2-relics*.json` from Downloads, checks them, and shows what changed:

   ```
   CARDS from sts2-cards-2026-12-01.json: 424 -> 425
     Added (1):
       + new-card  [Ironclad/A]
     Moved (1):
       ~ offering  Ironclad/S -> Ironclad/B

   RELICS from sts2-relics-2026-12-01.json: 166 -> 166
     No changes.
   ```

   Confirm with `y` and it writes `data/tiers.json` (keeping the repo's `_screens`), commits and pushes. If nothing changed, it says so and does nothing. Options: `-Path <files>`, `-Yes` (no prompt), `-NoPush`.
5. **Close the reminder issue.** Every player gets the new tiers the next time they start the game.

No new release or reinstall is needed for tier changes. A new release is only needed when the mod's code changes.

### `data/tiers.json` format

```json
{
  "_updated": "2026-11-11",
  "_sources": { "cards": "https://mobalytics.gg/…/cards", "relics": "https://mobalytics.gg/…/relics" },
  "_screens": {
    "cards":       ["CardReward", "ChooseACard", "Merchant"],
    "cardsBrowse": ["DeckView", "CardPileScreen", "InspectCard", "DeckCardSelect", "…", "HoverTipSet"],
    "relics":      ["TreasureRoomRelic", "ChooseARelic", "Merchant"]
  },
  "cards":  { "Ironclad": { "S": ["offering", "dominate"], "A": ["feed"] } },
  "relics": { "S": ["pocketwatch"], "A": ["kunai"] }
}
```

- Names are Mobalytics slugs (lowercase, dashes). Inside `cards` and `relics`, any grouping works: every list under an `S`/`A`/`B`/`C`/`D` key is read.
- `_screens` lists type-name fragments of the screens that show badges: `cards`/`relics` for offers, `cardsBrowse` for screens where you look at your own cards. If a game update renames a screen, fix it here; no rebuild is needed.
- To use your own tiers on one PC only, edit `mods\StsTierOverlay\tiers.txt` there and create an empty `no_update.txt` in the same folder so GitHub does not overwrite it.

## Building

Requirements: .NET SDK 8 or newer and the game installed. The .NET 9 SDK is not needed: the mod is compiled against the runtime assemblies that ship with the game.

```powershell
.\build.ps1                                   # finds the game via Steam or asks for it
.\build.ps1 -GameDir "C:\Games\Slay the Spire 2"
```

It produces `out/StsTierOverlay-vX.Y.Z.zip`, ready to share, and refreshes `tools/bookmarklet.txt`. To publish a code release: `gh release create vX.Y.Z out/StsTierOverlay-vX.Y.Z.zip`.

Layout:

```
src/        mod code: TierOverlay.cs (startup, sync, watcher), TierData.cs, Badges.cs,
            TooltipPatch.cs, ProfilePatches.cs
data/       tiers.json: card + relic tier lists and screen patterns (what the mod downloads)
installer/  Install.bat, Uninstall.bat, install.ps1, README.txt (zip contents)
tools/      extract-tiers.js + bookmarklet.txt: read the Mobalytics pages
            publish-tiers.ps1: diff, commit and push new tier lists
.github/    monthly reminder workflow
```

## Credits

- Made with [Claude Code](https://claude.com/claude-code) using **Claude Opus 5.5 (high effort)**.
- Tier lists: [Mobalytics](https://mobalytics.gg/slay-the-spire-2/tier-lists/cards). This project is not affiliated with Mobalytics or Mega Crit.
- Code under the [MIT](LICENSE) license.
