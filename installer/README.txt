CARD TIER OVERLAY (Mobalytics) - Slay the Spire 2
=================================================
Made with Claude Code using Claude Opus 5.5 (high effort).

What it does
  Puts a badge with the Mobalytics tier (S, A, B, C, D) on the corner of
  each card when:
    - you pick a card reward after a fight
    - an effect asks you to "choose a card"
    - you are in the shop
  Colors:  S = red   A = orange   B = yellow   C = green   D = blue
  Cards without a tier (basic, colorless, curses...) get no badge.

Install
  1. Extract the whole zip anywhere.
  2. Close the game and double-click Install.bat
     (it looks for the game in your Steam libraries; if it can't find it,
     it asks you to drag in the folder that contains SlayTheSpire2.exe).
  3. Start the game as usual (Steam, the .exe or your own .bat). If it asks
     whether to load mods, say yes.

Uninstall
  Double-click Uninstall.bat (or disable it in the game's Mods menu).

Updating the tier list (in-game button)
  Main menu, bottom left: "Update tiers".
  - The first time it opens a help page: drag the "Update STS2 tiers"
    button to your browser's bookmarks bar.
  - After that it opens Mobalytics: click the bookmark and you're done. The
    game picks the file up from your Downloads folder within a few seconds,
    no restart needed.
  The game also downloads the tier list from the GitHub repo at startup if
  it is newer than yours.
  If something looks wrong, check  mods\StsTierOverlay\status.txt

Your progress
  Normally the game switches to an empty "modded" profile when mods are
  loaded. This mod is visual only, so it keeps the game on your normal
  profile (progress, timeline, unlocks). If you enable another mod that does
  change gameplay, the game goes back to its normal behaviour ("modded"
  profile).

Editing tiers by hand
  mods\StsTierOverlay\tiers.txt  (JSON). English card name, lowercase with
  dashes, under the letter you want. Create an empty file  no_update.txt
  in that folder so it is not overwritten from GitHub.
  Source: https://mobalytics.gg/slay-the-spire-2/tier-lists/cards

Code and updates
  https://github.com/ilikehotandcold-ux/sts2-card-tier-overlay
