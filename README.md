# Sus-tainable

Unity 6000.6.4f1 URP detective prototype. Open Assets/Scenes/Main.unity and press Play.

## Controls

- Drag across advertisement words to select a phrase; click selected words to remove it. Maximum three selections.
- Click physical LEGIT/SUS controls, or press L/S.
- Keyboard selection: arrows move through words; Space starts and ends a phrase.
- NEXT CASE advances after the verdict. Summary lets you replay.
- SOUND toggles audio; MOTION toggles reduced motion (including camera movement); TEXT + toggles larger text.

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
- IMAGE_MODEL: gpt-image-2, pending account confirmation.
- ONEENDPOINT_BASE_URL: https://1endpoint.dev/api/v1.
- CASE_RATE_LIMITER: configured on the generation Worker (six requests/minute per IP).

Pages forwards /api/cases to the private sus-tainable-factory Worker through its FACTORY service binding.
Deploy the Worker first: npx wrangler deploy --config wrangler.factory.jsonc
Configure its secret interactively: npx wrangler secret put ONEENDPOINT_API_KEY --config wrangler.factory.jsonc
Then deploy public/ to Pages. Confirm the exact DeepSeek model identifier before a live request.

Unconfigured service returns 503; Unity continues using local cases. Initial waiting is capped at eight seconds. Queue targets five complete cases and refills in the background.
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

Local illustrations remain procedural placeholders. Marker strokes, verdict stamping, guided practice, distinct fallback cases, persistent settings, and XP/rank feedback are implemented. Detective character animation, polished product art, live provider validation and hosted browser verification remain. Hosting is not yet configured.

Typography uses Barlow and Barlow Condensed under the SIL Open Font License; see Assets/Fonts for licenses.

## Desk interface refinement

The case and circular verdict controls use physical desk coordinates. Both controls sit above the tabletop; the smaller case clears the lamp, plant, PC and desk props. Case transitions travel only eight centimetres. Settings rows stay inside their menu. Repeated controls text is confined to practice and How to play. Blank-space clicks no longer select nearby words. Mouse look responds continuously with subtle limits, holds during evidence selection, and settles to neutral outside the viewport or while menus are open.

Live verification: compilation, settings containment and input gating, physical button raycasts, evidence add/remove/clear, practice plus a ten-case shift, and renderer bounds against the tabletop and desk props. Visual review on additional display sizes and touch usability still need a device pass.
