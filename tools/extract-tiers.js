// Extracts the Mobalytics Slay the Spire 2 card tier list in the format of data/tiers.json.
//
// Used two ways (same code):
//  * Bookmarklet "Update STS2 tiers" (build.ps1 embeds it in the mod's update.html).
//  * Or paste it in the browser console (F12) on https://mobalytics.gg/slay-the-spire-2/tier-lists/cards
//
// It downloads sts2-tiers-YYYY-MM-DD.json (the game mod picks it up from the Downloads folder)
// and copies the same JSON to the clipboard (to paste over data/tiers.json in the repo).
(() => {
  if (!/mobalytics\.gg\/slay-the-spire-2\/tier-lists\/cards/.test(location.href)) {
    alert('Open the Mobalytics card tier list first:\nhttps://mobalytics.gg/slay-the-spire-2/tier-lists/cards');
    return;
  }
  const main = document.querySelector('main') || document.body;
  const walker = document.createTreeWalker(main, NodeFilter.SHOW_ELEMENT | NodeFilter.SHOW_TEXT);
  const seq = [];
  for (let n = walker.nextNode(); n; n = walker.nextNode()) {
    if (n.nodeType === Node.TEXT_NODE) {
      const t = n.textContent.trim();
      if (/^(S|A|B|C|D|TBD)$/.test(t) || /Tier List$/.test(t)) seq.push(t);
    } else if (n.tagName === 'IMG') {
      const m = (n.currentSrc || n.src).match(/cards\/(?:new\/)?([^\/?]+)\.webp/);
      if (m) seq.push('#' + m[1]);
    }
  }
  const tiers = {};
  let character = null, tier = null;
  for (const s of seq) {
    if (s.endsWith('Tier List')) {
      character = s.replace(/ Tier List$/, '');
      if (character === 'Card') { character = null; continue; } // page title
      tiers[character] = tiers[character] || {};
    } else if (!s.startsWith('#')) {
      tier = s;
    } else if (character && tier && tier !== 'TBD') {
      const list = (tiers[character][tier] = tiers[character][tier] || []);
      if (!list.includes(s.slice(1))) list.push(s.slice(1));
    }
  }
  const count = Object.values(tiers).reduce((a, t) => a + Object.values(t).flat().length, 0);
  if (count < 50) {
    alert('Only found ' + count + ' cards. Wait for the page to finish loading and click again.');
    return;
  }
  const date = new Date().toISOString().slice(0, 10);
  const json = JSON.stringify({
    _source: location.href.split('#')[0],
    _updated: date,
    // Type-name fragments of the screens whose cards get a badge. Edit if a game update renames them.
    _screens: ['CardReward', 'ChooseACard', 'Merchant'],
    ...tiers,
  }, null, 2);

  const a = document.createElement('a');
  a.href = URL.createObjectURL(new Blob([json], { type: 'application/json' }));
  a.download = 'sts2-tiers-' + date + '.json';
  document.body.appendChild(a);
  a.click();
  setTimeout(() => { URL.revokeObjectURL(a.href); a.remove(); }, 1000);
  try { navigator.clipboard.writeText(json); } catch (e) { /* clipboard is optional */ }

  alert(count + ' cards downloaded.\n\nThe game will load them in a few seconds.\n(The JSON is also on your clipboard, in case you want to update the repo.)');
})();
