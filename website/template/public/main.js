/*
  The language switch.

  The site is English at the root and Spanish under /es/, so a page and its translation differ by
  one path segment. The list below is the pages that have both; on anything else — the generated
  API reference, which stays English — the switch is not rendered rather than pointing at a page
  that is not there.

  `iconLinks` is the template's own extension point for the right side of the navbar, which means
  the switch is placed by the template and survives a DocFX upgrade. The visible label comes from
  the title, in main.css.
*/

const TRANSLATED = [
  'index.html',
  'articles/getting-started.html',
  'articles/migrating-from-mediatr.html',
  'articles/pipeline.html',
  'articles/streaming.html',
  'articles/aot.html',
  'articles/performance.html',
];

function siteRoot() {
  const meta = document.querySelector('meta[name="docfx:rel"]');
  return meta ? new URL(meta.content || './', window.location.href) : null;
}

function pageWithinSite(root) {
  const here = decodeURIComponent(window.location.pathname);
  const base = decodeURIComponent(root.pathname);
  if (!here.startsWith(base)) {
    return null;
  }
  const path = here.slice(base.length);
  return path === '' || path.endsWith('/') ? path + 'index.html' : path;
}

const root = siteRoot();
const page = root ? pageWithinSite(root) : null;
const spanish = page !== null && page.startsWith('es/');
const counterpart = page === null ? null : spanish ? page.slice(3) : 'es/' + page;
const translated = page !== null && TRANSLATED.includes(spanish ? counterpart : page);

/*
  The navbar is built from the TOC at the root of the site, so on a Spanish page its links lead
  back into English. Sending them to the Spanish article index keeps a reader who is reading in
  Spanish reading in Spanish; the API link is left alone because there is only one of it. Only
  the list built from the TOC is touched, so the switch beside it — which is the one link that is
  meant to leave — keeps pointing at English. The observer is there because the template renders
  the navbar after this runs.
*/
function keepNavbarInSpanish() {
  const base = root.href;
  document.querySelectorAll('#navbar .navbar-nav a[href]').forEach((anchor) => {
    const target = new URL(anchor.getAttribute('href'), window.location.href).href;
    if (!target.startsWith(base)) {
      return;
    }
    const rest = target.slice(base.length);
    if (rest.startsWith('articles/')) {
      anchor.setAttribute('href', new URL('es/' + rest, root).href);
    }
  });
}

if (spanish) {
  document.documentElement.lang = 'es';
  new MutationObserver(keepNavbarInSpanish).observe(document.documentElement, {
    childList: true,
    subtree: true,
  });
  keepNavbarInSpanish();
}

export default {
  iconLinks: translated
    ? [
        {
          icon: 'translate',
          href: new URL(counterpart, root).href,
          title: spanish ? 'English' : 'Español',
        },
      ]
    : [],
};
