// Extracts the Mobalytics STS2 card tier list into the format of data/tiers.json.
//
// How to use:
//   1. Open https://mobalytics.gg/slay-the-spire-2/tier-lists/cards in your browser and wait for it to load.
//   2. Press F12 -> Console, paste this whole file and press Enter.
//   3. The JSON is copied to the clipboard: paste it over data/tiers.json, commit and push.
//      Everyone with the mod gets the new tiers the next time they start the game.
(() => {
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
      if (character === 'Card') { character = null; continue; }   // page title
      tiers[character] = tiers[character] || {};
    } else if (!s.startsWith('#')) {
      tier = s;
    } else if (character && tier && tier !== 'TBD') {
      (tiers[character][tier] = tiers[character][tier] || []);
      if (!tiers[character][tier].includes(s.slice(1))) tiers[character][tier].push(s.slice(1));
    }
  }
  const out = {
    _source: location.href.split('#')[0],
    _updated: new Date().toISOString().slice(0, 10),
    // Type-name fragments of the screens whose cards get a badge. Edit here if a game update renames them.
    _screens: ['CardReward', 'ChooseACard', 'Merchant'],
    ...tiers,
  };
  const json = JSON.stringify(out, null, 2);
  const count = Object.values(tiers).reduce((a, t) => a + Object.values(t).flat().length, 0);
  if (typeof copy === 'function') { copy(json); console.log(`Copied ${count} cards to the clipboard.`); }
  else console.log(json);
  console.table(Object.fromEntries(Object.entries(tiers).map(([c, t]) => [c, Object.fromEntries(Object.entries(t).map(([k, v]) => [k, v.length]))])));
  return count;
})();
