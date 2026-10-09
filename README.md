# Sus-tainable

Unity 6000.6.4f1 URP detective prototype. Open Assets/Scenes/Main.unity and press Play.

## Controls

- Drag across advertisement words to select a phrase; click selected words to remove it. Maximum three selections.
- Click physical LEGIT/SUS controls, or press L/S.
- Keyboard selection: arrows move through words; Space starts and ends a phrase.
- NEXT CASE advances after the verdict. Summary lets you replay.
- SOUND toggles audio; MOTION toggles reduced motion (including camera movement); TEXT + toggles larger text.

## Hosted game

Play: https://sus-tainable.pages.dev/
Offline archive: https://sus-tainable.pages.dev/?offline=1

## Build

Unity menu: Sus-tainable > Build WebGL. Output is public/. Use an HTTP server; do not open index.html directly.
Local server: npx wrangler pages dev public
Deployment after authentication: npx wrangler pages deploy public --project-name sus-tainable

## AI

POST /api/cases with {difficulty:1,count:1}. Response: {ads:[case]}.
Fields: id, product, category, adText, isSus, tells [{phrase,type,explanation}], verdictText, difficulty, imageUrl, adImageUrl.
Images are data:image/png;base64 URLs. Both must decode before a live case becomes ready.

Server configuration:
- ONEENDPOINT_API_KEY: server secret only, never in Assets or public.
- TEXT_MODEL: confirmed available text model for case JSON.
- IMAGE_MODEL: gpt-image-2 (confirmed available).
- ONEENDPOINT_BASE_URL: https://1endpoint.dev/api/v1.
- CASE_RATE_LIMITER: configured on the generation Worker (six requests/minute per IP).

Pages forwards /api/cases to the private sus-tainable-factory Worker through its FACTORY service binding.
Deploy the Worker first: npx wrangler deploy --config wrangler.factory.jsonc
Configure its secret interactively: npx wrangler secret put ONEENDPOINT_API_KEY --config wrangler.factory.jsonc
Then deploy public/ to Pages. Confirmed text model: deepseek-v4.1-flash. Provider JSON can arrive in a Markdown code fence and is normalised before validation.

Unconfigured service returns 503; Unity continues using local cases. After practice, scored-shift preparation waits for three complete cases for up to eight seconds, with an immediate local-archive option. The queue targets five ready cases, with up to three generation requests in flight.
Product/ad artwork is generated separately; exact visual consistency needs reference-image edits in a later iteration.

## Verification

- npm test: server validation and error tests.
- Unity menu: Sus-tainable > Verify Case Content and Scoring.
- Unity menu: Sus-tainable > Verify Adaptive Agent.
- Full-shift and pointer regressions are run through the live Editor; no test objects are added to the game scene.

## Adaptive practice

Ready generated cases take priority over fallback when they fit the remaining six SUS / four LEGIT slots. Adaptation chooses among eligible cases and cannot leave usable live images stranded behind easier local categories.

Category mastery requires a correct verdict, every expected evidence phrase, and no false selections. The agent also records results for each greenwashing tell type and favours practice for missed tells. Evidence-aware memory uses a new v2 save key; old verdict-only history is not treated as mastered evidence. Images, score rules and career XP are unchanged.

## Remaining release work

Live generation and Cloudflare hosting are verified. Chrome loaded the WebGL runtime, completed practice keyboard selection/verdict/case advance, and resized without errors after scene startup. Every fallback case now has pre-generated AI product art and a landscape campaign stored locally. The detective radio portrait, original audio, cumulative badges and high-contrast controls are implemented. Final hosted visual/touch acceptance remains.

Typography uses Barlow and Barlow Condensed under the SIL Open Font License; see Assets/Fonts for licenses.

## Desk interface refinement

The case and circular verdict controls use physical desk coordinates. Both controls sit above the tabletop; the smaller case clears the lamp, plant, PC and desk props. Case transitions enter from the left and exit fully to the right. Settings rows stay inside their menu. Repeated controls text is confined to practice and How to play. Blank-space clicks no longer select nearby words. Mouse look responds continuously with subtle limits, holds during evidence selection, and settles to neutral outside the viewport or while menus are open.

Live verification: compilation, settings containment and input gating, physical button raycasts, evidence add/remove/clear, practice plus a ten-case shift, and renderer bounds against the tabletop and desk props. Visual review on additional display sizes and touch usability still need a device pass.

## Release checks, 9 October 2026

The adaptive-agent regression runs in Unity before builds. Server tests cover fenced provider JSON, invalid evidence, malformed requests, service routing and mocked two-image generation. Both selected model IDs were confirmed against the provider listing. One direct live case and one hosted Pages-to-Worker case returned valid metadata and two PNG images. The WebGL output is roughly 20 MB, below the 50 MB target, with each file below 25 MB. Full exception support is enabled for useful browser diagnostics. Wait for Unity splash/scene startup before using test hooks.

## Finished prototype presentation

All 30 local cases include two bundled AI images. Live and bundled campaigns use 3:2 landscape layouts with the exact case copy; click a campaign to read it full size. The selectable Unity claim remains the scoring source. Generated text may have visual imperfections, so use the selectable claim when judging evidence.

The no-splash build includes an animated low-poly detective radio portrait, original swing music and designed marker/paper/stamp/verdict audio, persistent cumulative badges, XP/rank progress, an impact counter, high-contrast controls, and three-star confetti. Motion effects respect reduced-motion mode. The four badge thresholds match the GDD: 20 tiny truths, 10 correct LEGIT calls, a perfect-verdict shift, and five correct cases under ten seconds.

Artwork generated through the configured 1endpoint gpt-image-2 API. Product prompt preserves the clean product-photo direction. Campaign prompt asks for a completed landscape print advert, brand name, exact headline/body copy, and no invented environmental claims. Prompt definitions are in functions/api/cases.js. All 60 saved JPEG assets are in Assets/Resources/CaseArt.

Campaign quality review: all 30 final ads are landscape and readable, with no extra environmental claims flagged. One ad omits a final period; its claim wording is intact. The selected Unity text remains authoritative for scoring. Failed training verdicts remain in Reading state; physical EventSystem input and direct mouse/touch fallback share a gated raycast handler.

## Completed feature release

Training regression verified in Unity and Chrome: empty SUS attempt leaves practice playable, then exact phrase highlighting and a physical SUS retry reaches the correct verdict. All 60 AI images are saved locally and all 30 campaigns are readable 3:2 ads. Generated ads were reviewed and edited to remove extra environmental claims; one final period is missing in GlowRoot, without changing its claim. Keyboard/UI focus is cleared after button actions so Space remains the marker control.

Release checks cover all bundled image paths, campaign shape, all four cumulative badge thresholds, rank/XP progress, adaptive learning, and server request validation. A complete offline browser shift reached its summary without Unity exceptions. The prototype uses procedural portrait poses on the supplied low-poly character rather than custom authored character animation clips. Desktop and touch campaign input are checked; broader physical-device acceptance remains useful.
