# Card Tier Overlay for Slay the Spire 2

> 🤖 **Made with [Claude Code](https://claude.com/claude-code) using Claude Opus 5.5 (high effort)**: code, installer, docs and everything else.

A **Slay the Spire 2** mod that shows the tier from the [Mobalytics card tier list](https://mobalytics.gg/slay-the-spire-2/tier-lists/cards) (**S / A / B / C / D**) on the corner of every card you are offered:

- card rewards after a fight
- "choose a card" screens
- the merchant's shop

| Tier | Color  |
|------|--------|
| S    | red    |
| A    | orange |
| B    | yellow |
| C    | green  |
| D    | blue   |

It ships with the Ironclad, Silent, Regent, Necrobinder, Defect and multiplayer tier lists (424 cards). Cards without a tier (basic, colorless, curses…) get no badge. Nothing is shown during combat.

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

- **Finding cards:** every card node (`NCard`) that enters the scene is checked. If one of its parents has a type name containing one of the `_screens` patterns (by default `CardReward`, `ChooseACard`, `Merchant`), it gets a badge. When the card leaves that screen the badge is removed; the game recycles card nodes, so badges never reach your hand.
- **Matching cards:** the card's class name (`PommelStrike`) is compared with the Mobalytics name (`pommel-strike`), ignoring case and dashes. Works with the game in any language.

### Built to survive game updates

| If after an update… | What happens | How to fix it |
|---|---|---|
| Mobalytics changes the tier list | nothing breaks | in-game "Update tiers" button; optionally push it to `data/tiers.json` so everyone gets it |
| new cards are added | they have no badge | same as above |
| the game renames a screen | that screen shows no badges | change `_screens` in `data/tiers.json`; it is distributed automatically, no rebuild |
| the save system changes | the profile patch fails and the game falls back to its normal behaviour (`modded` profile) | rebuild |

- No hooks into specific screen methods. The only Harmony patches are the profile ones, each applied separately: if one fails it is logged and everything else keeps working.
- At startup the mod downloads `data/tiers.json` from this repo (`raw.githubusercontent.com`, 8 s timeout) and stores it as `tiers.txt` if it is newer than what you have. Offline it uses the last saved copy, or the data built into the DLL.
- It writes a health report to `mods\StsTierOverlay\status.txt`: where the data came from, which components are OK or failed, and which tier list names do not exist in your game version.

### Your progress

When mods are loaded, the game switches to a separate, empty `modded/` profile (no timeline, no unlocks). This mod declares `"affects_gameplay": false` and, **only if every loaded mod does the same**, keeps your normal profile. If you load any mod that changes gameplay, the original behaviour is kept.

## Updating the tier list

Mobalytics blocks automated clients (Cloudflare), so the page is read by you, in your own browser. With the in-game button it takes two clicks:

1. On the game's main menu, click **"Update tiers"** (bottom left).
   - **The first time** a help page opens: drag the **"Update STS2 tiers"** button to your bookmarks bar (`Ctrl+Shift+B` if you can't see it).
   - After that, it opens the Mobalytics tier list directly.
2. On Mobalytics, once the page has loaded, click the **"Update STS2 tiers"** bookmark. It downloads `sts2-tiers-DATE.json`.
3. The game, which is waiting, picks the file up from your Downloads folder within a few seconds and loads the new tiers without restarting. The button shows "Updated: N cards".

If you download the file while the game is closed, it is loaded the next time you start the game.

To share it with everyone who has the mod: the bookmark also copies the JSON to your clipboard. Paste it over [`data/tiers.json`](data/tiers.json) on GitHub and commit. The data with the most recent `_updated` date always wins, so GitHub never overwrites a newer local update.

Without the bookmark: paste [`tools/extract-tiers.js`](tools/extract-tiers.js) into the browser console (F12) on the Mobalytics page. Same result.

`data/tiers.json` format:

```json
{
  "_updated": "2026-11-11",
  "_screens": ["CardReward", "ChooseACard", "Merchant"],
  "Ironclad": { "S": ["offering", "dominate"], "A": ["feed"] }
}
```

To use your own tiers on your PC only, edit `mods\StsTierOverlay\tiers.txt` and create an empty `no_update.txt` in the same folder so GitHub does not overwrite it.

## Building

Requirements: .NET SDK 8 or newer and the game installed. The .NET 9 SDK is not needed: the mod is compiled against the runtime assemblies that ship with the game.

```powershell
.\build.ps1                                   # finds the game via Steam or asks for it
.\build.ps1 -GameDir "C:\Games\Slay the Spire 2"
```

It produces `out/StsTierOverlay-vX.Y.Z.zip`, ready to share. To publish it: `gh release create vX.Y.Z out/StsTierOverlay-vX.Y.Z.zip`.

Layout:

```
src/        mod code (TierOverlay.cs, TierUpdater.cs) and mod_manifest.json
data/       tiers.json: tier list + screen patterns (what the mod downloads)
installer/  Install.bat, Uninstall.bat, install.ps1, README.txt, update.html template
tools/      extract-tiers.js: reads the Mobalytics tier list (bookmark / console)
```

## Credits

- Made with [Claude Code](https://claude.com/claude-code) using **Claude Opus 5.5 (high effort)**.
- Tier list: [Mobalytics](https://mobalytics.gg/slay-the-spire-2/tier-lists/cards). This project is not affiliated with Mobalytics or Mega Crit.
- Code under the [MIT](LICENSE) license.
