import {
  getAssetFromKV,
  mapRequestToAsset,
} from "@cloudflare/kv-asset-handler";

import { parseAcceptLanguage } from 'intl-parse-accept-language';

/**
 * The DEBUG flag will do two things that help during development:
 * 1. we will skip caching on the edge, which makes it easier to
 *    debug.
 * 2. we will return an error message on exception in your Response rather
 *    than the default 404.html page.
 */
const DEBUG = false;

addEventListener("fetch", (event) => {
  event.respondWith(handleEvent(event));
});

const STRIP_SUFFIX_RE = new RegExp('^(/.+)(?:/(?:index\.html)?|\.html)$');

async function handleEvent(event) {
  let options = {};

  /**
   * You can add custom logic to how we fetch your assets
   * by configuring the function `mapRequestToAsset`
   */
  // options.mapRequestToAsset = handlePrefix(/^\/docs/)

  try {
    if (DEBUG) {
      // customize caching
      options.cacheControl = {
        bypassCache: true,
      };
    }
    
    let response;
    
    const url = new URL(event.request.url);
    const path = url.pathname;
    const strip_match = STRIP_SUFFIX_RE.exec(path);
    
    if (url.searchParams.get("lang") === 'auto') {
      const languages = parseAcceptLanguage(event.request.headers.get('Accept-Language'));
      
      let resolvedLanguage = 'en';
      if (languages !== null) {
        for (const language of languages) {
          if (language === 'ja' || language === 'ja-JP') {
            resolvedLanguage = 'ja';
            break;
          }
          
          if (language === 'en' || language.startsWith('en-')) {
            resolvedLanguage = 'en';
            break;
          }
        }
      }
      
      let destination;
      switch (resolvedLanguage) {
        case 'ja':
          if (url.pathname.startsWith('/dev')) {
            destination = '/ja/dev' + url.pathname.substring(4);
          } else {
            destination = '/ja' + url.pathname;
          }
          break;
        default:
          destination = url.pathname;
          break;
      }
      
      response = new Response("Redirecting", {
        status: 301,
        headers: {
          Location: destination,
          Vary: 'Accept-Language',
        },
      });
      response.headers.set("Cache-Control", "private");
    } else if (strip_match !== null) {
      console.log("=== Redirect");
      response = new Response("Redirecting", {
        status: 301,
        headers: {
          Location: strip_match[1],
        }
      });
      response.headers.set("Cache-control", "public, max-age=3600");
    } else {
      let page;
      try {
        page = await getAssetFromKV(event, options);
      } catch (e) {
        // Try adding .html
        options.mapRequestToAsset = function(request) {
          return new Request(url.toString() + ".html", mapRequestToAsset(request));
        };

        page = await getAssetFromKV(event, options);
      }

      // allow headers to be altered
      response = new Response(page.body, page);
      response.headers.set("Cache-control", "public, max-age=3600");
    }

    response.headers.set("X-XSS-Protection", "1; mode=block");
    response.headers.set("X-Content-Type-Options", "nosniff");
    response.headers.set("X-Frame-Options", "DENY");
    response.headers.set("Referrer-Policy", "unsafe-url");
    response.headers.set("Feature-Policy", "none");

    return response;
  } catch (e) {
    console.log(e);
    // if an error is thrown try to serve the asset at 404.html
    if (!DEBUG) {
      try {
        let notFoundResponse = await getAssetFromKV(event, {
          mapRequestToAsset: (req) =>
            new Request(`${new URL(req.url).origin}/404.html`, req),
        });

        return new Response(notFoundResponse.body, {
          ...notFoundResponse,
          status: 404,
        });
      } catch (e) {}
    }

    return new Response(e.message || e.toString(), { status: 500 });
  }
}

/**
 * Here's one example of how to modify a request to
 * remove a specific prefix, in this case `/docs` from
 * the url. This can be useful if you are deploying to a
 * route on a zone, or if you only want your static content
 * to exist at a specific path.
 */
function handlePrefix(prefix) {
  return (request) => {
    // compute the default (e.g. / -> index.html)
    let defaultAssetKey = mapRequestToAsset(request);
    let url = new URL(defaultAssetKey.url);

    // strip the prefix from the path for lookup
    url.pathname = url.pathname.replace(prefix, "/");

    // inherit all other props from the default request
    return new Request(url.toString(), defaultAssetKey);
  };
}
