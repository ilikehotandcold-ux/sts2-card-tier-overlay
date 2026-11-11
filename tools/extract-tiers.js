// Reads the Mobalytics Slay the Spire 2 card tier list and saves it in the format of data/tiers.json.
//
// Maintainer tool (see "Updating the tier list" in README.md). Run it on
// https://mobalytics.gg/slay-the-spire-2/tier-lists/cards once the page has loaded, either:
//  * as a bookmark: create a bookmark whose URL is the contents of tools/bookmarklet.txt, or
//  * by pasting this whole file into the browser console (F12 > Console).
//
// It downloads sts2-tiers-YYYY-MM-DD.json to your Downloads folder; then run tools/publish-tiers.ps1.
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
    alert('Only found ' + count + ' cards. Wait for the page to finish loading and try again.');
    return;
  }
  const date = new Date().toISOString().slice(0, 10);
  // _screens is kept from the repo's current data/tiers.json by publish-tiers.ps1.
  const json = JSON.stringify({ _source: location.href.split('#')[0], _updated: date, ...tiers }, null, 2);
  const a = document.createElement('a');
  a.href = URL.createObjectURL(new Blob([json], { type: 'application/json' }));
  a.download = 'sts2-tiers-' + date + '.json';
  document.body.appendChild(a);
  a.click();
  setTimeout(() => { URL.revokeObjectURL(a.href); a.remove(); }, 2000);
  alert(count + ' cards saved to Downloads as ' + a.download + '.\nNext: run tools/publish-tiers.ps1');
})();
