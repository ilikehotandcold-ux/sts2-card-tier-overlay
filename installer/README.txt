CARD & RELIC TIER OVERLAY (Mobalytics) - Slay the Spire 2
=========================================================
Made with Claude Code using Claude Opus 5.5 (high effort).

What it does
  Puts a small badge with the Mobalytics tier (S, A, B, C, D) on the
  top-right corner of cards and relics:
    - card rewards, "choose a card" screens and the shop
    - your deck, pile views, upgrade/remove choices, card inspect screen
    - treasure chests, boss relic choices, relic rewards, Neow's options
    - every relic tooltip (hover a relic anywhere, even in your relic bar)
  Colors:  S = red   A = orange   B = yellow   C = green   D = blue
  A grey "?" means the item is offered but Mobalytics doesn't rate it
  (e.g. colorless cards). Nothing is shown on cards in your hand in combat.

Install
  1. Extract the whole zip anywhere.
  2. Close the game and double-click Install.bat
     (it looks for the game in your Steam libraries; if it can't find it,
     it asks you to drag in the folder that contains SlayTheSpire2.exe).
  3. Start the game as usual (Steam, the .exe or your own .bat). If it asks
     whether to load mods, say yes.

Uninstall
  Double-click Uninstall.bat (or disable it in the game's Mods menu).

Tier list updates
  Automatic. Every time the game starts, the mod downloads the latest tier
  lists from the GitHub repo (if it is newer than yours). Nothing to do.
  If something looks wrong, check  mods\StsTierOverlay\status.txt

Your progress
  Normally the game switches to an empty "modded" profile when mods are
  loaded. This mod is visual only, so it keeps the game on your normal
  profile (progress, timeline, unlocks). If you enable another mod that does
  change gameplay, the game goes back to its normal behaviour ("modded"
  profile).

Editing tiers by hand
  mods\StsTierOverlay\tiers.txt  (JSON). English card name, lowercase with
  dashes, under the letter you want (sections "cards" and "relics"). Create an empty file  no_update.txt
  in that folder so it is not overwritten from GitHub.
  Sources: https://mobalytics.gg/slay-the-spire-2/tier-lists/cards
           https://mobalytics.gg/slay-the-spire-2/tier-lists/relics

Code and updates
  https://github.com/ilikehotandcold-ux/sts2-card-tier-overlay
