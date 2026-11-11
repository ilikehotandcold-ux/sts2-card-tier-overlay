// Reads a Mobalytics Slay the Spire 2 tier list (cards or relics) and saves it for tools/publish-tiers.ps1.
//
// Maintainer tool (see "Updating the tier lists" in README.md). Run it, once the page has loaded, on:
//   https://mobalytics.gg/slay-the-spire-2/tier-lists/cards   -> saves sts2-cards-YYYY-MM-DD.json
//   https://mobalytics.gg/slay-the-spire-2/tier-lists/relics  -> saves sts2-relics-YYYY-MM-DD.json
// either as a bookmark (URL = contents of tools/bookmarklet.txt) or pasted into the browser console (F12).
(() => {
  const kind = (location.pathname.match(/\/slay-the-spire-2\/tier-lists\/(cards|relics)/) || [])[1];
  if (!kind) {
    alert('Open one of the Mobalytics tier lists first:\n' +
      'https://mobalytics.gg/slay-the-spire-2/tier-lists/cards\n' +
      'https://mobalytics.gg/slay-the-spire-2/tier-lists/relics');
    return;
  }
  const main = document.querySelector('main') || document.body;
  const slugOf = (img, folder) => {
    const m = (img.currentSrc || img.src || '').match(new RegExp(folder + '\\/(?:new\\/)?([^\\/?]+)\\.webp'));
    return m ? m[1] : null;
  };

  let data, count;
  if (kind === 'cards') {
    // Cards: one tier list per character, in page order: "<Character> Tier List", then tier labels and images.
    const walker = document.createTreeWalker(main, NodeFilter.SHOW_ELEMENT | NodeFilter.SHOW_TEXT);
    const cards = {};
    let character = null, tier = null;
    for (let n = walker.nextNode(); n; n = walker.nextNode()) {
      if (n.nodeType === Node.TEXT_NODE) {
        const t = n.textContent.trim();
        if (/Tier List$/.test(t)) {
          character = t.replace(/ Tier List$/, '');
          if (character === 'Card') character = null; // page title
          else cards[character] = cards[character] || {};
        } else if (/^(S|A|B|C|D|TBD)$/.test(t)) {
          tier = t;
        }
      } else if (n.tagName === 'IMG' && character && tier && tier !== 'TBD') {
        const slug = slugOf(n, 'cards');
        const list = slug && (cards[character][tier] = cards[character][tier] || []);
        if (slug && !list.includes(slug)) list.push(slug);
      }
    }
    data = { cards };
    count = Object.values(cards).reduce((a, t) => a + Object.values(t).flat().length, 0);
  } else {
    // Relics: one list; each tier label sits in a row with its relic images. (Relic images in the article
    // text above the rows are not inside a labelled row, so they are skipped.)
    const relics = {};
    const labels = [...main.querySelectorAll('*')].filter(e => e.children.length === 0 && /^(S|A|B|C|D)$/.test(e.textContent.trim()));
    for (const label of labels) {
      let row = label;
      for (let i = 0; i < 8 && row && !row.querySelector('img[src*="relics/"]'); i++) row = row.parentElement;
      if (!row) continue;
      const tier = label.textContent.trim();
      const list = (relics[tier] = relics[tier] || []);
      for (const img of row.querySelectorAll('img')) {
        const slug = slugOf(img, 'relics');
        if (slug && !list.includes(slug)) list.push(slug);
      }
    }
    data = { relics };
    count = Object.values(relics).flat().length;
  }

  if (count < 50) {
    alert('Only found ' + count + ' ' + kind + '. Wait for the page to finish loading and try again.');
    return;
  }
  const date = new Date().toISOString().slice(0, 10);
  const json = JSON.stringify({ _source: location.href.split('#')[0], _updated: date, ...data }, null, 2);
  const a = document.createElement('a');
  a.href = URL.createObjectURL(new Blob([json], { type: 'application/json' }));
  a.download = 'sts2-' + kind + '-' + date + '.json';
  document.body.appendChild(a);
  a.click();
  setTimeout(() => { URL.revokeObjectURL(a.href); a.remove(); }, 2000);
  alert(count + ' ' + kind + ' saved to Downloads as ' + a.download + '.\n' +
    (kind === 'cards' ? 'Do the relics page too if it changed, then run' : 'Then run') + ' tools/publish-tiers.ps1');
})();
